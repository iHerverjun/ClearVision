// RectangleDetectionOperator.cs
// 矩形检测算子
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "矩形检测",
    Description = "Detects rectangular/quadrilateral objects from contours.",
    Category = "定位",
    IconName = "rectangle",
    Keywords = new[] { "rectangle", "quadrilateral", "box", "locate" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Rectangles", "Rectangles", PortDataType.Any)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("Center", "Center", PortDataType.Point)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OutputPort("Width", "Width", PortDataType.Float)]
[OutputPort("Height", "Height", PortDataType.Float)]
[OutputPort("NormalizedAngle", "Normalized Angle", PortDataType.Float)]
[OutputPort("LongSide", "Long Side", PortDataType.Float)]
[OutputPort("ShortSide", "Short Side", PortDataType.Float)]
[OperatorParam("MinArea", "Min Area", "int", DefaultValue = 100, Min = 0, Max = 100000000)]
[OperatorParam("MaxArea", "Max Area", "int", DefaultValue = 10000000, Min = 0, Max = 100000000)]
[OperatorParam("AngleTolerance", "Angle Tolerance", "double", DefaultValue = 15.0, Min = 0.0, Max = 90.0)]
[OperatorParam("ApproxEpsilon", "Approx Epsilon", "double", DefaultValue = 0.02, Min = 0.0001, Max = 1000.0)]
public class RectangleDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RectangleDetection;

    public RectangleDetectionOperator(ILogger<RectangleDetectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var minArea = GetIntParam(@operator, "MinArea", 100, 0, 100_000_000);
        var maxArea = GetIntParam(@operator, "MaxArea", 10_000_000, 0, 100_000_000);
        var angleTolerance = GetDoubleParam(@operator, "AngleTolerance", 15.0, 0.0, 90.0);
        var approxEpsilon = GetDoubleParam(@operator, "ApproxEpsilon", 0.02, 0.0001, 1000.0);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 60, 180);
        using var closed = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kernel);
        Cv2.FindContours(closed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var rectangles = new List<RectangleResult>();
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minArea || area > maxArea)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            var epsilon = approxEpsilon <= 1.0 ? perimeter * approxEpsilon : approxEpsilon;
            var approx = Cv2.ApproxPolyDP(contour, epsilon, true);
            if (approx.Length != 4 || !Cv2.IsContourConvex(approx))
            {
                continue;
            }

            if (!IsNearRightAngle(approx, angleTolerance))
            {
                continue;
            }

            var rect = Cv2.MinAreaRect(approx);
            var normalized = NormalizeRect(rect);
            rectangles.Add(new RectangleResult(rect, rect.Points(), area, normalized.Angle, normalized.LongSide, normalized.ShortSide));
        }

        rectangles = rectangles.OrderByDescending(rectangle => rectangle.Area).ToList();
        var resultImage = src.Clone();
        foreach (var rectangle in rectangles)
        {
            var pts = rectangle.Points.Select(point => new Point((int)Math.Round(point.X), (int)Math.Round(point.Y))).ToArray();
            Cv2.Polylines(resultImage, new[] { pts }, true, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, new Point((int)Math.Round(rectangle.Rect.Center.X), (int)Math.Round(rectangle.Rect.Center.Y)), 3, new Scalar(0, 0, 255), -1);
        }

        var payload = rectangles.Select(rectangle => new Dictionary<string, object>
        {
            { "CenterX", rectangle.Rect.Center.X },
            { "CenterY", rectangle.Rect.Center.Y },
            { "Width", rectangle.Rect.Size.Width },
            { "Height", rectangle.Rect.Size.Height },
            { "Angle", rectangle.Rect.Angle },
            { "NormalizedAngle", rectangle.NormalizedAngle },
            { "LongSide", rectangle.LongSide },
            { "ShortSide", rectangle.ShortSide },
            { "Area", rectangle.Area },
            { "Points", rectangle.Points.Select(point => new Dictionary<string, object> { { "X", point.X }, { "Y", point.Y } }).ToList() }
        }).ToList();

        var primary = rectangles.FirstOrDefault();
        var output = new Dictionary<string, object>
        {
            { "Rectangles", payload },
            { "Count", rectangles.Count },
            { "Center", primary != null ? new Position(primary.Rect.Center.X, primary.Rect.Center.Y) : new Position(0, 0) },
            { "Angle", primary?.Rect.Angle ?? 0f },
            { "Width", primary?.Rect.Size.Width ?? 0f },
            { "Height", primary?.Rect.Size.Height ?? 0f },
            { "NormalizedAngle", primary?.NormalizedAngle ?? 0.0 },
            { "LongSide", primary?.LongSide ?? 0.0 },
            { "ShortSide", primary?.ShortSide ?? 0.0 }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minArea = GetIntParam(@operator, "MinArea", 100);
        var maxArea = GetIntParam(@operator, "MaxArea", 10_000_000);
        if (minArea < 0 || maxArea <= 0 || minArea > maxArea)
        {
            return ValidationResult.Invalid("Invalid MinArea/MaxArea range");
        }

        var angleTolerance = GetDoubleParam(@operator, "AngleTolerance", 15.0);
        if (angleTolerance < 0 || angleTolerance > 90)
        {
            return ValidationResult.Invalid("AngleTolerance must be in [0, 90]");
        }

        return ValidationResult.Valid();
    }

    private static bool IsNearRightAngle(IReadOnlyList<Point> polygon, double toleranceDeg)
    {
        if (polygon.Count != 4)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            var p0 = polygon[(i + 3) % 4];
            var p1 = polygon[i];
            var p2 = polygon[(i + 1) % 4];

            var v1 = new Point2d(p0.X - p1.X, p0.Y - p1.Y);
            var v2 = new Point2d(p2.X - p1.X, p2.Y - p1.Y);
            var dot = v1.X * v2.X + v1.Y * v2.Y;
            var n1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            var n2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);
            if (n1 < 1e-9 || n2 < 1e-9)
            {
                return false;
            }

            var cos = Math.Clamp(dot / (n1 * n2), -1.0, 1.0);
            var angle = Math.Acos(cos) * 180.0 / Math.PI;
            if (Math.Abs(90.0 - angle) > toleranceDeg)
            {
                return false;
            }
        }

        return true;
    }

    private static (double Angle, double LongSide, double ShortSide) NormalizeRect(RotatedRect rect)
    {
        double width = rect.Size.Width;
        double height = rect.Size.Height;
        double angle = rect.Angle;

        if (height > width)
        {
            (width, height) = (height, width);
            angle += 90.0;
        }

        while (angle >= 90.0)
        {
            angle -= 180.0;
        }

        while (angle < -90.0)
        {
            angle += 180.0;
        }

        return (angle, width, height);
    }

    private sealed record RectangleResult(RotatedRect Rect, Point2f[] Points, double Area, double NormalizedAngle, double LongSide, double ShortSide);
}
