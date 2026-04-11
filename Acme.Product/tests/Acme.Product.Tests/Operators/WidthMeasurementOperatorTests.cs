using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class WidthMeasurementOperatorTests
{
    private readonly WidthMeasurementOperator _operator;

    public WidthMeasurementOperatorTests()
    {
        _operator = new WidthMeasurementOperator(Substitute.For<ILogger<WidthMeasurementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeWidthMeasurement()
    {
        Assert.Equal(OperatorType.WidthMeasurement, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithManualLines_ShouldReturnExpectedWidth()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MeasureMode", "ManualLines" },
            { "NumSamples", 20 },
            { "MultiScanCount", 20 },
            { "RobustMode", true },
            { "OutlierSigmaK", 3.0 },
            { "MinValidSamples", 0 }
        });

        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(20, 20, 20, 120);
        inputs["Line2"] = new LineData(40, 20, 40, 120);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        var width = Convert.ToDouble(result.OutputData!["Width"]);
        Assert.InRange(width, 19.0, 21.0);
        Assert.True(result.OutputData.ContainsKey("MeanWidth"));
        Assert.True(result.OutputData.ContainsKey("P95Width"));
        Assert.True(result.OutputData.ContainsKey("StdDev"));
        Assert.True(result.OutputData.ContainsKey("ValidSampleRate"));
        Assert.Equal("OK", result.OutputData["StatusCode"]);
    }

    [Fact]
    public void ValidateParameters_WithInvalidNumSamples_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "NumSamples", 5 } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateParameters_WithInvalidOutlierSigma_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "OutlierSigmaK", 0.1 } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_WithTooHighMinValidSamples_ShouldReturnFailure()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MeasureMode", "ManualLines" },
            { "MinValidSamples", 200 },
            { "MultiScanCount", 24 }
        });

        using var image = TestHelpers.CreateTestImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["Line1"] = new LineData(20, 20, 20, 120);
        inputs["Line2"] = new LineData(40, 20, 40, 120);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
        Assert.Contains("MinValidSamples", result.ErrorMessage ?? string.Empty);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Width", OperatorType.WidthMeasurement, 0, 0);

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
