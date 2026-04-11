using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "角度测量",
    Description = "基于三点计算夹角。",
    Category = "检测",
    IconName = "angle-measure",
    Keywords = new[] { "角度", "三点", "夹角", "Angle", "Degree", "Radian" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Angle", "角度", PortDataType.Float)]
[OperatorParam("Point1X", "点1 X", "int", DefaultValue = 0)]
[OperatorParam("Point1Y", "点1 Y", "int", DefaultValue = 0)]
[OperatorParam("Point2X", "点2 X(顶点)", "int", DefaultValue = 100)]
[OperatorParam("Point2Y", "点2 Y(顶点)", "int", DefaultValue = 100)]
[OperatorParam("Point3X", "点3 X", "int", DefaultValue = 200)]
[OperatorParam("Point3Y", "点3 Y", "int", DefaultValue = 0)]
[OperatorParam("Unit", "角度单位", "enum", DefaultValue = "Degree", Options = new[] { "Degree|度", "Radian|弧度" })]
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

        var unit = GetStringParam(@operator, "Unit", "Degree");
        var p1 = new Point(GetIntParam(@operator, "Point1X", 0), GetIntParam(@operator, "Point1Y", 0));
        var p2 = new Point(GetIntParam(@operator, "Point2X", 100), GetIntParam(@operator, "Point2Y", 100));
        var p3 = new Point(GetIntParam(@operator, "Point3X", 200), GetIntParam(@operator, "Point3Y", 0));

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("输入图像无效"));
        }

        var v1x = p1.X - p2.X;
        var v1y = p1.Y - p2.Y;
        var v2x = p3.X - p2.X;
        var v2y = p3.Y - p2.Y;
        var len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var len2 = Math.Sqrt(v2x * v2x + v2y * v2y);
        if (len1 < 1e-9 || len2 < 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Angle vertex has zero-length arm"));
        }

        var angleRad = ComputeAngleRadians(v1x, v1y, v2x, v2y, len1, len2);
        var angle = unit.Equals("Radian", StringComparison.OrdinalIgnoreCase)
            ? angleRad
            : angleRad * 180.0 / Math.PI;

        var resultImage = src.Clone();
        DrawGeometry(resultImage, p1, p2, p3, angle, unit);

        var output = CreateImageOutput(resultImage, new Dictionary<string, object>
        {
            { "Angle", angle },
            { "Unit", unit },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        });

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var unit = GetStringParam(@operator, "Unit", "Degree");
        if (!unit.Equals("Degree", StringComparison.OrdinalIgnoreCase) &&
            !unit.Equals("Radian", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("角度单位必须是 Degree 或 Radian");
        }

        return ValidationResult.Valid();
    }

    private static double ComputeAngleRadians(double v1x, double v1y, double v2x, double v2y, double len1, double len2)
    {
        var dot = v1x * v2x + v1y * v2y;
        var cosTheta = Math.Clamp(dot / (len1 * len2), -1.0, 1.0);
        return Math.Acos(cosTheta);
    }

    private static void DrawGeometry(Mat image, Point p1, Point p2, Point p3, double angle, string unit)
    {
        Cv2.Circle(image, p1, 5, new Scalar(0, 0, 255), -1);
        Cv2.Circle(image, p2, 5, new Scalar(0, 255, 0), -1);
        Cv2.Circle(image, p3, 5, new Scalar(255, 0, 0), -1);
        Cv2.Line(image, p1, p2, new Scalar(0, 255, 255), 2);
        Cv2.Line(image, p2, p3, new Scalar(0, 255, 255), 2);

        Cv2.PutText(
            image,
            $"Angle: {angle:F2} {unit}",
            new Point(p2.X + 8, p2.Y - 8),
            HersheyFonts.HersheySimplex,
            0.7,
            new Scalar(255, 255, 255),
            2);
    }
}
