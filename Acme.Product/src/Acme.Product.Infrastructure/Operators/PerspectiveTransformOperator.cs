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
        var hasPointSetInput = TryResolvePointSet(inputs, @operator, "SrcPoints", "SrcPointsJson", out parsedSrcPoints) &&
                               TryResolvePointSet(inputs, @operator, "DstPoints", "DstPointsJson", out parsedDstPoints) &&
                               parsedSrcPoints.Length >= 4 &&
                               parsedDstPoints.Length >= 4;

        var srcPoints = hasPointSetInput
            ? parsedSrcPoints.Take(4).ToArray()
            : GetLegacyPoints(@operator, isSource: true);

        var dstPoints = hasPointSetInput
            ? parsedDstPoints.Take(4).ToArray()
            : GetLegacyPoints(@operator, isSource: false);

        using var perspectiveMatrix = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);
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
            if (!TryParsePointArray(srcPointsJson, out var srcPoints) || srcPoints.Length < 4)
            {
                return ValidationResult.Invalid("SrcPointsJson 至少需要 4 个点");
            }

            if (!TryParsePointArray(dstPointsJson, out var dstPoints) || dstPoints.Length < 4)
            {
                return ValidationResult.Invalid("DstPointsJson 至少需要 4 个点");
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

    private bool TryResolvePointSet(
        Dictionary<string, object>? inputs,
        Operator @operator,
        string inputPort,
        string jsonParamName,
        out Point2f[] points)
    {
        points = Array.Empty<Point2f>();

        if (inputs != null &&
            inputs.TryGetValue(inputPort, out var rawInput) &&
            TryParsePointCollection(rawInput, out points) &&
            points.Length >= 4)
        {
            return true;
        }

        var json = GetStringParam(@operator, jsonParamName, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        return TryParsePointArray(json, out points) && points.Length >= 4;
    }

    private static bool TryParsePointCollection(object? raw, out Point2f[] points)
    {
        points = Array.Empty<Point2f>();
        if (raw == null)
        {
            return false;
        }

        if (raw is string json && !string.IsNullOrWhiteSpace(json))
        {
            return TryParsePointArray(json, out points);
        }

        if (raw is IEnumerable<Position> positions)
        {
            points = positions.Select(p => new Point2f((float)p.X, (float)p.Y)).ToArray();
            return points.Length > 0;
        }

        if (raw is IEnumerable<Point> cvPoints)
        {
            points = cvPoints.Select(p => new Point2f(p.X, p.Y)).ToArray();
            return points.Length > 0;
        }

        if (raw is IEnumerable<Point2f> point2Fs)
        {
            points = point2Fs.ToArray();
            return points.Length > 0;
        }

        if (raw is IEnumerable<object> objectList)
        {
            var parsed = new List<Point2f>();
            foreach (var item in objectList)
            {
                if (TryParsePoint(item, out var point))
                {
                    parsed.Add(point);
                }
            }

            points = parsed.ToArray();
            return points.Length > 0;
        }

        return false;
    }

    private static bool TryParsePointArray(string json, out Point2f[] points)
    {
        points = Array.Empty<Point2f>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<Point2f>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (TryParsePoint(element, out var point))
                {
                    parsed.Add(point);
                }
            }

            points = parsed.ToArray();
            return points.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParsePoint(object? raw, out Point2f point)
    {
        point = default;
        switch (raw)
        {
            case null:
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

                return false;

            case JsonElement element:
                return TryParsePoint(element, out point);

            default:
                return false;
        }
    }

    private static bool TryParsePoint(JsonElement element, out Point2f point)
    {
        point = default;
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() >= 2)
        {
            point = new Point2f(ParseElementAsFloat(element[0]), ParseElementAsFloat(element[1]));
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            point = new Point2f(
                GetPropertyAsFloat(element, "x", "X"),
                GetPropertyAsFloat(element, "y", "Y"));
            return true;
        }

        return false;
    }

    private static float ParseElementAsFloat(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetSingle(),
            JsonValueKind.String => float.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0f,
            _ => 0f
        };
    }

    private static float GetPropertyAsFloat(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var value))
            {
                continue;
            }

            return ParseElementAsFloat(value);
        }

        return 0f;
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
