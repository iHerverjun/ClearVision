// LineMeasurementOperator.cs
// 直线测量算子 - 霍夫直线检测与测量
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 直线测量算子 - 霍夫直线检测与测量
/// </summary>
public class LineMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LineMeasurement;

    public LineMeasurementOperator(ILogger<LineMeasurementOperator> logger) : base(logger)
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

        var method = GetStringParam(@operator, "Method", "HoughLine");
        var threshold = GetIntParam(@operator, "Threshold", 100, min: 1);
        var minLength = GetDoubleParam(@operator, "MinLength", 50.0, min: 0);
        var maxGap = GetDoubleParam(@operator, "MaxGap", 10.0, min: 0);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        using var resultImage = src.Clone();
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        var lineResults = new List<Dictionary<string, object>>();

        if (method == "HoughLine")
        {
            var lines = Cv2.HoughLines(edges, 1, Math.PI / 180, threshold);
            
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    double rho = line.Rho;
                    double theta = line.Theta;
                    
                    double cos = Math.Cos(theta);
                    double sin = Math.Sin(theta);
                    double x0 = rho * cos;
                    double y0 = rho * sin;
                    
                    Point pt1 = new((int)(x0 + 1000 * (-sin)), (int)(y0 + 1000 * cos));
                    Point pt2 = new((int)(x0 - 1000 * (-sin)), (int)(y0 - 1000 * cos));
                    
                    Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);

                    double angleDegrees = theta * 180 / Math.PI;
                    if (angleDegrees > 90) angleDegrees -= 180;

                    lineResults.Add(new Dictionary<string, object>
                    {
                        { "Rho", rho },
                        { "Theta", theta },
                        { "Angle", angleDegrees },
                        { "StartX", pt1.X },
                        { "StartY", pt1.Y },
                        { "EndX", pt2.X },
                        { "EndY", pt2.Y }
                    });
                }
            }
        }
        else
        {
            var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, threshold, minLength, maxGap);
            
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    Point pt1 = new(line.P1.X, line.P1.Y);
                    Point pt2 = new(line.P2.X, line.P2.Y);
                    
                    Cv2.Line(resultImage, pt1, pt2, new Scalar(0, 255, 0), 2);
                    
                    double length = Math.Sqrt(Math.Pow(pt2.X - pt1.X, 2) + Math.Pow(pt2.Y - pt1.Y, 2));
                    double angleDegrees = Math.Atan2(pt2.Y - pt1.Y, pt2.X - pt1.X) * 180 / Math.PI;

                    lineResults.Add(new Dictionary<string, object>
                    {
                        { "StartX", pt1.X },
                        { "StartY", pt1.Y },
                        { "EndX", pt2.X },
                        { "EndY", pt2.Y },
                        { "Length", length },
                        { "Angle", angleDegrees }
                    });
                }
            }
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "LineCount", lineResults.Count },
            { "Lines", lineResults }
        };

        var firstLine = lineResults.FirstOrDefault();
        if (firstLine != null)
        {
            foreach (var kvp in firstLine)
            {
                if (!additionalData.ContainsKey(kvp.Key))
                    additionalData[kvp.Key] = kvp.Value;
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetIntParam(@operator, "Threshold", 100);
        var minLength = GetDoubleParam(@operator, "MinLength", 50.0);
        var maxGap = GetDoubleParam(@operator, "MaxGap", 10.0);

        if (threshold < 1)
            return ValidationResult.Invalid("累加阈值必须大于等于1");
        if (minLength < 0)
            return ValidationResult.Invalid("最小长度不能为负数");
        if (maxGap < 0)
            return ValidationResult.Invalid("最大间隙不能为负数");

        return ValidationResult.Valid();
    }
}
