using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Calibration;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class CalibrationLoaderOperatorTests
{
    private readonly CalibrationLoaderOperator _operator;

    public CalibrationLoaderOperatorTests()
    {
        _operator = new CalibrationLoaderOperator(Substitute.For<ILogger<CalibrationLoaderOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeCalibrationLoader()
    {
        Assert.Equal(OperatorType.CalibrationLoader, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidV2JsonFile_ShouldLoadTypedOutputs()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"calibration_v2_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, CreateAcceptedV2BundleJson());

            var op = CreateOperator(new Dictionary<string, object>
            {
                { "FilePath", tempFile }
            });

            var result = await _operator.ExecuteAsync(op, null);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputData);
            Assert.True((bool)result.OutputData!["Accepted"]);
            Assert.IsType<CalibrationBundleV2>(result.OutputData["CalibrationBundle"]);
            Assert.IsType<string>(result.OutputData["CalibrationData"]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidSchema_ShouldReturnFailure()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"calibration_invalid_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempFile, """
                                     {
                                       "schemaVersion": 1,
                                       "calibrationKind": "planarTransform2D"
                                     }
                                     """);

            var op = CreateOperator(new Dictionary<string, object>
            {
                { "FilePath", tempFile }
            });

            var result = await _operator.ExecuteAsync(op, null);

            Assert.False(result.IsSuccess);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingFile_ShouldReturnFailure()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "FilePath", Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json") }
        });

        var result = await _operator.ExecuteAsync(op, null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateParameters_WithEmptyPath_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "FilePath", string.Empty } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("CalibrationLoader", OperatorType.CalibrationLoader, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static string CreateAcceptedV2BundleJson()
    {
        return """
               {
                 "schemaVersion": 2,
                 "calibrationKind": "cameraIntrinsics",
                 "transformModel": "none",
                 "sourceFrame": "image",
                 "targetFrame": "imageUndistorted",
                 "unit": "mm",
                 "imageSize": {
                   "width": 320,
                   "height": 240
                 },
                 "intrinsics": {
                   "cameraMatrix": [
                     [500.0, 0.0, 160.0],
                     [0.0, 500.0, 120.0],
                     [0.0, 0.0, 1.0]
                   ]
                 },
                 "distortion": {
                   "model": "brownConrady",
                   "coefficients": [0.1, 0.01, 0.0, 0.0, 0.0]
                 },
                 "quality": {
                   "accepted": true,
                   "meanError": 0.11,
                   "maxError": 0.23,
                   "inlierCount": 24,
                   "totalSampleCount": 24,
                   "diagnostics": []
                 },
                 "producerOperator": "CalibrationLoaderOperatorTests"
               }
               """;
    }
}
