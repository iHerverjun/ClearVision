using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

public sealed class LawsTextureFilterOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeLawsTextureFilter()
    {
        var sut = new LawsTextureFilterOperator(Substitute.For<ILogger<LawsTextureFilterOperator>>());
        sut.OperatorType.Should().Be(OperatorType.LawsTextureFilter);
    }

    [Fact]
    public async Task ExecuteAsync_WithCheckerboard_ShouldReturnEnergyImageAndMeanEnergy()
    {
        var sut = new LawsTextureFilterOperator(Substitute.For<ILogger<LawsTextureFilterOperator>>());

        var op = new Operator("laws_texture", OperatorType.LawsTextureFilter, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("KernelCombo", "E5E5", dataType: "string"));
        op.AddParameter(TestHelpers.CreateParameter("SubtractLocalMean", false, dataType: "bool"));
        op.AddParameter(TestHelpers.CreateParameter("LocalMeanWindowSize", 15, dataType: "int"));
        op.AddParameter(TestHelpers.CreateParameter("EnergyWindowSize", 15, dataType: "int"));
        op.AddParameter(TestHelpers.CreateParameter("BorderType", 1, dataType: "int"));

        using var img = new ImageWrapper(BuildCheckerboard(128, 128, blockSize: 8));
        var inputs = TestHelpers.CreateImageInputs(img);

        var result = await sut.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData.Should().NotBeNull();
        var output = result.OutputData!;
        output.Should().ContainKey("FilteredImage");
        output.Should().ContainKey("EnergyImage");
        output.Should().ContainKey("MeanEnergy");

        var filtered = output["FilteredImage"].Should().BeOfType<ImageWrapper>().Subject;
        var energy = output["EnergyImage"].Should().BeOfType<ImageWrapper>().Subject;
        var meanEnergy = Convert.ToDouble(output["MeanEnergy"]);

        filtered.MatReadOnly.Empty().Should().BeFalse();
        energy.MatReadOnly.Empty().Should().BeFalse();
        energy.MatReadOnly.Type().Should().Be(MatType.CV_32FC1);
        meanEnergy.Should().BeGreaterThan(1e-5);

        // Cleanup output wrappers to avoid leaking pooled mats in test runs.
        filtered.Dispose();
        energy.Dispose();
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
