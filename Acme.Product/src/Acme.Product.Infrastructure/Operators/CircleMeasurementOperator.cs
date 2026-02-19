// CircleMeasurementOperator.cs
// 圆测量算子 - 霍夫圆检测与测量
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 圆测量算子 - 霍夫圆检测与测量
/// </summary>
public class CircleMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CircleMeasurement;

    public CircleMeasurementOperator(ILogger<CircleMeasurementOperator> logger) : base(logger)
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

        // 获取参数
        var method = GetStringParam(@operator, "Method", "HoughCircle");
        var minRadius = GetIntParam(@operator, "MinRadius", 10, min: 0);
        var maxRadius = GetIntParam(@operator, "MaxRadius", 200, min: minRadius + 1);
        var dp = GetDoubleParam(@operator, "Dp", 1.0, min: 0.5, max: 4.0);
        var minDist = GetDoubleParam(@operator, "MinDist", 50.0, min: 1);
        var param1 = GetDoubleParam(@operator, "Param1", 100.0, min: 0, max: 255);
        var param2 = GetDoubleParam(@operator, "Param2", 30.0, min: 0, max: 255);

        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 创建结果图像副本用于绘制
        using var resultImage = src.Clone();

        // 转换为灰度图
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // 使用高斯模糊降噪
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(9, 9), 2);

        // 霍夫圆检测
        var circles = Cv2.HoughCircles(blurred, HoughModes.Gradient, dp, minDist, param1, param2, minRadius, maxRadius);

        var circleResults = new List<Dictionary<string, object>>();
        var circleDataList = new List<CircleData>(); // Sprint 1 Task 1.2: CircleData 列表
        
        if (circles != null)
        {
            foreach (var circle in circles)
            {
                var center = new Point((int)circle.Center.X, (int)circle.Center.Y);
                var radius = (int)circle.Radius;

                // 绘制圆
                Cv2.Circle(resultImage, center, radius, new Scalar(0, 255, 0), 2);
                // 绘制圆心
                Cv2.Circle(resultImage, center, 3, new Scalar(0, 0, 255), -1);

                // 计算圆度 (理想圆为1.0)
                double circularity = CalculateCircularity(gray, center, radius);

                circleResults.Add(new Dictionary<string, object>
                {
                    { "CenterX", circle.Center.X },
                    { "CenterY", circle.Center.Y },
                    { "Radius", circle.Radius },
                    { "Circularity", circularity }
                });

                // Sprint 1 Task 1.2: 创建 CircleData 对象
                circleDataList.Add(new CircleData(
                    (float)circle.Center.X,
                    (float)circle.Center.Y,
                    (float)circle.Radius
                ));
            }
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        // Sprint 1 Task 1.2: 添加 CircleData 输出端口
        var additionalData = new Dictionary<string, object>
        {
            { "Circles", circleResults },
            { "CircleCount", circleResults.Count },
            { "CircleDataList", circleDataList } // 新增 CircleDataList 输出
        };
        
        if (circleResults.Count > 0)
        {
            var firstCircle = circleResults.FirstOrDefault();
            var firstCircleData = circleDataList.FirstOrDefault();
            if (firstCircle != null)
            {
                additionalData["CenterX"] = firstCircle["CenterX"];
                additionalData["CenterY"] = firstCircle["CenterY"];
                additionalData["Radius"] = firstCircle["Radius"];
                additionalData["Circularity"] = firstCircle["Circularity"];
                
                // Sprint 1 Task 1.2: 添加单个 CircleData 输出
                if (firstCircleData != null)
                {
                    additionalData["Circle"] = firstCircleData;
                }
            }
        }
        
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    private double CalculateCircularity(Mat grayImage, Point center, int radius)
    {
        try
        {
            // 【修复】在原始灰度图的 ROI 上做边缘检测，计算真实圆度
            // 1. 以检测到的圆为 ROI 提取区域（添加边界检查）
            var roiX = Math.Max(0, center.X - radius);
            var roiY = Math.Max(0, center.Y - radius);
            var roiWidth = Math.Min(radius * 2, grayImage.Width - roiX);
            var roiHeight = Math.Min(radius * 2, grayImage.Height - roiY);

            if (roiWidth <= 0 || roiHeight <= 0)
            {
                return 0.0;
            }

            var roi = new Rect(roiX, roiY, roiWidth, roiHeight);

            // 2. 在 ROI 上做边缘检测
            using var roiMat = new Mat(grayImage, roi);
            using var edges = new Mat();
            Cv2.Canny(roiMat, edges, 50, 150);

            // 3. 找轮廓，计算真实圆度
            Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
            {
                return 0.0;
            }

            // 取面积最大的轮廓
            var mainContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            double area = Cv2.ContourArea(mainContour);
            double perimeter = Cv2.ArcLength(mainContour, true);

            // 圆度 = 4π * 面积 / 周长² (理想圆 = 1.0)
            if (perimeter > 0)
            {
                double circularity = 4 * Math.PI * area / (perimeter * perimeter);
                return Math.Min(1.0, Math.Max(0.0, circularity));
            }
        }
        catch (Exception ex)
        {
            // 【修复】不吞掉异常，使用结构化日志记录
            Logger.LogDebug(ex, "[CircleMeasurement] 圆度计算异常: {ErrorMessage}", ex.Message);
        }

        return 0.0;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minRadius = GetIntParam(@operator, "MinRadius", 10);
        var maxRadius = GetIntParam(@operator, "MaxRadius", 200);
        var dp = GetDoubleParam(@operator, "Dp", 1.0);
        var param1 = GetDoubleParam(@operator, "Param1", 100.0);
        var param2 = GetDoubleParam(@operator, "Param2", 30.0);

        if (minRadius < 0)
        {
            return ValidationResult.Invalid("最小半径不能为负数");
        }
        if (maxRadius <= minRadius)
        {
            return ValidationResult.Invalid("最大半径必须大于最小半径");
        }
        if (dp < 0.5 || dp > 4.0)
        {
            return ValidationResult.Invalid("分辨率比必须在 0.5-4.0 之间");
        }
        if (param1 < 0 || param1 > 255)
        {
            return ValidationResult.Invalid("Canny阈值必须在 0-255 之间");
        }
        if (param2 < 0 || param2 > 255)
        {
            return ValidationResult.Invalid("累加器阈值必须在 0-255 之间");
        }
        return ValidationResult.Valid();
    }
}
