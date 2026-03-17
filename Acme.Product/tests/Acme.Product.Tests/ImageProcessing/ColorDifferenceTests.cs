using Acme.Product.Infrastructure.ImageProcessing;
using FluentAssertions;
using Xunit;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

public sealed class ColorDifferenceTests
{
    [Theory]
    // Reference values from Sharma et al. (CIEDE2000 supplementary test data)
    [InlineData(50.0000, 2.6772, -79.7751, 50.0000, 0.0000, -82.7485, 2.0425)]
    [InlineData(50.0000, 3.1571, -77.2803, 50.0000, 0.0000, -82.7485, 2.8615)]
    [InlineData(50.0000, 2.8361, -74.0200, 50.0000, 0.0000, -82.7485, 3.4412)]
    [InlineData(50.0000, -1.3802, -84.2814, 50.0000, 0.0000, -82.7485, 1.0000)]
    [InlineData(50.0000, -1.1848, -84.8006, 50.0000, 0.0000, -82.7485, 1.0000)]
    public void DeltaE00_ShouldMatchReference(
        double l1, double a1, double b1,
        double l2, double a2, double b2,
        double expected)
    {
        var d = ColorDifference.DeltaE00(new CieLab(l1, a1, b1), new CieLab(l2, a2, b2));
        d.Should().BeApproximately(expected, 1e-4);
    }

    [Fact]
    public void DeltaE76_ShouldBeZeroForSameColor()
    {
        var c = new CieLab(20, 10, -5);
        ColorDifference.DeltaE76(c, c).Should().BeApproximately(0.0, 1e-12);
    }
}

