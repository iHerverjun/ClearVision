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
        var sut = CreateSut();
        Assert.Equal(OperatorType.ParallelLineFind, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithTwoParallelEdges_ShouldReturnPair()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "AngleTolerance", 8.0 },
            { "MinLength", 40.0 },
            { "MinDistance", 5.0 },
            { "MaxDistance", 80.0 }
        });

        using var image = CreateLineImage();
        var result = await sut.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);
        Assert.True(Convert.ToInt32(result.OutputData!["PairCount"]) >= 1);
    }

    [Fact]
    public void ValidateParameters_WithInvalidDistanceRange_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinDistance", 50.0 },
            { "MaxDistance", 10.0 }
        });

        Assert.False(sut.ValidateParameters(op).IsValid);
    }

    private static ParallelLineFindOperator CreateSut()
    {
        return new ParallelLineFindOperator(Substitute.For<ILogger<ParallelLineFindOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("ParallelLineFind", OperatorType.ParallelLineFind, 0, 0);
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), k, k, string.Empty, "string", v));
            }
        }

        return op;
    }

    private static ImageWrapper CreateLineImage()
    {
        var mat = new Mat(120, 180, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 30), new Point(160, 30), Scalar.White, 2);
        Cv2.Line(mat, new Point(20, 60), new Point(160, 60), Scalar.White, 2);
        return new ImageWrapper(mat);
    }
}

