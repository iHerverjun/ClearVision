using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "测量",
    Description = "两点/水平/垂直距离测量，支持参数坐标与 PointA/PointB 输入。",
    Category = "检测",
    IconName = "measure",
    Keywords = new[] { "测量", "距离", "长度", "Measure", "Distance", "Length" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = false)]
[InputPort("PointA", "起点", PortDataType.Point, IsRequired = false)]
[InputPort("PointB", "终点", PortDataType.Point, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Distance", "测量距离", PortDataType.Float)]
[OperatorParam("X1", "起点X", "int", DefaultValue = 0)]
[OperatorParam("Y1", "起点Y", "int", DefaultValue = 0)]
[OperatorParam("X2", "终点X", "int", DefaultValue = 100)]
[OperatorParam("Y2", "终点Y", "int", DefaultValue = 100)]
[OperatorParam("MeasureType", "测量类型", "enum", DefaultValue = "PointToPoint", Options = new[] { "PointToPoint|点到点", "Horizontal|水平", "Vertical|垂直" })]
public class MeasureDistanceOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.Measurement;

    public MeasureDistanceOperator(ILogger<MeasureDistanceOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var measureType = GetStringParam(@operator, "MeasureType", "PointToPoint");
        var normalizedType = measureType.Trim().ToLowerInvariant();

        if (inputs != null &&
            inputs.TryGetValue("PointA", out var pointAObj) &&
            inputs.TryGetValue("PointB", out var pointBObj) &&
            TryParsePoint(pointAObj, out var pointA) &&
            TryParsePoint(pointBObj, out var pointB))
        {
            return Task.FromResult(BuildPointInputResult(pointA, pointB, measureType));
        }

        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像或 PointA/PointB"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("输入图像无效"));
        }

        var x1 = GetIntParam(@operator, "X1", 0);
        var y1 = GetIntParam(@operator, "Y1", 0);
        var x2 = GetIntParam(@operator, "X2", 100);
        var y2 = GetIntParam(@operator, "Y2", 100);

        var p1 = new Point(x1, y1);
        var p2 = new Point(x2, y2);
        var resultImage = src.Clone();

        double distance;
        switch (normalizedType)
        {
            case "pointtopoint":
                if (x1 == x2 && y1 == y2)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Start and end points are identical"));
                }

                distance = Distance(p1, p2);
                DrawLineDistance(resultImage, p1, p2, $"{distance:F2}px");
                break;

            case "horizontal":
                distance = Math.Abs(x2 - x1);
                if (distance < 1e-9)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Horizontal distance is zero"));
                }

                p2 = new Point(x2, y1);
                DrawLineDistance(resultImage, p1, p2, $"H: {distance:F2}px");
                break;

            case "vertical":
                distance = Math.Abs(y2 - y1);
                if (distance < 1e-9)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Vertical distance is zero"));
                }

                p2 = new Point(x1, y2);
                DrawLineDistance(resultImage, p1, p2, $"V: {distance:F2}px");
                break;

            default:
                return Task.FromResult(OperatorExecutionOutput.Failure($"Unsupported measure type: {measureType}"));
        }

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Distance", distance },
            { "X1", x1 },
            { "Y1", y1 },
            { "X2", p2.X },
            { "Y2", p2.Y },
            { "MeasureType", measureType },
            { "DeltaX", p2.X - x1 },
            { "DeltaY", p2.Y - y1 },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        });

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var measureType = GetStringParam(@operator, "MeasureType", "PointToPoint").Trim();
        var validTypes = new[] { "PointToPoint", "Horizontal", "Vertical" };
        if (!validTypes.Contains(measureType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Unsupported measure type: {measureType}");
        }

        return ValidationResult.Valid();
    }

    private static OperatorExecutionOutput BuildPointInputResult(Point pointA, Point pointB, string measureType)
    {
        if (pointA.X == pointB.X && pointA.Y == pointB.Y)
        {
            return OperatorExecutionOutput.Failure("[DegenerateGeometry] PointA and PointB are identical");
        }

        var distance = Distance(pointA, pointB);
        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Distance", distance },
            { "X1", pointA.X },
            { "Y1", pointA.Y },
            { "X2", pointB.X },
            { "Y2", pointB.Y },
            { "MeasureType", measureType },
            { "DeltaX", pointB.X - pointA.X },
            { "DeltaY", pointB.Y - pointA.Y },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        });
    }

    private static void DrawLineDistance(Mat image, Point p1, Point p2, string label)
    {
        Cv2.Line(image, p1, p2, new Scalar(0, 255, 0), 2);
        Cv2.Circle(image, p1, 5, new Scalar(255, 0, 0), -1);
        Cv2.Circle(image, p2, 5, new Scalar(255, 0, 0), -1);
        var textPoint = new Point((p1.X + p2.X) / 2 + 6, (p1.Y + p2.Y) / 2 - 6);
        Cv2.PutText(image, label, textPoint, HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
    }

    private static double Distance(Point p1, Point p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool TryParsePoint(object? obj, out Point point)
    {
        point = default;
        if (obj == null)
        {
            return false;
        }

        switch (obj)
        {
            case Point p:
                point = p;
                return true;
            case Point2f p2f:
                point = new Point((int)Math.Round(p2f.X), (int)Math.Round(p2f.Y));
                return true;
            case Point2d p2d:
                point = new Point((int)Math.Round(p2d.X), (int)Math.Round(p2d.Y));
                return true;
            case Acme.Product.Core.ValueObjects.Position pos:
                point = new Point((int)Math.Round(pos.X), (int)Math.Round(pos.Y));
                return true;
        }

        var str = obj.ToString()?.Trim('(', ')', '[', ']', ' ');
        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }

        var parts = str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!double.TryParse(parts[0], out var x) || !double.TryParse(parts[1], out var y))
        {
            return false;
        }

        point = new Point((int)Math.Round(x), (int)Math.Round(y));
        return true;
    }
}
