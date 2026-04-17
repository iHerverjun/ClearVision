using System.Reflection;
using Acme.Product.Infrastructure.ImageProcessing;
using FluentAssertions;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

public class StegerSubpixelEdgeDetectorTests
{
    [Fact]
    public void ComputeSubpixelPoint_WithAxisAlignedHessian_ShouldNotDropPointWhenGxyIsZero()
    {
        using var detector = new StegerSubpixelEdgeDetector
        {
            EdgeThreshold = 0.1,
            MaxOffset = 1.0
        };

        var point = InvokeComputeSubpixelPoint(
            detector,
            x: 40,
            y: 20,
            gx: 40.0,
            gy: 0.0,
            gxx: -80.0,
            gyy: -5.0,
            gxy: 0.0);

        point.Should().NotBeNull();
        point!.X.Should().BeApproximately(40.5, 1e-6);
        point.Y.Should().BeApproximately(20.0, 1e-6);
        Math.Abs(point.NormalX).Should().BeApproximately(1.0, 1e-6);
        Math.Abs(point.NormalY).Should().BeLessThan(1e-6);
    }

    [Fact]
    public void ComputeDerivatives_WithLargerSigma_ShouldSmoothGradientResponse()
    {
        using var image = CreateVerticalStepEdgeImage();
        using var smallSigmaDetector = new StegerSubpixelEdgeDetector { Sigma = 0.6 };
        using var largeSigmaDetector = new StegerSubpixelEdgeDetector { Sigma = 2.0 };
        using var smallDx = new Mat();
        using var smallDy = new Mat();
        using var smallDxx = new Mat();
        using var smallDyy = new Mat();
        using var smallDxy = new Mat();
        using var largeDx = new Mat();
        using var largeDy = new Mat();
        using var largeDxx = new Mat();
        using var largeDyy = new Mat();
        using var largeDxy = new Mat();

        InvokeComputeDerivatives(smallSigmaDetector, image, smallDx, smallDy, smallDxx, smallDyy, smallDxy);
        InvokeComputeDerivatives(largeSigmaDetector, image, largeDx, largeDy, largeDxx, largeDyy, largeDxy);

        var row = image.Rows / 2;
        var smallPeak = GetPeakAbsoluteValue(smallDx, row);
        var largePeak = GetPeakAbsoluteValue(largeDx, row);

        largePeak.Should().BeLessThan(smallPeak);
    }

    private static SubpixelEdgePoint? InvokeComputeSubpixelPoint(
        StegerSubpixelEdgeDetector detector,
        int x,
        int y,
        double gx,
        double gy,
        double gxx,
        double gyy,
        double gxy)
    {
        var method = typeof(StegerSubpixelEdgeDetector).GetMethod(
            "ComputeSubpixelPoint",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        return (SubpixelEdgePoint?)method!.Invoke(detector, new object[] { x, y, gx, gy, gxx, gyy, gxy });
    }

    private static void InvokeComputeDerivatives(
        StegerSubpixelEdgeDetector detector,
        Mat gray,
        Mat dx,
        Mat dy,
        Mat dxx,
        Mat dyy,
        Mat dxy)
    {
        var method = typeof(StegerSubpixelEdgeDetector).GetMethod(
            "ComputeDerivatives",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method!.Invoke(detector, new object[] { gray, dx, dy, dxx, dyy, dxy });
    }

    private static double GetPeakAbsoluteValue(Mat image, int row)
    {
        double peak = 0;
        for (var x = 0; x < image.Cols; x++)
        {
            peak = Math.Max(peak, Math.Abs(image.At<double>(row, x)));
        }

        return peak;
    }

    private static Mat CreateVerticalStepEdgeImage(int width = 96, int height = 96)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, Scalar.Black);
        var edgeX = width / 2;
        Cv2.Rectangle(image, new Rect(edgeX, 0, width - edgeX, height), Scalar.White, -1);
        return image;
    }
}
