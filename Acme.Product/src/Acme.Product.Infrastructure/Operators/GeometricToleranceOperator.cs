using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Geometric tolerance operator for angle-based parallelism/perpendicularity checks.
/// Note: this is an angle-only model and not a full GD&T tolerance zone evaluation.
/// </summary>
[OperatorMeta(
    DisplayName = "几何公差",
    Description = "角度偏差测量（仅角度模型，非完整GD&T公差带）",
    Category = "检测",
    IconName = "geometric-tolerance",
    Keywords = new[]
    {
        "公差",
        "平行度",
        "垂直度",
        "GD&T",
        "Tolerance",
        "Parallelism",
        "Perpendicularity",
        "AngleOnly"
    }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Tolerance", "角度偏差", PortDataType.Float)]
[OutputPort("AngularDeviationDeg", "角度偏差(度)", PortDataType.Float)]
[OutputPort("LinearBand", "线性跳动带(像素)", PortDataType.Float)]
[OutputPort("MeasurementModel", "测量模型", PortDataType.String)]
[OperatorParam("MeasureType", "测量类型", "enum", DefaultValue = "Parallelism", Options = new[] { "Parallelism|平行度", "Perpendicularity|垂直度" })]
[OperatorParam("Line1_X1", "线1起点X", "int", DefaultValue = 0)]
[OperatorParam("Line1_Y1", "线1起点Y", "int", DefaultValue = 0)]
[OperatorParam("Line1_X2", "线1终点X", "int", DefaultValue = 100)]
[OperatorParam("Line1_Y2", "线1终点Y", "int", DefaultValue = 100)]
[OperatorParam("Line2_X1", "线2起点X", "int", DefaultValue = 0)]
[OperatorParam("Line2_Y1", "线2起点Y", "int", DefaultValue = 200)]
[OperatorParam("Line2_X2", "线2终点X", "int", DefaultValue = 100)]
[OperatorParam("Line2_Y2", "线2终点Y", "int", DefaultValue = 200)]
public class GeometricToleranceOperator : OperatorBase
{
    private const string AngleOnlyModel = "AngleOnly";

    public override OperatorType OperatorType => OperatorType.GeometricTolerance;

    public GeometricToleranceOperator(ILogger<GeometricToleranceOperator> logger) : base(logger)
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

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        var measureType = GetStringParam(@operator, "MeasureType", "Parallelism");
        var line1Start = new Point(GetIntParam(@operator, "Line1_X1", 0), GetIntParam(@operator, "Line1_Y1", 0));
        var line1End = new Point(GetIntParam(@operator, "Line1_X2", 100), GetIntParam(@operator, "Line1_Y2", 100));
        var line2Start = new Point(GetIntParam(@operator, "Line2_X1", 0), GetIntParam(@operator, "Line2_Y1", 200));
        var line2End = new Point(GetIntParam(@operator, "Line2_X2", 100), GetIntParam(@operator, "Line2_Y2", 200));

        if (!IsValidLine(line1Start, line1End) || !IsValidLine(line2Start, line2End))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("线段长度不能为 0"));
        }

        var evaluation = Evaluate(measureType, line1Start, line1End, line2Start, line2End);

        var resultImage = src.Clone();
        DrawInputLines(resultImage, line1Start, line1End, line2Start, line2End);

        var line1Text = $"{evaluation.Label}: {evaluation.AngularDeviationDeg:F4} deg (angle-only)";
        Cv2.PutText(resultImage, line1Text, new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);

        if (!double.IsNaN(evaluation.LinearBand))
        {
            var line2Text = $"Linear band: {evaluation.LinearBand:F4}px";
            Cv2.PutText(resultImage, line2Text, new Point(10, 56), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);
        }

        var additionalData = new Dictionary<string, object>
        {
            { "Tolerance", evaluation.AngularDeviationDeg },
            { "AngularDeviationDeg", evaluation.AngularDeviationDeg },
            { "LinearBand", evaluation.LinearBand },
            { "MeasureType", measureType },
            { "MeasurementModel", AngleOnlyModel },
            { "Result", evaluation.ResultText }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var measureType = GetStringParam(@operator, "MeasureType", "Parallelism");
        if (measureType != "Parallelism" && measureType != "Perpendicularity")
        {
            return ValidationResult.Invalid("测量类型必须是 Parallelism 或 Perpendicularity");
        }

        return ValidationResult.Valid();
    }

    private static bool IsValidLine(Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        return dx * dx + dy * dy > 0;
    }

    private static void DrawInputLines(Mat image, Point line1Start, Point line1End, Point line2Start, Point line2End)
    {
        Cv2.Line(image, line1Start, line1End, new Scalar(0, 255, 0), 2);
        Cv2.Line(image, line2Start, line2End, new Scalar(0, 255, 0), 2);
        Cv2.Circle(image, line1Start, 4, new Scalar(0, 0, 255), -1);
        Cv2.Circle(image, line1End, 4, new Scalar(0, 0, 255), -1);
        Cv2.Circle(image, line2Start, 4, new Scalar(255, 0, 0), -1);
        Cv2.Circle(image, line2End, 4, new Scalar(255, 0, 0), -1);
    }

    private static ToleranceEvaluation Evaluate(
        string measureType,
        Point line1Start,
        Point line1End,
        Point line2Start,
        Point line2End)
    {
        var angleDiff = ComputeAcuteAngleDifference(line1Start, line1End, line2Start, line2End);

        if (measureType.Equals("Perpendicularity", StringComparison.OrdinalIgnoreCase))
        {
            var deviation = Math.Abs(angleDiff - 90.0);
            if (deviation > 90.0)
            {
                deviation = 180.0 - deviation;
            }

            return new ToleranceEvaluation(
                Label: "PerpendicularityDeviation",
                AngularDeviationDeg: deviation,
                LinearBand: double.NaN,
                ResultText: $"Perpendicularity angle deviation = {deviation:F4} deg (angle-only model)");
        }

        var parallelDeviation = angleDiff;
        var distanceStart = DistancePointToLine(line2Start, line1Start, line1End);
        var distanceEnd = DistancePointToLine(line2End, line1Start, line1End);
        var linearBand = Math.Abs(distanceStart - distanceEnd);

        return new ToleranceEvaluation(
            Label: "ParallelismDeviation",
            AngularDeviationDeg: parallelDeviation,
            LinearBand: linearBand,
            ResultText: $"Parallelism angle deviation = {parallelDeviation:F4} deg (angle-only model), linear band = {linearBand:F4}px");
    }

    private static double ComputeAcuteAngleDifference(Point l1s, Point l1e, Point l2s, Point l2e)
    {
        var angle1 = Math.Atan2(l1e.Y - l1s.Y, l1e.X - l1s.X) * 180.0 / Math.PI;
        var angle2 = Math.Atan2(l2e.Y - l2s.Y, l2e.X - l2s.X) * 180.0 / Math.PI;

        var diff = Math.Abs(angle1 - angle2) % 180.0;
        if (diff > 90.0)
        {
            diff = 180.0 - diff;
        }

        return diff;
    }

    private static double DistancePointToLine(Point point, Point lineStart, Point lineEnd)
    {
        var a = lineEnd.Y - lineStart.Y;
        var b = lineStart.X - lineEnd.X;
        var c = lineEnd.X * lineStart.Y - lineStart.X * lineEnd.Y;
        var denominator = Math.Sqrt(a * a + b * b);
        if (denominator < 1e-9)
        {
            return 0.0;
        }

        return Math.Abs(a * point.X + b * point.Y + c) / denominator;
    }

    private readonly record struct ToleranceEvaluation(
        string Label,
        double AngularDeviationDeg,
        double LinearBand,
        string ResultText);
}
