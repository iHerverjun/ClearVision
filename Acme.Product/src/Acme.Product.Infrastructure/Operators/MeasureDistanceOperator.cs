using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
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
            TryParsePoint(pointAObj, out var pointA, out var sigmaA) &&
            TryParsePoint(pointBObj, out var pointB, out var sigmaB))
        {
            return Task.FromResult(BuildPointInputResult(pointA, sigmaA, pointB, sigmaB, measureType));
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

        var p1 = new Position(x1, y1);
        var p2 = new Position(x2, y2);
        var resultImage = src.Clone();

        if (!TryMeasure(p1, p2, normalizedType, out var distance, out var drawnEndPoint, out var label, out var error))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(error ?? $"Unsupported measure type: {measureType}"));
        }

        var uncertaintyPx = ComputeDistanceUncertaintyPx(normalizedType, 0.5, 0.5);
        DrawLineDistance(resultImage, p1, drawnEndPoint, label);
        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Distance", distance },
            { "X1", p1.X },
            { "Y1", p1.Y },
            { "X2", drawnEndPoint.X },
            { "Y2", drawnEndPoint.Y },
            { "MeasureType", measureType },
            { "DeltaX", drawnEndPoint.X - p1.X },
            { "DeltaY", drawnEndPoint.Y - p1.Y },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", ComputeConfidence(uncertaintyPx) },
            { "UncertaintyPx", uncertaintyPx }
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

    private static OperatorExecutionOutput BuildPointInputResult(
        Position pointA,
        double pointASigmaPx,
        Position pointB,
        double pointBSigmaPx,
        string measureType)
    {
        var normalizedType = measureType.Trim().ToLowerInvariant();
        if (!TryMeasure(pointA, pointB, normalizedType, out var distance, out var resolvedEndPoint, out _, out var error))
        {
            return OperatorExecutionOutput.Failure(error ?? "Unsupported measure type");
        }

        var uncertaintyPx = ComputeDistanceUncertaintyPx(normalizedType, pointASigmaPx, pointBSigmaPx);
        return OperatorExecutionOutput.Success(new Dictionary<string, object>
        {
            { "Distance", distance },
            { "X1", pointA.X },
            { "Y1", pointA.Y },
            { "X2", resolvedEndPoint.X },
            { "Y2", resolvedEndPoint.Y },
            { "MeasureType", measureType },
            { "DeltaX", resolvedEndPoint.X - pointA.X },
            { "DeltaY", resolvedEndPoint.Y - pointA.Y },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", ComputeConfidence(uncertaintyPx) },
            { "UncertaintyPx", uncertaintyPx }
        });
    }

    private static bool TryMeasure(
        Position start,
        Position end,
        string normalizedType,
        out double distance,
        out Position drawnEndPoint,
        out string label,
        out string? error)
    {
        distance = 0;
        drawnEndPoint = end;
        label = string.Empty;
        error = null;

        if (Distance(start, end) < 1e-9)
        {
            error = "[DegenerateGeometry] Start and end points are identical";
            return false;
        }

        switch (normalizedType)
        {
            case "pointtopoint":
                distance = Distance(start, end);
                drawnEndPoint = end;
                label = $"{distance:F2}px";
                return true;
            case "horizontal":
                distance = Math.Abs(end.X - start.X);
                if (distance < 1e-9)
                {
                    error = "[DegenerateGeometry] Horizontal distance is zero";
                    return false;
                }

                drawnEndPoint = new Position(end.X, start.Y);
                label = $"H: {distance:F2}px";
                return true;
            case "vertical":
                distance = Math.Abs(end.Y - start.Y);
                if (distance < 1e-9)
                {
                    error = "[DegenerateGeometry] Vertical distance is zero";
                    return false;
                }

                drawnEndPoint = new Position(start.X, end.Y);
                label = $"V: {distance:F2}px";
                return true;
            default:
                error = $"Unsupported measure type: {normalizedType}";
                return false;
        }
    }

    private static void DrawLineDistance(Mat image, Position start, Position end, string label)
    {
        var p1 = ToCvPoint(start);
        var p2 = ToCvPoint(end);
        Cv2.Line(image, p1, p2, new Scalar(0, 255, 0), 2);
        Cv2.Circle(image, p1, 5, new Scalar(255, 0, 0), -1);
        Cv2.Circle(image, p2, 5, new Scalar(255, 0, 0), -1);
        var textPoint = new Point(((p1.X + p2.X) / 2) + 6, ((p1.Y + p2.Y) / 2) - 6);
        Cv2.PutText(image, label, textPoint, HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
    }

    private static double Distance(Position p1, Position p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static bool TryParsePoint(object? obj, out Position point, out double sigmaPx)
    {
        point = new Position(0, 0);
        sigmaPx = 0.0;
        if (obj == null)
        {
            return false;
        }

        switch (obj)
        {
            case Point p:
                point = new Position(p.X, p.Y);
                sigmaPx = 0.5;
                return true;
            case Point2f p2f:
                point = new Position(p2f.X, p2f.Y);
                sigmaPx = 0.08;
                return true;
            case Point2d p2d:
                point = new Position(p2d.X, p2d.Y);
                sigmaPx = 0.05;
                return true;
            case Position pos:
                point = pos;
                sigmaPx = HasFractionalComponent(pos.X) || HasFractionalComponent(pos.Y) ? 0.05 : 0.5;
                return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var parsedX) &&
            TryGetDouble(dict, "Y", out var parsedY))
        {
            point = new Position(parsedX, parsedY);
            sigmaPx = HasFractionalComponent(parsedX) || HasFractionalComponent(parsedY) ? 0.05 : 0.5;
            return true;
        }

        if (obj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(
                    entry => entry.Key!.ToString() ?? string.Empty,
                    entry => entry.Value ?? 0.0,
                    StringComparer.OrdinalIgnoreCase);

            if (TryGetDouble(normalized, "X", out parsedX) && TryGetDouble(normalized, "Y", out parsedY))
            {
                point = new Position(parsedX, parsedY);
                sigmaPx = HasFractionalComponent(parsedX) || HasFractionalComponent(parsedY) ? 0.05 : 0.5;
                return true;
            }
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

        if (!double.TryParse(parts[0], out var stringX) || !double.TryParse(parts[1], out var stringY))
        {
            return false;
        }

        point = new Position(stringX, stringY);
        sigmaPx = HasFractionalComponent(stringX) || HasFractionalComponent(stringY) ? 0.05 : 0.5;
        return true;
    }

    private static double ComputeDistanceUncertaintyPx(string normalizedType, double sigmaPointA, double sigmaPointB)
    {
        return normalizedType switch
        {
            "pointtopoint" => Math.Sqrt((sigmaPointA * sigmaPointA) + (sigmaPointB * sigmaPointB)),
            "horizontal" => Math.Sqrt((sigmaPointA * sigmaPointA) + (sigmaPointB * sigmaPointB)),
            "vertical" => Math.Sqrt((sigmaPointA * sigmaPointA) + (sigmaPointB * sigmaPointB)),
            _ => double.NaN
        };
    }

    private static double ComputeConfidence(double uncertaintyPx)
    {
        if (!double.IsFinite(uncertaintyPx) || uncertaintyPx < 0)
        {
            return 0.0;
        }

        return 1.0 / (1.0 + uncertaintyPx);
    }

    private static bool HasFractionalComponent(double value)
    {
        return Math.Abs(value - Math.Round(value)) > 1e-9;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0.0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }

    private static Point ToCvPoint(Position point)
    {
        return new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
    }
}
