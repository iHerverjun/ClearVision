// ContourMeasurementOperator.cs
// 轮廓测量算子 - 轮廓分析与测量
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 轮廓测量算子 - 轮廓分析与测量
/// </summary>
[OperatorMeta(
    DisplayName = "轮廓测量",
    Description = "轮廓分析与测量",
    Category = "检测",
    IconName = "contour-measure",
    Keywords = new[] { "轮廓", "面积", "周长", "形状分析", "Contour", "Area", "Perimeter" }
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Area", "面积", PortDataType.Float)]
[OutputPort("Perimeter", "周长", PortDataType.Float)]
[OutputPort("ContourCount", "轮廓数量", PortDataType.Integer)]
[OperatorParam("Threshold", "二值化阈值", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MinArea", "最小面积", "int", DefaultValue = 100, Min = 0)]
[OperatorParam("MaxArea", "最大面积", "int", DefaultValue = 100000, Min = 0)]
[OperatorParam("SortBy", "排序依据", "enum", DefaultValue = "Area", Options = new[] { "Area|面积", "Perimeter|周长" })]
public class ContourMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ContourMeasurement;

    public ContourMeasurementOperator(ILogger<ContourMeasurementOperator> logger) : base(logger)
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

        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var minArea = GetDoubleParam(@operator, "MinArea", 100.0, min: 0);
        var maxArea = GetDoubleParam(@operator, "MaxArea", 100000.0, min: minArea);
        var sortBy = GetStringParam(@operator, "SortBy", "Area");

        var src = imageWrapper.GetMat();
            if (src.Empty())
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
            }

            // 创建结果图像副本用于绘制
            var resultImage = src.Clone();

            // 转换为灰度图
            using var gray = new Mat();
            if (src.Channels() == 1)
            {
                src.CopyTo(gray);
            }
            else
            {
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            }

            // 二值化
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);

            // 查找轮廓
            Cv2.FindContours(binary, out var contours, out var hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            var contourResults = new List<Dictionary<string, object>>();

            for (int i = 0; i < contours.Length; i++)
            {
                var contour = contours[i];
                double area = Cv2.ContourArea(contour);

                // 面积过滤
                if (area < minArea || area > maxArea)
                    continue;

                double perimeter = Cv2.ArcLength(contour, true);
                var boundingRect = Cv2.BoundingRect(contour);
                var moments = Cv2.Moments(contour);

                // 计算中心点（退化轮廓时回退到包围框中心）
                var centerX = Math.Abs(moments.M00) > 1e-9
                    ? moments.M10 / moments.M00
                    : boundingRect.X + boundingRect.Width / 2.0;
                var centerY = Math.Abs(moments.M00) > 1e-9
                    ? moments.M01 / moments.M00
                    : boundingRect.Y + boundingRect.Height / 2.0;

                // 计算圆度
                double circularity = 0;
                if (perimeter > 0)
                {
                    circularity = 4 * Math.PI * area / (perimeter * perimeter);
                }

                // 计算矩形度
                double rectArea = boundingRect.Width * boundingRect.Height;
                double extent = rectArea > 0 ? area / rectArea : 0;

                // 绘制轮廓
                Cv2.DrawContours(resultImage, contours, i, new Scalar(0, 255, 0), 2);
                // 绘制中心点
                Cv2.Circle(resultImage, new Point((int)centerX, (int)centerY), 3, new Scalar(0, 0, 255), -1);
                // 绘制边界框
                Cv2.Rectangle(resultImage, boundingRect, new Scalar(255, 0, 0), 1);

                contourResults.Add(new Dictionary<string, object>
                {
                    { "Index", i },
                    { "Area", area },
                    { "Perimeter", perimeter },
                    { "CenterX", centerX },
                    { "CenterY", centerY },
                    { "BoundingRect", $"{boundingRect.X},{boundingRect.Y},{boundingRect.Width},{boundingRect.Height}" },
                    { "Circularity", circularity },
                    { "Extent", extent }
                });
            }

            // 排序
            if (sortBy == "Area")
            {
                contourResults = contourResults.OrderByDescending(c => (double)c["Area"]).ToList();
            }
            else if (sortBy == "Perimeter")
            {
                contourResults = contourResults.OrderByDescending(c => (double)c["Perimeter"]).ToList();
            }

            // P0: 使用ImageWrapper实现零拷贝输出
            var additionalData = new Dictionary<string, object>
            {
                { "ContourCount", contourResults.Count },
                { "Contours", contourResults }
            };

            var firstContour = contourResults.FirstOrDefault();
            if (firstContour != null)
            {
                foreach (var kvp in firstContour)
                {
                    if (!additionalData.ContainsKey(kvp.Key))
                        additionalData[kvp.Key] = kvp.Value;
                }
            }

            var hasFeature = contourResults.Count > 0;
            additionalData["StatusCode"] = hasFeature ? "OK" : "NoFeature";
            additionalData["StatusMessage"] = hasFeature ? "Success" : "No contour found";
            additionalData["Confidence"] = hasFeature ? 1.0 : 0.0;
            additionalData["UncertaintyPx"] = hasFeature ? 0.2 : double.NaN;

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
        }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        var minArea = GetDoubleParam(@operator, "MinArea", 100.0);
        var maxArea = GetDoubleParam(@operator, "MaxArea", 100000.0);

        if (threshold < 0 || threshold > 255)
            return ValidationResult.Invalid("二值化阈值必须在 0-255 之间");
        if (minArea < 0)
            return ValidationResult.Invalid("最小面积不能为负数");
        if (maxArea < minArea)
            return ValidationResult.Invalid("最大面积必须大于等于最小面积");

        return ValidationResult.Valid();
    }
}
