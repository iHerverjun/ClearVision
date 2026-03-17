using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.AI.Anomaly;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Acme.Product.Tests.Integration;

public sealed class Stage3_EndToEndDemoIntegrationTests
{
    [Fact]
    public async Task Demo_Ai3dFusionPipeline_ShouldProduceNgDecision()
    {
        using var segmentationInput = new ImageWrapper(Cv2.ImRead(ResolveTestDataPath(@"model_test_suite\identity_2x2\input.png"), ImreadModes.Color));
        using var normalA = CreateUniformImage(new Scalar(90, 90, 90));
        using var normalB = CreateUniformImage(new Scalar(92, 92, 92));
        using var defect = CreateUniformImage(new Scalar(90, 90, 90));
        var defectMat = defect.GetWritableMat();
        Cv2.Rectangle(defectMat, new Rect(40, 40, 28, 28), new Scalar(250, 250, 250), -1);

        var featureBankPath = Path.Combine(Path.GetTempPath(), $"demo-feature-bank-{Guid.NewGuid():N}.json");
        var bank = SimplePatchCoreDetector.BuildFeatureBank(
            new[] { normalA.GetMat(), normalB.GetMat() },
            new SimplePatchCoreOptions { PatchSize = 24, PatchStride = 12, CoresetRatio = 1.0 });
        SimplePatchCoreDetector.Save(featureBankPath, bank);

        var segmentationOperator = new SemanticSegmentationOperator(NullLogger<SemanticSegmentationOperator>.Instance);
        var anomalyOperator = new AnomalyDetectionOperator(NullLogger<AnomalyDetectionOperator>.Instance);
        var ransacOperator = new RansacPlaneSegmentationOperator(NullLogger<RansacPlaneSegmentationOperator>.Instance);

        var segmentationOp = new Operator("seg", OperatorType.SemanticSegmentation, 0, 0);
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ModelId", "semantic_identity_2x2", "string"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ModelCatalogPath", ResolveRepoPath("models/model_catalog.json"), "file"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ModelPath", string.Empty, "file"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("InputSize", "512,512", "string"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("NumClasses", 21, "int"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ClassNames", string.Empty, "string"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ExecutionProvider", "cpu", "string"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ScaleToUnitRange", true, "bool"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("ChannelOrder", "RGB", "string"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("Mean", "0,0,0", "string"));
        segmentationOp.AddParameter(TestHelpers.CreateParameter("Std", "1,1,1", "string"));

        var anomalyOp = new Operator("anomaly", OperatorType.AnomalyDetection, 0, 0);
        anomalyOp.AddParameter(TestHelpers.CreateParameter("Mode", "inference", "string"));
        anomalyOp.AddParameter(TestHelpers.CreateParameter("FeatureBankPath", featureBankPath, "file"));
        anomalyOp.AddParameter(TestHelpers.CreateParameter("Backbone", "simple_patchcore", "string"));
        anomalyOp.AddParameter(TestHelpers.CreateParameter("PatchSize", 24, "int"));
        anomalyOp.AddParameter(TestHelpers.CreateParameter("PatchStride", 12, "int"));
        anomalyOp.AddParameter(TestHelpers.CreateParameter("CoresetRatio", 1.0, "double"));
        anomalyOp.AddParameter(TestHelpers.CreateParameter("Threshold", 0.15, "double"));

        var ransacOp = new Operator("ransac", OperatorType.RansacPlaneSegmentation, 0, 0);
        ransacOp.AddParameter(TestHelpers.CreateParameter("DistanceThreshold", 0.002, "double"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("MaxIterations", 300, "int"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("MinInliers", 500, "int"));

        var segmentationResult = await segmentationOperator.ExecuteAsync(segmentationOp, TestHelpers.CreateImageInputs(segmentationInput));
        segmentationResult.IsSuccess.Should().BeTrue(segmentationResult.ErrorMessage);

        var anomalyResult = await anomalyOperator.ExecuteAsync(anomalyOp, TestHelpers.CreateImageInputs(defect));
        anomalyResult.IsSuccess.Should().BeTrue(anomalyResult.ErrorMessage);

        var generator = new SyntheticPointCloudGenerator(seed: 5101);
        using var plane = generator.GeneratePlane(
            center: Vector3.Zero,
            normal: Vector3.UnitZ,
            size: (1.0f, 1.0f),
            density: 1800,
            noise: 0.0005f,
            includeColors: false,
            includeNormals: false,
            outlierRatio: 0.0f);

        var ransacResult = await ransacOperator.ExecuteAsync(ransacOp, new Dictionary<string, object> { ["PointCloud"] = plane });
        ransacResult.IsSuccess.Should().BeTrue(ransacResult.ErrorMessage);

        var presentClasses = segmentationResult.OutputData!["PresentClasses"].Should().BeOfType<string[]>().Subject;
        var isAnomaly = Convert.ToBoolean(anomalyResult.OutputData!["IsAnomaly"]);
        var planeRatio = Convert.ToDouble(ransacResult.OutputData!["InlierRatio"]);
        var finalDecision = presentClasses.Length > 0 && (!isAnomaly && planeRatio > 0.8) ? "OK" : "NG";

        finalDecision.Should().Be("NG");

        DisposeImageOutputs(segmentationResult.OutputData);
        DisposeImageOutputs(anomalyResult.OutputData);
        File.Delete(featureBankPath);
    }

    private static ImageWrapper CreateUniformImage(Scalar color)
    {
        return new ImageWrapper(new Mat(128, 128, MatType.CV_8UC3, color));
    }

    private static void DisposeImageOutputs(Dictionary<string, object>? outputData)
    {
        if (outputData == null)
        {
            return;
        }

        foreach (var image in outputData.Values.OfType<ImageWrapper>())
        {
            image.Dispose();
        }

        if (outputData.TryGetValue("ClassMasks", out var classMasksObj) && classMasksObj is Dictionary<string, object> classMasks)
        {
            foreach (var image in classMasks.Values.OfType<ImageWrapper>())
            {
                image.Dispose();
            }
        }
    }

    private static string ResolveTestDataPath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.Name.Equals("Acme.Product", StringComparison.OrdinalIgnoreCase))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Failed to resolve Acme.Product root.");
        }

        return Path.Combine(dir.FullName, "tests", "TestData", relativePath);
    }

    private static string ResolveRepoPath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.Name.Equals("ClearVision", StringComparison.OrdinalIgnoreCase))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new DirectoryNotFoundException("Failed to resolve repository root.");
        }

        return Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
