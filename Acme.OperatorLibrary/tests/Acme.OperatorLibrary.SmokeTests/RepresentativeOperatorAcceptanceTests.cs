using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Acme.OperatorLibrary.SmokeTests;

public class RepresentativeOperatorAcceptanceTests
{
    [Fact]
    public async Task MeanFilterOperator_ShouldHandleBoundaryKernelSizeAndReturnImage()
    {
        using var source = new Mat(64, 64, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(source, new Rect(8, 8, 20, 20), Scalar.White, -1);

        using var inputImage = new ImageWrapper(source.Clone());
        var op = CreateOperator(
            OperatorType.MeanFilter,
            ("KernelSize", 64), // out of range and even; runtime should clamp then force odd
            ("BorderType", 4));
        var executor = new MeanFilterOperator(NullLogger<MeanFilterOperator>.Instance);

        var result = await executor.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = inputImage });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.Equal(64, Assert.IsType<int>(result.OutputData!["Width"]));
        Assert.Equal(64, Assert.IsType<int>(result.OutputData!["Height"]));
    }

    [Fact]
    public async Task CaliperToolOperator_ShouldCoverSuccessAndMissingInputFailurePaths()
    {
        using var source = new Mat(80, 200, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(source, new Rect(70, 0, 60, 80), Scalar.White, -1);

        var executor = new CaliperToolOperator(NullLogger<CaliperToolOperator>.Instance);

        var successOperator = CreateOperator(
            OperatorType.CaliperTool,
            ("Direction", "Horizontal"),
            ("Polarity", "Both"),
            ("EdgeThreshold", 8.0),
            ("ExpectedCount", 1),
            ("MeasureMode", "edge_pairs"),
            ("PairDirection", "any"));

        using (var successInput = new ImageWrapper(source.Clone()))
        {
            var success = await executor.ExecuteAsync(successOperator, new Dictionary<string, object> { ["Image"] = successInput });
            Assert.True(success.IsSuccess);
            Assert.NotNull(success.OutputData);
            Assert.True(Assert.IsType<int>(success.OutputData!["PairCount"]) >= 1);
        }

        var failure = await executor.ExecuteAsync(successOperator, inputs: null);
        Assert.False(failure.IsSuccess);
        Assert.NotNull(failure.ErrorMessage);
    }

    [Fact]
    public void CameraCalibrationOperator_ShouldRejectInvalidModeInValidation()
    {
        var op = CreateOperator(
            OperatorType.CameraCalibration,
            ("BoardWidth", 9),
            ("BoardHeight", 6),
            ("SquareSize", 25.0),
            ("Mode", "BadMode"));
        var executor = new CameraCalibrationOperator(NullLogger<CameraCalibrationOperator>.Instance);

        var validation = executor.ValidateParameters(op);

        Assert.False(validation.IsValid);
        Assert.Contains("Mode", validation.Errors.FirstOrDefault() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CameraCalibrationOperator_ShouldReturnFailureWhenFolderDoesNotExist()
    {
        var missingFolder = Path.Combine(Path.GetTempPath(), "acme-oplib-missing-folder-" + Guid.NewGuid().ToString("N"));
        var op = CreateOperator(
            OperatorType.CameraCalibration,
            ("Mode", "FolderCalibration"),
            ("ImageFolder", missingFolder));
        var executor = new CameraCalibrationOperator(NullLogger<CameraCalibrationOperator>.Instance);

        var result = await executor.ExecuteAsync(op);

        Assert.False(result.IsSuccess);
        Assert.Contains("ImageFolder", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModbusCommunicationOperator_ShouldRejectOutOfRangePortInValidation()
    {
        var op = CreateOperator(
            OperatorType.ModbusCommunication,
            ("Protocol", "TCP"),
            ("Port", 0),
            ("SlaveId", 1),
            ("RegisterCount", 1));
        var executor = new ModbusCommunicationOperator(NullLogger<ModbusCommunicationOperator>.Instance);

        var validation = executor.ValidateParameters(op);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public void ModbusCommunicationOperator_ShouldRejectUnsupportedProtocolInValidation()
    {
        var op = CreateOperator(
            OperatorType.ModbusCommunication,
            ("Protocol", "TcpRtuBridge"),
            ("SlaveId", 1),
            ("RegisterAddress", 0),
            ("RegisterCount", 1),
            ("FunctionCode", "ReadHolding"));
        var executor = new ModbusCommunicationOperator(NullLogger<ModbusCommunicationOperator>.Instance);

        var validation = executor.ValidateParameters(op);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public async Task TryCatchOperator_ShouldPassInputThroughTryBranch()
    {
        var op = CreateOperator(
            OperatorType.TryCatch,
            ("EnableCatch", false),
            ("CatchOutputError", true),
            ("CatchOutputStackTrace", false));
        var executor = new TryCatchOperator(NullLogger<TryCatchOperator>.Instance);
        var payload = "package-acceptance";

        var result = await executor.ExecuteAsync(op, new Dictionary<string, object> { ["Input"] = payload });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        var outputValues = result.OutputData!.Values.ToList();
        Assert.Contains(payload, outputValues);
        if (result.OutputData.TryGetValue("HasError", out var hasError))
        {
            Assert.Equal(false, hasError);
        }
    }

    [Fact]
    public void DeepLearningOperator_ShouldRejectMissingModelPathInValidation()
    {
        var op = CreateOperator(
            OperatorType.DeepLearning,
            ("ModelPath", string.Empty),
            ("Confidence", 0.5));
        var executor = new DeepLearningOperator(NullLogger<DeepLearningOperator>.Instance);

        var validation = executor.ValidateParameters(op);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Errors);
    }

    [Fact]
    public async Task DeepLearningOperator_ShouldFailWhenModelPathIsNotProvidedAtRuntime()
    {
        using var source = new Mat(64, 64, MatType.CV_8UC3, Scalar.Black);
        using var inputImage = new ImageWrapper(source.Clone());

        var op = CreateOperator(
            OperatorType.DeepLearning,
            ("ModelPath", string.Empty),
            ("Confidence", 0.4),
            ("InputSize", 640),
            ("ModelVersion", "Auto"));
        var executor = new DeepLearningOperator(NullLogger<DeepLearningOperator>.Instance);

        var result = await executor.ExecuteAsync(op, new Dictionary<string, object> { ["Image"] = inputImage });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    private static Operator CreateOperator(OperatorType operatorType, params (string Name, object? Value)[] parameters)
    {
        var op = new Operator($"{operatorType}-acceptance", operatorType, 0, 0);
        foreach (var (name, value) in parameters)
        {
            op.AddParameter(new Parameter(
                id: Guid.NewGuid(),
                name: name,
                displayName: name,
                description: $"Acceptance parameter {name}",
                dataType: "object",
                defaultValue: value,
                isRequired: false));
        }

        return op;
    }
}
