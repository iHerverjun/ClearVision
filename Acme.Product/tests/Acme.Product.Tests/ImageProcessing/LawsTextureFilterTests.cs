using Acme.Product.Infrastructure.ImageProcessing;
using FluentAssertions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

public sealed class LawsTextureFilterTests
{
    [Fact]
    public void Apply_AndComputeEnergy_OnConstantVsCheckerboard_ShouldShowHigherEnergyForTexture()
    {
        using var constant = new Mat(128, 128, MatType.CV_8UC1, Scalar.All(128));
        using var checker = BuildCheckerboard(128, 128, blockSize: 8);

        using var filteredConst = LawsTextureFilter.Apply(constant, kernelCombo: "E5E5", subtractLocalMean: false);
        using var energyConst = LawsTextureFilter.ComputeEnergy(filteredConst, windowSize: 15);

        using var filteredChk = LawsTextureFilter.Apply(checker, kernelCombo: "E5E5", subtractLocalMean: false);
        using var energyChk = LawsTextureFilter.ComputeEnergy(filteredChk, windowSize: 15);

        filteredConst.Type().Should().Be(MatType.CV_32FC1);
        energyConst.Type().Should().Be(MatType.CV_32FC1);
        filteredConst.Size().Should().Be(constant.Size());
        energyConst.Size().Should().Be(constant.Size());

        var meanConst = Cv2.Mean(energyConst).Val0;
        var meanChk = Cv2.Mean(energyChk).Val0;

        // Constant image should be near-zero for zero-sum kernels like E5E5.
        meanConst.Should().BeLessThan(1e-8);
        // Checkerboard should have noticeably higher energy.
        meanChk.Should().BeGreaterThan(1e-5);
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

