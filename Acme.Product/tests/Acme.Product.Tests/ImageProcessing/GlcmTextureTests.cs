using Acme.Product.Infrastructure.ImageProcessing;
using FluentAssertions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

public sealed class GlcmTextureTests
{
    [Fact]
    public void Compute_OnConstantImage_ShouldHaveZeroContrastAndLowEntropy()
    {
        using var constant = new Mat(128, 128, MatType.CV_8UC1, Scalar.All(128));

        var (mean, per) = GlcmTexture.Compute(constant, levels: 16, distance: 1);

        mean.Contrast.Should().BeApproximately(0.0, 1e-12);
        mean.Homogeneity.Should().BeApproximately(1.0, 1e-12);
        mean.Energy.Should().BeApproximately(1.0, 1e-12);
        mean.Entropy.Should().BeLessThan(1e-8);
        per.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compute_OnCheckerboard_ShouldHaveHigherContrastThanConstant()
    {
        using var constant = new Mat(128, 128, MatType.CV_8UC1, Scalar.All(128));
        using var checker = BuildCheckerboard(128, 128, blockSize: 8);

        var (c0, _) = GlcmTexture.Compute(constant, levels: 16, distance: 1);
        var (c1, _) = GlcmTexture.Compute(checker, levels: 16, distance: 1);

        c1.Contrast.Should().BeGreaterThan(c0.Contrast + 0.1);
        c1.Energy.Should().BeLessThan(0.8);
        c1.Entropy.Should().BeGreaterThan(0.01);
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

