// PerspectiveTransformOperator.cs
// 透视变换算子 - 四边形透视校正
// 作者：蘅芜君

using System.Globalization;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 透视变换算子 - 四边形透视校正
/// </summary>
[OperatorMeta(
    DisplayName = "透视变换",
    Description = "四边形透视校正",
    Category = "预处理",
    IconName = "perspective",
    Keywords = new[] { "透视", "变换", "矫正", "仿射", "四边形", "Perspective", "Warp", "Transform" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[InputPort("SrcPoints", "源点集合", PortDataType.PointList, IsRequired = false)]
[InputPort("DstPoints", "目标点集合", PortDataType.PointList, IsRequired = false)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OperatorParam("SrcPointsJson", "源点集合(JSON)", "string", DefaultValue = "")]
[OperatorParam("DstPointsJson", "目标点集合(JSON)", "string", DefaultValue = "")]
[OperatorParam("SrcX1", "源点1 X", "double", DefaultValue = 0.0)]
[OperatorParam("SrcY1", "源点1 Y", "double", DefaultValue = 0.0)]
[OperatorParam("SrcX2", "源点2 X", "double", DefaultValue = 100.0)]
[OperatorParam("SrcY2", "源点2 Y", "double", DefaultValue = 0.0)]
[OperatorParam("SrcX3", "源点3 X", "double", DefaultValue = 100.0)]
[OperatorParam("SrcY3", "源点3 Y", "double", DefaultValue = 100.0)]
[OperatorParam("SrcX4", "源点4 X", "double", DefaultValue = 0.0)]
[OperatorParam("SrcY4", "源点4 Y", "double", DefaultValue = 100.0)]
[OperatorParam("DstX1", "目标点1 X", "double", DefaultValue = 0.0)]
[OperatorParam("DstY1", "目标点1 Y", "double", DefaultValue = 0.0)]
[OperatorParam("DstX2", "目标点2 X", "double", DefaultValue = 640.0)]
[OperatorParam("DstY2", "目标点2 Y", "double", DefaultValue = 0.0)]
[OperatorParam("DstX3", "目标点3 X", "double", DefaultValue = 640.0)]
[OperatorParam("DstY3", "目标点3 Y", "double", DefaultValue = 480.0)]
[OperatorParam("DstX4", "目标点4 X", "double", DefaultValue = 0.0)]
[OperatorParam("DstY4", "目标点4 Y", "double", DefaultValue = 480.0)]
[OperatorParam("OutputWidth", "输出宽度", "int", DefaultValue = 640, Min = 1, Max = 8192)]
[OperatorParam("OutputHeight", "输出高度", "int", DefaultValue = 480, Min = 1, Max = 8192)]
public class PerspectiveTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PerspectiveTransform;

    public PerspectiveTransformOperator(ILogger<PerspectiveTransformOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var outputWidth = GetIntParam(@operator, "OutputWidth", 640, min: 1, max: 8192);
        var outputHeight = GetIntParam(@operator, "OutputHeight", 480, min: 1, max: 8192);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var parsedSrcPoints = Array.Empty<Point2f>();
        var parsedDstPoints = Array.Empty<Point2f>();
        var hasSrcPointSet = HasExplicitPointSetInput(inputs, @operator, "SrcPoints", "SrcPointsJson");
        var hasDstPointSet = HasExplicitPointSetInput(inputs, @operator, "DstPoints", "DstPointsJson");
        if (hasSrcPointSet ^ hasDstPointSet)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("SrcPoints 与 DstPoints 需要同时提供。"));
        }

        var hasPointSetInput = false;
        if (hasSrcPointSet && hasDstPointSet)
        {
            if (!TryResolvePointSet(inputs, @operator, "SrcPoints", "SrcPointsJson", out parsedSrcPoints, out var srcError))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"SrcPoints 解析失败: {srcError}"));
            }

            if (!TryResolvePointSet(inputs, @operator, "DstPoints", "DstPointsJson", out parsedDstPoints, out var dstError))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"DstPoints 解析失败: {dstError}"));
            }

            hasPointSetInput = parsedSrcPoints.Length >= 4 && parsedDstPoints.Length >= 4;
        }

        var srcPoints = hasPointSetInput
            ? parsedSrcPoints.Take(4).ToArray()
            : GetLegacyPoints(@operator, isSource: true);

        var dstPoints = hasPointSetInput
            ? parsedDstPoints.Take(4).ToArray()
            : GetLegacyPoints(@operator, isSource: false);

        if (!TryEnsureNonDegenerateQuadrilateral(srcPoints, "SrcPoints", out var srcDegenerateError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(srcDegenerateError ?? "SrcPoints are degenerate."));
        }

        if (!TryEnsureNonDegenerateQuadrilateral(dstPoints, "DstPoints", out var dstDegenerateError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(dstDegenerateError ?? "DstPoints are degenerate."));
        }

        using var perspectiveMatrix = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);
        if (!TryValidatePerspectiveMatrix(perspectiveMatrix, out var matrixError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(matrixError));
        }

        var dst = new Mat();
        Cv2.WarpPerspective(
            src,
            dst,
            perspectiveMatrix,
            new Size(outputWidth, outputHeight),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            Scalar.Black);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(dst, new Dictionary<string, object>
        {
            { "PointSetMode", hasPointSetInput ? "PointSetJsonOrInput" : "Legacy16Params" },
            { "PointCount", 4 }
        })));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var outputWidth = GetIntParam(@operator, "OutputWidth", 640);
        var outputHeight = GetIntParam(@operator, "OutputHeight", 480);

        if (outputWidth < 1 || outputWidth > 8192)
        {
            return ValidationResult.Invalid("输出宽度必须在 1-8192 之间");
        }

        if (outputHeight < 1 || outputHeight > 8192)
        {
            return ValidationResult.Invalid("输出高度必须在 1-8192 之间");
        }

        var srcPointsJson = GetStringParam(@operator, "SrcPointsJson", string.Empty);
        var dstPointsJson = GetStringParam(@operator, "DstPointsJson", string.Empty);
        var hasSrcJson = !string.IsNullOrWhiteSpace(srcPointsJson);
        var hasDstJson = !string.IsNullOrWhiteSpace(dstPointsJson);
        if (hasSrcJson ^ hasDstJson)
        {
            return ValidationResult.Invalid("SrcPointsJson 与 DstPointsJson 需要同时提供");
        }

        if (hasSrcJson && hasDstJson)
        {
            if (!TryParsePointArray(srcPointsJson, out var srcPoints, out var srcError) || srcPoints.Length < 4)
            {
                return ValidationResult.Invalid($"SrcPointsJson 至少需要 4 个点。{srcError}");
            }

            if (!TryParsePointArray(dstPointsJson, out var dstPoints, out var dstError) || dstPoints.Length < 4)
            {
                return ValidationResult.Invalid($"DstPointsJson 至少需要 4 个点。{dstError}");
            }
        }

        return ValidationResult.Valid();
    }

    private Point2f[] GetLegacyPoints(Operator @operator, bool isSource)
    {
        if (isSource)
        {
            return new[]
            {
                new Point2f((float)GetDoubleParam(@operator, "SrcX1", 0.0), (float)GetDoubleParam(@operator, "SrcY1", 0.0)),
                new Point2f((float)GetDoubleParam(@operator, "SrcX2", 100.0), (float)GetDoubleParam(@operator, "SrcY2", 0.0)),
                new Point2f((float)GetDoubleParam(@operator, "SrcX3", 100.0), (float)GetDoubleParam(@operator, "SrcY3", 100.0)),
                new Point2f((float)GetDoubleParam(@operator, "SrcX4", 0.0), (float)GetDoubleParam(@operator, "SrcY4", 100.0))
            };
        }

        return new[]
        {
            new Point2f((float)GetDoubleParam(@operator, "DstX1", 0.0), (float)GetDoubleParam(@operator, "DstY1", 0.0)),
            new Point2f((float)GetDoubleParam(@operator, "DstX2", 640.0), (float)GetDoubleParam(@operator, "DstY2", 0.0)),
            new Point2f((float)GetDoubleParam(@operator, "DstX3", 640.0), (float)GetDoubleParam(@operator, "DstY3", 480.0)),
            new Point2f((float)GetDoubleParam(@operator, "DstX4", 0.0), (float)GetDoubleParam(@operator, "DstY4", 480.0))
        };
    }

    private bool HasExplicitPointSetInput(
        Dictionary<string, object>? inputs,
        Operator @operator,
        string inputPort,
        string jsonParamName)
    {
        if (inputs != null &&
            inputs.TryGetValue(inputPort, out var rawInput) &&
            rawInput != null)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(GetStringParam(@operator, jsonParamName, string.Empty));
    }

    private bool TryResolvePointSet(
        Dictionary<string, object>? inputs,
        Operator @operator,
        string inputPort,
        string jsonParamName,
        out Point2f[] points,
        out string error)
    {
        points = Array.Empty<Point2f>();
        error = string.Empty;

        if (inputs != null &&
            inputs.TryGetValue(inputPort, out var rawInput) &&
            rawInput != null)
        {
            if (!TryParsePointCollection(rawInput, out points, out error))
            {
                return false;
            }

            if (points.Length < 4)
            {
                error = "需要至少 4 个点。";
                return false;
            }

            return true;
        }

        var json = GetStringParam(@operator, jsonParamName, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "未提供点集。";
            return false;
        }

        if (!TryParsePointArray(json, out points, out error))
        {
            return false;
        }

        if (points.Length < 4)
        {
            error = "需要至少 4 个点。";
            return false;
        }

        return true;
    }

    private static bool TryParsePointCollection(object? raw, out Point2f[] points, out string error)
    {
        points = Array.Empty<Point2f>();
        error = string.Empty;
        if (raw == null)
        {
            error = "点集为空。";
            return false;
        }

        if (raw is string json && !string.IsNullOrWhiteSpace(json))
        {
            return TryParsePointArray(json, out points, out error);
        }

        if (raw is IEnumerable<Position> positions)
        {
            points = positions.Select(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
            if (points.Length == 0)
            {
                error = "点集为空。";
                return false;
            }

            return true;
        }

        if (raw is IEnumerable<Point> cvPoints)
        {
            points = cvPoints.Select(p => new Point2f(p.X, p.Y)).ToArray();
            if (points.Length == 0)
            {
                error = "点集为空。";
                return false;
            }

            return true;
        }

        if (raw is IEnumerable<Point2f> point2Fs)
        {
            points = point2Fs.ToArray();
            if (points.Length == 0)
            {
                error = "点集为空。";
                return false;
            }

            return true;
        }

        if (raw is IEnumerable<object> objectList)
        {
            var parsed = new List<Point2f>();
            var index = 0;
            foreach (var item in objectList)
            {
                if (!TryParsePoint(item, out var point, out var parseError))
                {
                    error = $"点集第 {index} 个元素无效: {parseError}";
                    return false;
                }

                parsed.Add(point);
                index++;
            }

            points = parsed.ToArray();
            if (points.Length == 0)
            {
                error = "点集为空。";
                return false;
            }

            return true;
        }

        error = $"不支持的点集类型: {raw.GetType().Name}";
        return false;
    }

    private static bool TryParsePointArray(string json, out Point2f[] points, out string error)
    {
        points = Array.Empty<Point2f>();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "JSON 为空。";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                error = "JSON 根节点必须是数组。";
                return false;
            }

            var parsed = new List<Point2f>();
            var index = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (!TryParsePoint(element, out var point, out var parseError))
                {
                    error = $"点集第 {index} 个元素无效: {parseError}";
                    return false;
                }

                parsed.Add(point);
                index++;
            }

            points = parsed.ToArray();
            if (points.Length == 0)
            {
                error = "点集为空。";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON 解析失败: {ex.Message}";
            return false;
        }
    }

    private static bool TryParsePoint(object? raw, out Point2f point, out string error)
    {
        point = default;
        error = string.Empty;
        switch (raw)
        {
            case null:
                error = "点不能为空。";
                return false;

            case Point2f p2f:
                point = p2f;
                return true;

            case Point p:
                point = new Point2f(p.X, p.Y);
                return true;

            case Position position:
                point = new Point2f((float)position.X, (float)position.Y);
                return true;

            case IDictionary<string, object> dict:
                if (TryGetFloat(dict, "X", out var x) && TryGetFloat(dict, "Y", out var y))
                {
                    point = new Point2f(x, y);
                    return true;
                }

                error = "对象点缺少 X 或 Y 字段。";
                return false;

            case JsonElement element:
                return TryParsePoint(element, out point, out error);

            default:
                error = $"不支持的点类型: {raw.GetType().Name}";
                return false;
        }
    }

    private static bool TryParsePoint(JsonElement element, out Point2f point, out string error)
    {
        point = default;
        error = string.Empty;
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() >= 2)
        {
            if (!TryParseElementAsFloat(element[0], out var x, out var xError))
            {
                error = $"X {xError}";
                return false;
            }

            if (!TryParseElementAsFloat(element[1], out var y, out var yError))
            {
                error = $"Y {yError}";
                return false;
            }

            point = new Point2f(x, y);
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (!TryGetPropertyAsFloat(element, out var x, out var xError, "x", "X"))
            {
                error = $"X {xError}";
                return false;
            }

            if (!TryGetPropertyAsFloat(element, out var y, out var yError, "y", "Y"))
            {
                error = $"Y {yError}";
                return false;
            }

            point = new Point2f(x, y);
            return true;
        }

        error = "必须是 [x,y] 数组或 {x,y} 对象。";
        return false;
    }

    private static bool TryParseElementAsFloat(JsonElement element, out float value, out string error)
    {
        value = 0f;
        error = string.Empty;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                var parsed = element.GetSingle();
                if (!float.IsFinite(parsed))
                {
                    error = "必须是有限数值。";
                    return false;
                }

                value = parsed;
                return true;
            case JsonValueKind.String:
                if (float.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var fromString) &&
                    float.IsFinite(fromString))
                {
                    value = fromString;
                    return true;
                }

                error = "必须是有效数字。";
                return false;
            default:
                error = "必须是数字或数字字符串。";
                return false;
        }
    }

    private static bool TryGetPropertyAsFloat(JsonElement obj, out float value, out string error, params string[] names)
    {
        value = 0f;
        error = "字段缺失。";
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var propertyValue))
            {
                continue;
            }

            return TryParseElementAsFloat(propertyValue, out value, out error);
        }

        return false;
    }

    private static bool TryEnsureNonDegenerateQuadrilateral(Point2f[] points, string label, out string? error)
    {
        error = null;
        if (points.Length < 4)
        {
            error = $"{label} 至少需要 4 个点。";
            return false;
        }

        var uniqueCount = points
            .Select(p => $"{p.X:F6},{p.Y:F6}")
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (uniqueCount < 4)
        {
            error = $"{label} 包含重复点，无法构建透视变换。";
            return false;
        }

        var sample = points.Take(4).ToArray();
        var maxTriangleArea = ComputeMaxTriangleArea(sample);
        if (maxTriangleArea <= 1e-6)
        {
            error = $"{label} 几何退化（点集共线或面积近零）。";
            return false;
        }

        return true;
    }

    private static double ComputeMaxTriangleArea(IReadOnlyList<Point2f> points)
    {
        var maxArea = 0.0;
        for (var i = 0; i < points.Count - 2; i++)
        {
            for (var j = i + 1; j < points.Count - 1; j++)
            {
                for (var k = j + 1; k < points.Count; k++)
                {
                    var area = Math.Abs(ComputeTriangleTwiceArea(points[i], points[j], points[k])) * 0.5;
                    if (area > maxArea)
                    {
                        maxArea = area;
                    }
                }
            }
        }

        return maxArea;
    }

    private static double ComputeTriangleTwiceArea(Point2f a, Point2f b, Point2f c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
    }

    private static bool TryValidatePerspectiveMatrix(Mat matrix, out string error)
    {
        error = string.Empty;
        if (matrix.Empty() || matrix.Rows != 3 || matrix.Cols != 3)
        {
            error = "Perspective matrix is invalid.";
            return false;
        }

        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var value = matrix.At<double>(row, col);
                if (!double.IsFinite(value))
                {
                    error = "Perspective matrix contains non-finite values.";
                    return false;
                }
            }
        }

        var determinant = Cv2.Determinant(matrix);
        if (!double.IsFinite(determinant) || Math.Abs(determinant) <= 1e-12)
        {
            error = "Perspective matrix is singular or near-singular.";
            return false;
        }

        return true;
    }

    private static bool TryGetFloat(IDictionary<string, object> dict, string key, out float value)
    {
        value = 0f;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            float f => (value = f) == f,
            double d => (value = (float)d) == (float)d,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => float.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
        };
    }
}
