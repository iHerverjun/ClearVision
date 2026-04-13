using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint6_Phase3")]
public class ParallelLineFindOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeParallelLineFind()
    {
        Assert.Equal(OperatorType.ParallelLineFind, CreateSut().OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithParallelEdgesAndClutter_ShouldReturnTrueRailPair()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AngleTolerance", 8.0 },
            { "MinLength", 40.0 },
            { "MinDistance", 5.0 },
            { "MaxDistance", 80.0 }
        });

        using var image = CreateLineImage(withClutter: true);
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, Convert.ToInt32(result.OutputData!["PairCount"]));
        Assert.InRange(Convert.ToDouble(result.OutputData["Distance"]), 20.0, 40.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithDisjointParallelSegments_ShouldRejectPair()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AngleTolerance", 5.0 },
            { "MinLength", 30.0 },
            { "MinDistance", 5.0 },
            { "MaxDistance", 80.0 }
        });

        using var image = CreateDisjointParallelImage();
        var result = await CreateSut().ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, Convert.ToInt32(result.OutputData!["PairCount"]));
    }

    [Fact]
    public void ValidateParameters_WithInvalidDistanceRange_ShouldReturnInvalid()
    {
        var validation = CreateSut().ValidateParameters(CreateOperator(new Dictionary<string, object>
        {
            { "MinDistance", 50.0 },
            { "MaxDistance", 10.0 }
        }));

        Assert.False(validation.IsValid);
    }

    private static ParallelLineFindOperator CreateSut() => new(Substitute.For<ILogger<ParallelLineFindOperator>>());

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ParallelLineFind", OperatorType.ParallelLineFind, 0, 0);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), key, key, string.Empty, "string", value));
            }
        }

        return op;
    }

    private static ImageWrapper CreateLineImage(bool withClutter)
    {
        var mat = new Mat(140, 200, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 35), new Point(180, 35), Scalar.White, 2);
        Cv2.Line(mat, new Point(20, 65), new Point(180, 65), Scalar.White, 2);
        if (withClutter)
        {
            Cv2.Line(mat, new Point(40, 10), new Point(40, 120), Scalar.White, 2);
            Cv2.Line(mat, new Point(160, 15), new Point(160, 125), Scalar.White, 2);
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateDisjointParallelImage()
    {
        var mat = new Mat(140, 200, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(10, 40), new Point(70, 40), Scalar.White, 2);
        Cv2.Line(mat, new Point(130, 75), new Point(190, 75), Scalar.White, 2);
        return new ImageWrapper(mat);
    }
}
