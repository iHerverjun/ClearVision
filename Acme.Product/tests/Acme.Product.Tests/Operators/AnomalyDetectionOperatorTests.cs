using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.AI.Anomaly;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public sealed class AnomalyDetectionOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_TrainMode_ShouldPersistFeatureBankAndReturnPreviewOutputs()
    {
        var sut = new AnomalyDetectionOperator(Substitute.For<ILogger<AnomalyDetectionOperator>>());
        var featureBankPath = Path.Combine(Path.GetTempPath(), $"anomaly-bank-{Guid.NewGuid():N}.json");
        var op = CreateTrainOperator(featureBankPath);

        using var normalA = CreateUniformImage(new Scalar(90, 90, 90));
        using var normalB = CreateUniformImage(new Scalar(100, 100, 100));
        using var preview = CreateUniformImage(new Scalar(95, 95, 95));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = preview,
            ["NormalImages"] = new[] { normalA, normalB }
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        File.Exists(featureBankPath).Should().BeTrue();
        result.OutputData!["FeatureBankPath"].Should().Be(featureBankPath);
        result.OutputData["PatchCount"].Should().BeOfType<int>();

        DisposeOutputs(result.OutputData);
        File.Delete(featureBankPath);
    }

    [Fact]
    public async Task ExecuteAsync_InferenceMode_ShouldDetectInjectedDefect()
    {
        var sut = new AnomalyDetectionOperator(Substitute.For<ILogger<AnomalyDetectionOperator>>());
        var featureBankPath = Path.Combine(Path.GetTempPath(), $"anomaly-bank-{Guid.NewGuid():N}.json");

        using var normalA = CreateUniformImage(new Scalar(90, 90, 90));
        using var normalB = CreateUniformImage(new Scalar(92, 92, 92));
        var bank = SimplePatchCoreDetector.BuildFeatureBank(
            new[] { normalA.GetMat(), normalB.GetMat() },
            new SimplePatchCoreOptions { PatchSize = 24, PatchStride = 12, CoresetRatio = 1.0 });
        SimplePatchCoreDetector.Save(featureBankPath, bank);

        var inferenceOp = CreateInferenceOperator(featureBankPath);

        using var defect = CreateUniformImage(new Scalar(90, 90, 90));
        var writable = defect.GetWritableMat();
        Cv2.Rectangle(writable, new Rect(40, 40, 32, 32), new Scalar(250, 250, 250), -1);

        var result = await sut.ExecuteAsync(inferenceOp, TestHelpers.CreateImageInputs(defect));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToBoolean(result.OutputData!["IsAnomaly"]).Should().BeTrue();
        Convert.ToSingle(result.OutputData["AnomalyScore"]).Should().BeGreaterThan(0.15f);

        var mask = result.OutputData["AnomalyMask"].Should().BeOfType<ImageWrapper>().Subject;
        Cv2.CountNonZero(mask.MatReadOnly).Should().BeGreaterThan(0);

        DisposeOutputs(result.OutputData);
        File.Delete(featureBankPath);
    }

    [Fact]
    public async Task ExecuteAsync_InferenceModeWithModelId_ShouldResolveFeatureBankFromCatalog()
    {
        var sut = new AnomalyDetectionOperator(Substitute.For<ILogger<AnomalyDetectionOperator>>());
        var tempDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"anomaly-catalog-{Guid.NewGuid():N}"));
        var featureBankPath = Path.Combine(tempDirectory.FullName, "sample_feature_bank.json");
        var catalogPath = Path.Combine(tempDirectory.FullName, "model_catalog.json");

        using var normalA = CreateUniformImage(new Scalar(80, 80, 80));
        using var normalB = CreateUniformImage(new Scalar(82, 82, 82));
        var bank = SimplePatchCoreDetector.BuildFeatureBank(
            new[] { normalA.GetMat(), normalB.GetMat() },
            new SimplePatchCoreOptions { PatchSize = 24, PatchStride = 12, CoresetRatio = 1.0 });
        SimplePatchCoreDetector.Save(featureBankPath, bank);

        await File.WriteAllTextAsync(catalogPath, $$"""
        {
          "models": [
            {
              "id": "patchcore_demo_bank",
              "type": "anomaly_feature_bank",
              "path": "{{featureBankPath.Replace("\\", "\\\\")}}",
              "version": "1.0.0"
            }
          ]
        }
        """);

        var op = new Operator("anomaly", OperatorType.AnomalyDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "inference", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ModelId", "patchcore_demo_bank", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ModelCatalogPath", catalogPath, "string"));
        op.AddParameter(TestHelpers.CreateParameter("Backbone", "simple_patchcore", "string"));
        op.AddParameter(TestHelpers.CreateParameter("PatchSize", 24, "int"));
        op.AddParameter(TestHelpers.CreateParameter("PatchStride", 12, "int"));
        op.AddParameter(TestHelpers.CreateParameter("CoresetRatio", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.15, "double"));

        using var defect = CreateUniformImage(new Scalar(80, 80, 80));
        var writable = defect.GetWritableMat();
        Cv2.Circle(writable, new Point(64, 64), 18, new Scalar(240, 240, 240), -1);

        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(defect));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        Convert.ToBoolean(result.OutputData!["IsAnomaly"]).Should().BeTrue();

        DisposeOutputs(result.OutputData);
        Directory.Delete(tempDirectory.FullName, recursive: true);
    }

    private static Operator CreateTrainOperator(string featureBankPath)
    {
        var op = new Operator("anomaly_train", OperatorType.AnomalyDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "train", "string"));
        op.AddParameter(TestHelpers.CreateParameter("SaveFeatureBankPath", featureBankPath, "file"));
        op.AddParameter(TestHelpers.CreateParameter("Backbone", "simple_patchcore", "string"));
        op.AddParameter(TestHelpers.CreateParameter("PatchSize", 24, "int"));
        op.AddParameter(TestHelpers.CreateParameter("PatchStride", 12, "int"));
        op.AddParameter(TestHelpers.CreateParameter("CoresetRatio", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.15, "double"));
        return op;
    }

    private static Operator CreateInferenceOperator(string featureBankPath)
    {
        var op = new Operator("anomaly_inference", OperatorType.AnomalyDetection, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "inference", "string"));
        op.AddParameter(TestHelpers.CreateParameter("FeatureBankPath", featureBankPath, "file"));
        op.AddParameter(TestHelpers.CreateParameter("Backbone", "simple_patchcore", "string"));
        op.AddParameter(TestHelpers.CreateParameter("PatchSize", 24, "int"));
        op.AddParameter(TestHelpers.CreateParameter("PatchStride", 12, "int"));
        op.AddParameter(TestHelpers.CreateParameter("CoresetRatio", 1.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.15, "double"));
        return op;
    }

    private static ImageWrapper CreateUniformImage(Scalar color)
    {
        return new ImageWrapper(new Mat(128, 128, MatType.CV_8UC3, color));
    }

    private static void DisposeOutputs(Dictionary<string, object>? outputData)
    {
        if (outputData == null)
        {
            return;
        }

        foreach (var image in outputData.Values.OfType<ImageWrapper>())
        {
            image.Dispose();
        }
    }
}
