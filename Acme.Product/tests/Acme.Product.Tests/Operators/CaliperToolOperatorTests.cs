using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class CaliperToolOperatorTests
{
    private readonly CaliperToolOperator _operator;

    public CaliperToolOperatorTests()
    {
        _operator = new CaliperToolOperator(Substitute.For<ILogger<CaliperToolOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeCaliperTool()
    {
        Assert.Equal(OperatorType.CaliperTool, _operator.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleEdgeImage_ShouldReturnSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", false }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Width"));
        Assert.True(result.OutputData.ContainsKey("PairCount"));
    }

    [Fact]
    public async Task ExecuteAsync_WithSubpixelEnabled_ShouldReturnSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", true }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Width"));
        Assert.True(result.OutputData.ContainsKey("PairCount"));
    }

    [Fact]
    public async Task ExecuteAsync_WithSubpixelZernike_ShouldReturnSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "Polarity", "Both" },
            { "EdgeThreshold", 10.0 },
            { "ExpectedCount", 1 },
            { "SubpixelAccuracy", true },
            { "SubPixelMode", "zernike" }
        });

        using var image = CreateCaliperImage();
        var inputs = TestHelpers.CreateImageInputs(image);

        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(result.OutputData!.ContainsKey("Width"));
        Assert.True(result.OutputData.ContainsKey("PairCount"));
    }

    [Fact]
    public void ValidateParameters_WithInvalidDirection_ShouldReturnInvalid()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Direction", "Diagonal" } });

        var validation = _operator.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("Caliper", OperatorType.CaliperTool, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateCaliperImage()
    {
        var mat = new Mat(120, 220, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(90, 10, 40, 100), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
}
