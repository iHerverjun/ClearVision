// QuadrilateralFindOperator.cs
// 四边形查找算子
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "四边形查找",
    Description = "Finds quadrilateral contours without right-angle constraints.",
    Category = "定位",
    IconName = "quadrilateral",
    Keywords = new[] { "quadrilateral", "polygon", "trapezoid" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Vertices", "Vertices", PortDataType.PointList)]
[OutputPort("OrderedVertices", "Ordered Vertices", PortDataType.PointList)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OutputPort("Area", "Area", PortDataType.Float)]
[OutputPort("Center", "Center", PortDataType.Point)]
[OperatorParam("MinArea", "Min Area", "int", DefaultValue = 100, Min = 0, Max = 100000000)]
[OperatorParam("MaxArea", "Max Area", "int", DefaultValue = 10000000, Min = 0, Max = 100000000)]
[OperatorParam("ApproxEpsilon", "Approx Epsilon", "double", DefaultValue = 0.02, Min = 0.0001, Max = 1000.0)]
[OperatorParam("ConvexOnly", "Convex Only", "bool", DefaultValue = false)]
public class QuadrilateralFindOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.QuadrilateralFind;

    public QuadrilateralFindOperator(ILogger<QuadrilateralFindOperator> logger) : base(logger)
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
        var approxEpsilon = GetDoubleParam(@operator, "ApproxEpsilon", 0.02, 0.0001, 1000.0);
        var convexOnly = GetBoolParam(@operator, "ConvexOnly", false);

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
        using var edge = new Mat();
        Cv2.Canny(blurred, edge, 60, 180);
        using var closed = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(edge, closed, MorphTypes.Close, kernel);
        Cv2.FindContours(closed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var quads = new List<(Point2f[] Points, Point2f[] OrderedPoints, double Area, Position Center)>();
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
            if (approx.Length != 4)
            {
                continue;
            }

            if (convexOnly && !Cv2.IsContourConvex(approx))
            {
                continue;
            }

            var refinedCorners = RefineCorners(gray, approx);
            var moments = Cv2.Moments(refinedCorners);
            var center = moments.M00 > 1e-9
                ? new Position(moments.M10 / moments.M00, moments.M01 / moments.M00)
                : new Position(0, 0);

            quads.Add((refinedCorners, OrderVertices(refinedCorners), area, center));
        }

        quads = quads.OrderByDescending(quad => quad.Area).ToList();
        var primary = quads.FirstOrDefault();

        var resultImage = src.Clone();
        foreach (var quad in quads)
        {
            var drawPoints = quad.OrderedPoints
                .Select(point => new Point((int)Math.Round(point.X), (int)Math.Round(point.Y)))
                .ToArray();
            Cv2.Polylines(resultImage, new[] { drawPoints }, true, new Scalar(0, 255, 0), 2);
        }

        var vertices = primary.Points == null ? new List<Position>() : primary.Points.Select(point => new Position(point.X, point.Y)).ToList();
        var orderedVertices = primary.OrderedPoints == null ? new List<Position>() : primary.OrderedPoints.Select(point => new Position(point.X, point.Y)).ToList();

        var output = new Dictionary<string, object>
        {
            { "Vertices", vertices },
            { "OrderedVertices", orderedVertices },
            { "Count", quads.Count },
            { "Area", primary.Area },
            { "Center", primary.Center ?? new Position(0, 0) }
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

        return ValidationResult.Valid();
    }

    private static Point2f[] RefineCorners(Mat gray, IReadOnlyList<Point> approx)
    {
        var corners = approx.Select(point => new Point2f(point.X, point.Y)).ToArray();
        if (corners.Length != 4)
        {
            return corners;
        }

        if (corners.Any(point => point.X < 1 || point.X >= gray.Cols - 1 || point.Y < 1 || point.Y >= gray.Rows - 1))
        {
            return corners;
        }

        Cv2.CornerSubPix(
            gray,
            corners,
            new Size(5, 5),
            new Size(-1, -1),
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01));
        return corners;
    }

    private static Point2f[] OrderVertices(Point2f[] points)
    {
        if (points.Length != 4)
        {
            return points;
        }

        var centroidX = points.Average(point => point.X);
        var centroidY = points.Average(point => point.Y);
        var ordered = points
            .Select(point => new { Point = point, Angle = Math.Atan2(point.Y - centroidY, point.X - centroidX) })
            .OrderBy(item => item.Angle)
            .Select(item => item.Point)
            .ToArray();

        if (SignedArea(ordered) < 0)
        {
            Array.Reverse(ordered);
        }

        var startIndex = Enumerable.Range(0, ordered.Length)
            .OrderBy(index => ordered[index].Y)
            .ThenBy(index => ordered[index].X)
            .First();

        return Enumerable.Range(0, ordered.Length)
            .Select(offset => ordered[(startIndex + offset) % ordered.Length])
            .ToArray();
    }

    private static double SignedArea(IReadOnlyList<Point2f> polygon)
    {
        double area = 0;
        for (var index = 0; index < polygon.Count; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5;
    }
}
