using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

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
    public async Task ExecuteAsync_WithJsonFile_ShouldLoadValues()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"calibration_{Guid.NewGuid():N}.json");
        try
        {
            var json = "{" +
                       "\"TransformMatrix\":[[1,0,0],[0,1,0],[0,0,1]]," +
                       "\"CameraMatrix\":[[1000,0,320],[0,1000,240],[0,0,1]]," +
                       "\"DistCoeffs\":[0.1,0.01,0,0,0]," +
                       "\"PixelSize\":0.02" +
                       "}";
            File.WriteAllText(tempFile, json);

            var op = CreateOperator(new Dictionary<string, object>
            {
                { "FilePath", tempFile },
                { "FileFormat", "JSON" }
            });

            var result = await _operator.ExecuteAsync(op, null);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.OutputData);
            Assert.Equal(0.02, Convert.ToDouble(result.OutputData!["PixelSize"]), 6);
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
            { "FilePath", Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json") },
            { "FileFormat", "JSON" }
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
}
