// MeasureDistanceOperator.cs
// 距离测量算子 - 测量两点之间或点到轮廓的距离
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 距离测量算子 - 测量两点之间或点到轮廓的距离
/// </summary>
[OperatorMeta(
    DisplayName = "测量",
    Description = "两点/水平/垂直距离测量，支持图像坐标和 Point 输入两种模式，用于尺寸检测",
    Category = "检测",
    IconName = "measure",
    Keywords = new[] { "测量", "距离", "长度", "卡尺", "尺寸", "两点间距", "Measure", "Distance", "Length", "Size" }
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

    public MeasureDistanceOperator(ILogger<MeasureDistanceOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var measureType = GetStringParam(@operator, "MeasureType", "PointToPoint");

        // 优先使用 PointA/PointB 输入端口（无图测距模式）
        if (inputs != null &&
            inputs.TryGetValue("PointA", out var ptAObj) && ptAObj != null &&
            inputs.TryGetValue("PointB", out var ptBObj) && ptBObj != null)
        {
            if (TryParsePoint(ptAObj, out var pA) && TryParsePoint(ptBObj, out var pB))
            {
                var dist = Math.Sqrt(Math.Pow(pB.X - pA.X, 2) + Math.Pow(pB.Y - pA.Y, 2));
                return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Distance", dist },
                    { "X1", pA.X }, { "Y1", pA.Y },
                    { "X2", pB.X }, { "Y2", pB.Y },
                    { "MeasureType", measureType },
                    { "DeltaX", pB.X - pA.X },
                    { "DeltaY", pB.Y - pA.Y }
                }));
            }
        }

        // 回退到 Image + 参数坐标模式
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像或 PointA/PointB"));
        }

        // 获取参数
        var x1 = GetIntParam(@operator, "X1", 0);
        var y1 = GetIntParam(@operator, "Y1", 0);
        var x2 = GetIntParam(@operator, "X2", 100);
        var y2 = GetIntParam(@operator, "Y2", 100);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        double distance = 0;
        var resultImg = src.Clone();
        Point pt1 = new Point(x1, y1);
        Point pt2 = new Point(x2, y2);

        switch (measureType.ToLower())
        {
            case "pointtopoint":
                // 点到点距离
                distance = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));

                // 绘制测量线
                Cv2.Line(resultImg, pt1, pt2, new Scalar(0, 255, 0), 2);
                Cv2.Circle(resultImg, pt1, 5, new Scalar(255, 0, 0), -1);
                Cv2.Circle(resultImg, pt2, 5, new Scalar(255, 0, 0), -1);

                // 显示距离
                var midPoint = new Point((x1 + x2) / 2, (y1 + y2) / 2);
                Cv2.PutText(resultImg, $"{distance:F2}px", midPoint,
                    HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
                break;

            case "horizontal":
                // 水平距离
                distance = Math.Abs(x2 - x1);
                pt2.Y = y1; // 保持水平

                Cv2.Line(resultImg, pt1, pt2, new Scalar(0, 255, 0), 2);
                Cv2.Circle(resultImg, pt1, 5, new Scalar(255, 0, 0), -1);
                Cv2.Circle(resultImg, pt2, 5, new Scalar(255, 0, 0), -1);

                var hMidPoint = new Point((x1 + x2) / 2, y1 - 10);
                Cv2.PutText(resultImg, $"H: {distance:F2}px", hMidPoint,
                    HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
                break;

            case "vertical":
                // 垂直距离
                distance = Math.Abs(y2 - y1);
                pt2.X = x1; // 保持垂直

                Cv2.Line(resultImg, pt1, pt2, new Scalar(0, 255, 0), 2);
                Cv2.Circle(resultImg, pt1, 5, new Scalar(255, 0, 0), -1);
                Cv2.Circle(resultImg, pt2, 5, new Scalar(255, 0, 0), -1);

                var vMidPoint = new Point(x1 + 10, (y1 + y2) / 2);
                Cv2.PutText(resultImg, $"V: {distance:F2}px", vMidPoint,
                    HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
                break;

            default:
                return Task.FromResult(OperatorExecutionOutput.Failure($"不支持的测量类型: {measureType}"));
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImg, new Dictionary<string, object>
        {
            { "Distance", distance },
            { "X1", x1 },
            { "Y1", y1 },
            { "X2", x2 },
            { "Y2", y2 },
            { "MeasureType", measureType },
            { "DeltaX", x2 - x1 },
            { "DeltaY", y2 - y1 }
        })));
    }

    /// <summary>
    /// 尝试从输入对象中解析 Point 坐标（支持 "(x,y)" 字符串和 OpenCvSharp.Point）
    /// </summary>
    private static bool TryParsePoint(object obj, out Point point)
    {
        point = default;
        if (obj is Point p)
        {
            point = p;
            return true;
        }
        var str = obj.ToString()?.Trim('(', ')', ' ');
        if (str == null)
            return false;
        var parts = str.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out var x) &&
            int.TryParse(parts[1].Trim(), out var y))
        {
            point = new Point(x, y);
            return true;
        }
        return false;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var measureType = GetStringParam(@operator, "MeasureType", "PointToPoint").ToLower();
        var validTypes = new[] { "pointtopoint", "horizontal", "vertical" };

        if (!validTypes.Contains(measureType))
        {
            return ValidationResult.Invalid($"不支持的测量类型: {measureType}");
        }

        return ValidationResult.Valid();
    }
}
