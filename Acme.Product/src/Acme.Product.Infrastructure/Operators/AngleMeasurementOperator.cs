// AngleMeasurementOperator.cs
// 角度测量算子 - 基于三点计算角度
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 角度测量算子 - 基于三点计算角度
/// </summary>
public class AngleMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.AngleMeasurement;

    public AngleMeasurementOperator(ILogger<AngleMeasurementOperator> logger) : base(logger)
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

        var point1X = GetIntParam(@operator, "Point1X", 0);
        var point1Y = GetIntParam(@operator, "Point1Y", 0);
        var point2X = GetIntParam(@operator, "Point2X", 100);
        var point2Y = GetIntParam(@operator, "Point2Y", 100);
        var point3X = GetIntParam(@operator, "Point3X", 200);
        var point3Y = GetIntParam(@operator, "Point3Y", 0);
        var unit = GetStringParam(@operator, "Unit", "Degree");

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        using var resultImage = src.Clone();

        var p1 = new Point(point1X, point1Y);
        var p2 = new Point(point2X, point2Y);
        var p3 = new Point(point3X, point3Y);

        Cv2.Circle(resultImage, p1, 5, new Scalar(0, 0, 255), -1);
        Cv2.Circle(resultImage, p2, 5, new Scalar(0, 255, 0), -1);
        Cv2.Circle(resultImage, p3, 5, new Scalar(255, 0, 0), -1);
        Cv2.Line(resultImage, p1, p2, new Scalar(0, 255, 255), 2);
        Cv2.Line(resultImage, p2, p3, new Scalar(0, 255, 255), 2);

        double angle = CalculateAngle(p1, p2, p3, unit);

        var text = $"Angle: {angle:F2} {unit}";
        Cv2.PutText(resultImage, text, new Point(p2.X + 10, p2.Y - 10), HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 255), 2);

        // P0: 使用ImageWrapper实现零拷贝输出
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Angle", angle },
            { "Unit", unit }
        })));
    }

    private double CalculateAngle(Point p1, Point p2, Point p3, string unit)
    {
        double v1x = p1.X - p2.X;
        double v1y = p1.Y - p2.Y;
        double v2x = p3.X - p2.X;
        double v2y = p3.Y - p2.Y;

        double angle1 = Math.Atan2(v1y, v1x);
        double angle2 = Math.Atan2(v2y, v2x);

        double angle = angle2 - angle1;

        if (angle < 0) angle += 2 * Math.PI;
        if (angle > 2 * Math.PI) angle -= 2 * Math.PI;

        if (angle > Math.PI) angle = 2 * Math.PI - angle;

        if (unit.ToLower() == "degree")
        {
            angle = angle * 180 / Math.PI;
        }

        return angle;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var unit = GetStringParam(@operator, "Unit", "Degree");
        if (unit.ToLower() != "degree" && unit.ToLower() != "radian")
        {
            return ValidationResult.Invalid("角度单位必须是 Degree 或 Radian");
        }
        return ValidationResult.Valid();
    }
}
