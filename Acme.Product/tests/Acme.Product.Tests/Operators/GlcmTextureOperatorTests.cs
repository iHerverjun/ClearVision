using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public sealed class GlcmTextureOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeGlcmTexture()
    {
        var sut = new GlcmTextureOperator(Substitute.For<ILogger<GlcmTextureOperator>>());
        sut.OperatorType.Should().Be(OperatorType.GlcmTexture);
    }

    [Fact]
    public async Task ExecuteAsync_WithCheckerboard_ShouldReturnTextureFeatures()
    {
        var sut = new GlcmTextureOperator(Substitute.For<ILogger<GlcmTextureOperator>>());

        var op = new Operator("glcm", OperatorType.GlcmTexture, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Levels", 16, dataType: "int"));
        op.AddParameter(TestHelpers.CreateParameter("Distance", 1, dataType: "int"));
        op.AddParameter(TestHelpers.CreateParameter("DirectionsDeg", "0,45,90,135", dataType: "string"));
        op.AddParameter(TestHelpers.CreateParameter("Symmetric", true, dataType: "bool"));
        op.AddParameter(TestHelpers.CreateParameter("Normalize", true, dataType: "bool"));

        using var img = new ImageWrapper(BuildCheckerboard(128, 128, blockSize: 8));
        var inputs = TestHelpers.CreateImageInputs(img);

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        var output = result.OutputData!;

        Convert.ToDouble(output["Contrast"]).Should().BeGreaterThan(0.1);
        Convert.ToDouble(output["Energy"]).Should().BeLessThan(0.8);
        Convert.ToDouble(output["Entropy"]).Should().BeGreaterThan(0.01);
        output.Should().ContainKey("PerDirection");
    }

    private static Mat BuildCheckerboard(int width, int height, int blockSize)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1);
        var idx = mat.GetGenericIndexer<byte>();
        for (var y = 0; y < height; y++)
        {
            var by = (y / blockSize) & 1;
            for (var x = 0; x < width; x++)
            {
                var bx = (x / blockSize) & 1;
                idx[y, x] = (byte)(((bx ^ by) == 0) ? 0 : 255);
            }
        }
        return mat;
    }
}

