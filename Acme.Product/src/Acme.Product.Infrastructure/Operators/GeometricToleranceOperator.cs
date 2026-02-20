// GeometricToleranceOperator.cs
// 几何公差算子 - 平行度// 功能实现垂直度测量
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 几何公差算子 - 平行度/垂直度测量
/// </summary>
public class GeometricToleranceOperator : OperatorBase
{
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

        var measureType = GetStringParam(@operator, "MeasureType", "Parallelism");
        var line1X1 = GetIntParam(@operator, "Line1_X1", 0);
        var line1Y1 = GetIntParam(@operator, "Line1_Y1", 0);
        var line1X2 = GetIntParam(@operator, "Line1_X2", 100);
        var line1Y2 = GetIntParam(@operator, "Line1_Y2", 100);
        var line2X1 = GetIntParam(@operator, "Line2_X1", 0);
        var line2Y1 = GetIntParam(@operator, "Line2_Y1", 200);
        var line2X2 = GetIntParam(@operator, "Line2_X2", 100);
        var line2Y2 = GetIntParam(@operator, "Line2_Y2", 200);

        var src = imageWrapper.GetMat();
            if (src.Empty())
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
            }

            // 创建结果图像副本
            var resultImage = src.Clone();

            // 定义两条线的端点
            var line1Start = new Point(line1X1, line1Y1);
            var line1End = new Point(line1X2, line1Y2);
            var line2Start = new Point(line2X1, line2Y1);
            var line2End = new Point(line2X2, line2Y2);

            // 绘制线条
            Cv2.Line(resultImage, line1Start, line1End, new Scalar(0, 255, 0), 2);
            Cv2.Line(resultImage, line2Start, line2End, new Scalar(0, 255, 0), 2);
            Cv2.Circle(resultImage, line1Start, 4, new Scalar(0, 0, 255), -1);
            Cv2.Circle(resultImage, line1End, 4, new Scalar(0, 0, 255), -1);
            Cv2.Circle(resultImage, line2Start, 4, new Scalar(255, 0, 0), -1);
            Cv2.Circle(resultImage, line2End, 4, new Scalar(255, 0, 0), -1);

            // 计算公差
            double tolerance = 0;
            string result = "";

            if (measureType == "Parallelism")
            {
                tolerance = CalculateParallelism(line1Start, line1End, line2Start, line2End);
                result = $"Parallelism: {tolerance:F4}°";
            }
            else if (measureType == "Perpendicularity")
            {
                tolerance = CalculatePerpendicularity(line1Start, line1End, line2Start, line2End);
                result = $"Perpendicularity: {tolerance:F4}°";
            }

            // 显示结果
            Cv2.PutText(resultImage, result, new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 255), 2);

            var additionalData = new Dictionary<string, object>
            {
                { "Tolerance", tolerance },
                { "MeasureType", measureType },
                { "Result", result }
            };
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    private double CalculateParallelism(Point l1s, Point l1e, Point l2s, Point l2e)
    {
        double angle1 = Math.Atan2(l1e.Y - l1s.Y, l1e.X - l1s.X);
        double angle2 = Math.Atan2(l2e.Y - l2s.Y, l2e.X - l2s.X);

        double diff = Math.Abs(angle1 - angle2) * 180 / Math.PI;

        if (diff > 90) diff = 180 - diff;

        return diff;
    }

    private double CalculatePerpendicularity(Point l1s, Point l1e, Point l2s, Point l2e)
    {
        double angle1 = Math.Atan2(l1e.Y - l1s.Y, l1e.X - l1s.X);
        double angle2 = Math.Atan2(l2e.Y - l2s.Y, l2e.X - l2s.X);

        double diff = Math.Abs(angle1 - angle2) * 180 / Math.PI;

        double deviation = Math.Abs(diff - 90);
        if (deviation > 90) deviation = 180 - deviation;

        return deviation;
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
}
