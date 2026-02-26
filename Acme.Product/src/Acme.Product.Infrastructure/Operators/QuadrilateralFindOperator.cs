using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
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

        using var edge = new Mat();
        Cv2.Canny(gray, edge, 60, 180);
        Cv2.FindContours(edge, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var quads = new List<(Point[] points, double area, Position center)>();

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

            var moments = Cv2.Moments(approx);
            var center = moments.M00 > 1e-9
                ? new Position(moments.M10 / moments.M00, moments.M01 / moments.M00)
                : new Position(0, 0);

            quads.Add((approx, area, center));
        }

        quads = quads.OrderByDescending(q => q.area).ToList();
        var primary = quads.FirstOrDefault();

        var resultImage = src.Clone();
        foreach (var quad in quads)
        {
            Cv2.Polylines(resultImage, new[] { quad.points }, true, new Scalar(0, 255, 0), 2);
        }

        var vertices = primary.points == null
            ? new List<Position>()
            : primary.points.Select(p => new Position(p.X, p.Y)).ToList();

        var output = new Dictionary<string, object>
        {
            { "Vertices", vertices },
            { "Count", quads.Count },
            { "Area", primary.area },
            { "Center", primary.center ?? new Position(0, 0) }
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
}

