using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class ColorMeasurementOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeColorMeasurement()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.ColorMeasurement, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithReferenceColor_ShouldReturnDeltaE()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "ColorSpace", "Lab" },
            { "RoiX", 0 },
            { "RoiY", 0 },
            { "RoiW", 40 },
            { "RoiH", 40 }
        });

        using var image = CreateSolidColorImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        inputs["ReferenceColor"] = new Dictionary<string, object>
        {
            { "L", 0.0 },
            { "A", 0.0 },
            { "B", 0.0 }
        };

        var result = await sut.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToDouble(result.OutputData!["DeltaE"]) > 0.0);
        Assert.True(result.OutputData.ContainsKey("L"));
        Assert.True(result.OutputData.ContainsKey("H"));
    }

    [Fact]
    public void ValidateParameters_WithInvalidColorSpace_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "ColorSpace", "RGB" } });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static ColorMeasurementOperator CreateSut()
    {
        return new ColorMeasurementOperator(Substitute.For<ILogger<ColorMeasurementOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ColorMeasurement", OperatorType.ColorMeasurement, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateSolidColorImage()
    {
        var mat = new Mat(60, 60, MatType.CV_8UC3, new Scalar(30, 30, 200));
        return new ImageWrapper(mat);
    }
}
