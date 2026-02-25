using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

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

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 60, 180);

        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

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
            var points = rect.Points();
            rectangles.Add(new RectangleResult(rect, points, area));
        }

        rectangles = rectangles.OrderByDescending(r => r.Area).ToList();

        var resultImage = src.Clone();
        foreach (var rectangle in rectangles)
        {
            var pts = rectangle.Points.Select(p => new Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
            Cv2.Polylines(resultImage, new[] { pts }, true, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, new Point((int)Math.Round(rectangle.Rect.Center.X), (int)Math.Round(rectangle.Rect.Center.Y)), 3, new Scalar(0, 0, 255), -1);
        }

        var rectanglePayload = rectangles.Select(r => new Dictionary<string, object>
        {
            { "CenterX", r.Rect.Center.X },
            { "CenterY", r.Rect.Center.Y },
            { "Width", r.Rect.Size.Width },
            { "Height", r.Rect.Size.Height },
            { "Angle", r.Rect.Angle },
            { "Area", r.Area },
            { "Points", r.Points.Select(p => new Dictionary<string, object> { { "X", p.X }, { "Y", p.Y } }).ToList() }
        }).ToList();

        var primary = rectangles.FirstOrDefault();
        var output = new Dictionary<string, object>
        {
            { "Rectangles", rectanglePayload },
            { "Count", rectangles.Count },
            { "Center", primary != null ? new Position(primary.Rect.Center.X, primary.Rect.Center.Y) : new Position(0, 0) },
            { "Angle", primary?.Rect.Angle ?? 0f },
            { "Width", primary?.Rect.Size.Width ?? 0f },
            { "Height", primary?.Rect.Size.Height ?? 0f }
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

    private sealed record RectangleResult(RotatedRect Rect, Point2f[] Points, double Area);
}
