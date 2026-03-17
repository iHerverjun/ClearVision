using FluentAssertions;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

public class RoiTrackerTests
{
    [Fact]
    public void TransformRoi_TranslationOnly_ShouldShiftRoi()
    {
        var baseRoi = new Rect(10, 20, 30, 40);
        var baseCenter = new Point2f(baseRoi.X + baseRoi.Width / 2f, baseRoi.Y + baseRoi.Height / 2f);
        var matchPosition = new Point2f(baseCenter.X + 100, baseCenter.Y + 50);

        var result = RoiTracker.TransformRoi(baseRoi, matchPosition, matchAngle: 0, matchScale: 1);

        result.X.Should().Be(baseRoi.X + 100);
        result.Y.Should().Be(baseRoi.Y + 50);
        result.Width.Should().Be(baseRoi.Width);
        result.Height.Should().Be(baseRoi.Height);
    }

    [Fact]
    public void TransformRoi_Rotate90_ShouldSwapWidthHeight()
    {
        var baseRoi = new Rect(100, 200, 40, 20);
        var center = new Point2f(baseRoi.X + baseRoi.Width / 2f, baseRoi.Y + baseRoi.Height / 2f);

        var result = RoiTracker.TransformRoi(baseRoi, center, matchAngle: 90, matchScale: 1);

        result.Width.Should().Be(baseRoi.Height);
        result.Height.Should().Be(baseRoi.Width);
        result.X.Should().Be(110);
        result.Y.Should().Be(190);
    }

    [Fact]
    public void TransformRoi_ScaleOnly_ShouldScaleAroundCenter()
    {
        var baseRoi = new Rect(10, 20, 30, 40);
        var center = new Point2f(baseRoi.X + baseRoi.Width / 2f, baseRoi.Y + baseRoi.Height / 2f);

        var result = RoiTracker.TransformRoi(baseRoi, center, matchAngle: 0, matchScale: 2);

        result.Width.Should().Be(baseRoi.Width * 2);
        result.Height.Should().Be(baseRoi.Height * 2);
        result.X.Should().Be(-5);
        result.Y.Should().Be(0);
    }

    [Fact]
    public void TransformRoi_TranslateRotateScale_ShouldPreserveCenter()
    {
        var baseRoi = new Rect(100, 200, 40, 20);
        var baseCenter = new Point2f(baseRoi.X + baseRoi.Width / 2f, baseRoi.Y + baseRoi.Height / 2f);
        var matchPosition = new Point2f(baseCenter.X + 50, baseCenter.Y + 25);

        var result = RoiTracker.TransformRoi(baseRoi, matchPosition, matchAngle: 90, matchScale: 2);

        result.Width.Should().Be(baseRoi.Height * 2);
        result.Height.Should().Be(baseRoi.Width * 2);

        var resultCenter = new Point2f(result.X + result.Width / 2f, result.Y + result.Height / 2f);
        resultCenter.X.Should().BeApproximately(matchPosition.X, 1.0f);
        resultCenter.Y.Should().BeApproximately(matchPosition.Y, 1.0f);
    }
}

