using Acme.Product.Core.Entities;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

internal static class IndustrialMeasurementSceneFactory
{
    public static ImageWrapper CreateFilledVerticalStripeImage(
        int width,
        int height,
        double leftX,
        double rightX,
        double topY = 0,
        double bottomY = double.NaN,
        int supersample = 8)
    {
        bottomY = double.IsNaN(bottomY) ? height : bottomY;
        return CreateStripesImage(
            width,
            height,
            new[]
            {
                (leftX, rightX, topY, bottomY)
            },
            supersample);
    }

    public static ImageWrapper CreateStripesImage(
        int width,
        int height,
        IReadOnlyList<(double Left, double Right, double Top, double Bottom)> stripes,
        int supersample = 8)
    {
        var safeScale = Math.Max(2, supersample);
        using var hiRes = new Mat(height * safeScale, width * safeScale, MatType.CV_8UC1, Scalar.Black);

        foreach (var stripe in stripes)
        {
            var x = (int)Math.Round(stripe.Left * safeScale);
            var y = (int)Math.Round(stripe.Top * safeScale);
            var w = Math.Max(1, (int)Math.Round((stripe.Right - stripe.Left) * safeScale));
            var h = Math.Max(1, (int)Math.Round((stripe.Bottom - stripe.Top) * safeScale));
            Cv2.Rectangle(hiRes, new Rect(x, y, w, h), Scalar.White, -1, LineTypes.Link8);
        }

        return DownsampleToColor(hiRes, width, height);
    }

    public static ImageWrapper CreateLineImage(
        int width,
        int height,
        Point2d start,
        Point2d end,
        double thicknessPx,
        int supersample = 8)
    {
        var safeScale = Math.Max(2, supersample);
        using var hiRes = new Mat(height * safeScale, width * safeScale, MatType.CV_8UC1, Scalar.Black);
        var scaledStart = new Point((int)Math.Round(start.X * safeScale), (int)Math.Round(start.Y * safeScale));
        var scaledEnd = new Point((int)Math.Round(end.X * safeScale), (int)Math.Round(end.Y * safeScale));
        var thickness = Math.Max(1, (int)Math.Round(thicknessPx * safeScale));
        Cv2.Line(hiRes, scaledStart, scaledEnd, Scalar.White, thickness, LineTypes.AntiAlias);
        return DownsampleToColor(hiRes, width, height);
    }

    public static ImageWrapper CreateFilledCircleImage(
        int width,
        int height,
        Point2d center,
        double radius,
        int supersample = 8)
    {
        var safeScale = Math.Max(2, supersample);
        using var hiRes = new Mat(height * safeScale, width * safeScale, MatType.CV_8UC1, Scalar.Black);
        var scaledCenter = new Point((int)Math.Round(center.X * safeScale), (int)Math.Round(center.Y * safeScale));
        var scaledRadius = Math.Max(1, (int)Math.Round(radius * safeScale));
        Cv2.Circle(hiRes, scaledCenter, scaledRadius, Scalar.White, -1, LineTypes.AntiAlias);
        return DownsampleToColor(hiRes, width, height);
    }

    public static double DistancePointToLine(Point2d point, Point2d lineStart, Point2d lineEnd)
    {
        var a = lineEnd.Y - lineStart.Y;
        var b = lineStart.X - lineEnd.X;
        var c = lineEnd.X * lineStart.Y - lineStart.X * lineEnd.Y;
        var denominator = Math.Sqrt((a * a) + (b * b));
        if (denominator < 1e-9)
        {
            return 0.0;
        }

        return Math.Abs((a * point.X) + (b * point.Y) + c) / denominator;
    }

    private static ImageWrapper DownsampleToColor(Mat hiRes, int width, int height)
    {
        using var lowResGray = new Mat();
        Cv2.Resize(hiRes, lowResGray, new Size(width, height), 0, 0, InterpolationFlags.Area);
        var color = new Mat();
        Cv2.CvtColor(lowResGray, color, ColorConversionCodes.GRAY2BGR);
        return new ImageWrapper(color);
    }
}
