// DistanceTransformOperator.cs
// 距离变换算子
// 对标 Halcon: distance_transform
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 距离变换算子 - 计算二值图像中每个像素到最近零像素的距离
/// 对标 Halcon distance_transform
/// </summary>
[OperatorMeta(
    DisplayName = "Distance Transform",
    Description = "Computes the distance from each pixel to the nearest zero pixel. Supports multiple distance metrics and signed distances.",
    Category = "Analysis",
    IconName = "distance-transform",
    Keywords = new[] { "Distance", "Transform", "EDT", "Chamfer", "Signed", "Euclidean" }
)]
[InputPort("Image", "Input Image (Binary or Grayscale)", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Distance Transform Result", PortDataType.Image)]
[OutputPort("DistanceMap", "Distance Map (Float)", PortDataType.Any)]
[OutputPort("MaxDistance", "Maximum Distance", PortDataType.Float)]
[OutputPort("MaxLocation", "Maximum Distance Location", PortDataType.Point)]
[OperatorParam("DistanceType", "Distance Type", "enum", DefaultValue = "Euclidean", Options = new[] { "Euclidean|Euclidean", "Manhattan|Manhattan (L1)", "Chessboard|Chessboard", "C|3x3 C", "L12|3x3 L12" })]
[OperatorParam("MaskSize", "Mask Size", "int", DefaultValue = 5, Min = 3, Max = 7)]
[OperatorParam("Signed", "Compute Signed Distance", "bool", DefaultValue = false)]
[OperatorParam("Threshold", "Binary Threshold", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("Invert", "Invert Input", "bool", DefaultValue = false)]
[OperatorParam("Normalize", "Normalize Output", "bool", DefaultValue = false)]
[OperatorParam("MaxDistanceLimit", "Max Distance Limit (0=unlimited)", "double", DefaultValue = 0.0, Min = 0.0, Max = 10000.0)]
public class DistanceTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.DistanceTransform;

    public DistanceTransformOperator(ILogger<DistanceTransformOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 获取参数
        var distanceType = GetStringParam(@operator, "DistanceType", "Euclidean");
        var maskSize = GetIntParam(@operator, "MaskSize", 5, 3, 7);
        var signed = GetBoolParam(@operator, "Signed", false);
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, 0.0, 255.0);
        var invert = GetBoolParam(@operator, "Invert", false);
        var normalize = GetBoolParam(@operator, "Normalize", false);
        var maxDistanceLimit = GetDoubleParam(@operator, "MaxDistanceLimit", 0.0, 0.0, 10000.0);

        // 获取输入图像
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        try
        {
            // 预处理：转换为二值图像
            using var gray = OperatorImageDepthHelper.EnsureSingleChannelGray(src);

            using var binary = new Mat();
            var nativeThreshold = OperatorImageDepthHelper.ResolveThresholdToNativeRange(gray, threshold);
            Cv2.Threshold(gray, binary, nativeThreshold, 255, ThresholdTypes.Binary);

            if (invert)
            {
                Cv2.BitwiseNot(binary, binary);
            }

            // 执行距离变换
            Mat distanceMap;
            if (signed)
            {
                distanceMap = ComputeSignedDistanceTransform(binary, distanceType, maskSize);
            }
            else
            {
                distanceMap = ComputeDistanceTransform(binary, distanceType, maskSize);
            }

            // 限制最大距离
            if (maxDistanceLimit > 0)
            {
                using var mask = new Mat();
                Cv2.Threshold(distanceMap, mask, maxDistanceLimit, maxDistanceLimit, ThresholdTypes.Trunc);
                mask.CopyTo(distanceMap);
            }

            // 找到最大距离位置
            Cv2.MinMaxLoc(distanceMap, out var minVal, out var maxVal, out var minLoc, out var maxLoc);

            // 归一化可视化
            Mat displayImage;
            if (normalize)
            {
                displayImage = new Mat();
                Cv2.Normalize(distanceMap, displayImage, 0, 255, NormTypes.MinMax, MatType.CV_8UC1);
            }
            else
            {
                // 按最大距离缩放显示
                var scale = maxVal > 0 ? 255.0 / maxVal : 1.0;
                displayImage = new Mat();
                distanceMap.ConvertTo(displayImage, MatType.CV_8UC1, scale, 0);
            }

            // 应用颜色映射
            Mat colorResult = displayImage.Clone();
            Cv2.ApplyColorMap(displayImage, colorResult, ColormapTypes.Jet);

            // 绘制最大距离点
            Cv2.Circle(colorResult, maxLoc, 5, new Scalar(255, 255, 255), -1);
            Cv2.Circle(colorResult, maxLoc, 5, new Scalar(0, 0, 0), 2);
            Cv2.PutText(colorResult, $"Max: {maxVal:F1}", new Point(maxLoc.X + 10, maxLoc.Y),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 2);

            stopwatch.Stop();

            // 精度验证报告（针对解析形状）
            var accuracyReport = ValidateDistanceAccuracy(distanceMap, binary, distanceType);

            var resultData = new Dictionary<string, object>
            {
                { "DistanceMap", distanceMap },
                { "MaxDistance", maxVal },
                { "MaxLocation", new { X = maxLoc.X, Y = maxLoc.Y } },
                { "MinDistance", minVal },
                { "DistanceType", distanceType },
                { "IsSigned", signed },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds },
                { "ImageWidth", src.Width },
                { "ImageHeight", src.Height },
                { "MeanDistance", Cv2.Mean(distanceMap).Val0 },
                { "AccuracyReport", accuracyReport },
                { "ThresholdUsed", nativeThreshold },
                { "InputBitDepth", gray.Depth().ToString() }
            };

            displayImage.Dispose();

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(colorResult, resultData)));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Distance transform failed");
            return Task.FromResult(OperatorExecutionOutput.Failure($"Distance transform failed: {ex.Message}"));
        }
    }

    private Mat ComputeDistanceTransform(Mat binary, string distanceType, int maskSize)
    {
        var distType = distanceType.ToLowerInvariant() switch
        {
            "manhattan" or "l1" => DistanceTypes.L1,
            "chessboard" => DistanceTypes.C,
            "c" => DistanceTypes.C,
            "l12" => DistanceTypes.L12,
            _ => DistanceTypes.L2
        };

        var distTransformMask = maskSize switch
        {
            3 => DistanceTransformMasks.Mask3,
            5 => DistanceTransformMasks.Mask5,
            _ => DistanceTransformMasks.Precise
        };

        var result = new Mat();
        Cv2.DistanceTransform(binary, result, distType, distTransformMask);

        return result;
    }

    private Mat ComputeSignedDistanceTransform(Mat binary, string distanceType, int maskSize)
    {
        // 计算前景到背景的距离（正）
        using var distForeground = ComputeDistanceTransform(binary, distanceType, maskSize);

        // 计算背景到前景的距离（负）
        using var inverted = new Mat();
        Cv2.BitwiseNot(binary, inverted);
        using var distBackground = ComputeDistanceTransform(inverted, distanceType, maskSize);

        // 合并：前景区域为正，背景区域为负
        var signedDist = new Mat(binary.Size(), MatType.CV_32FC1);

        for (int y = 0; y < binary.Rows; y++)
        {
            for (int x = 0; x < binary.Cols; x++)
            {
                var isForeground = binary.At<byte>(y, x) > 0;
                var value = isForeground 
                    ? distForeground.At<float>(y, x) 
                    : -distBackground.At<float>(y, x);
                signedDist.Set(y, x, value);
            }
        }

        return signedDist;
    }

    private Dictionary<string, object> ValidateDistanceAccuracy(Mat distanceMap, Mat binary, string distanceType)
    {
        var report = new Dictionary<string, object>
        {
            { "ValidationType", "DistanceTransform" },
            { "DistanceType", distanceType },
            { "Timestamp", DateTime.UtcNow }
        };

        try
        {
            // 针对简单几何形状的验证
            // 寻找连通区域
            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();
            Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids);

            var shapeAnalyses = new List<Dictionary<string, object>>();

            const int StatLeft = 0;
            const int StatTop = 1;
            const int StatWidth = 2;
            const int StatHeight = 3;
            const int StatArea = 4;

            for (int i = 1; i < stats.Rows; i++) // 跳过背景
            {
                var area = stats.At<int>(i, StatArea);
                if (area < 100) continue; // 忽略小区域

                var x = stats.At<int>(i, StatLeft);
                var y = stats.At<int>(i, StatTop);
                var width = stats.At<int>(i, StatWidth);
                var height = stats.At<int>(i, StatHeight);

                // 提取该区域的距离值
                using var roi = new Mat(distanceMap, new Rect(x, y, width, height));
                using var roiMask = new Mat(labels, new Rect(x, y, width, height));
                
                // 计算该区域的距离统计
                var maxDist = 0.0;
                var maxDistCount = 0;

                for (int ry = 0; ry < height; ry++)
                {
                    for (int rx = 0; rx < width; rx++)
                    {
                        if (roiMask.At<int>(ry, rx) == i)
                        {
                            var dist = roi.At<float>(ry, rx);
                            if (Math.Abs(dist - maxDist) < 0.1)
                            {
                                maxDistCount++;
                            }
                            else if (dist > maxDist)
                            {
                                maxDist = dist;
                                maxDistCount = 1;
                            }
                        }
                    }
                }

                // 估算形状类型
                var aspectRatio = (double)width / height;
                var estimatedRadius = maxDist;
                var expectedMaxDist = EstimateExpectedMaxDistance(width, height, aspectRatio, distanceType);
                var error = Math.Abs(maxDist - expectedMaxDist);
                var errorRatio = expectedMaxDist > 0 ? error / expectedMaxDist : 0;

                shapeAnalyses.Add(new Dictionary<string, object>
                {
                    { "ComponentId", i },
                    { "Area", area },
                    { "BoundingBox", new { X = x, Y = y, Width = width, Height = height } },
                    { "AspectRatio", aspectRatio },
                    { "MaxDistance", maxDist },
                    { "ExpectedMaxDistance", expectedMaxDist },
                    { "Error", error },
                    { "ErrorRatio", errorRatio },
                    { "IsWithinTolerance", errorRatio < 0.01 } // 1% 容差
                });
            }

            report["ShapeAnalyses"] = shapeAnalyses;
            report["TotalComponents"] = shapeAnalyses.Count;
            report["ComponentsWithinTolerance"] = shapeAnalyses.Count(s => (bool)s["IsWithinTolerance"]);
            report["AverageError"] = shapeAnalyses.Count > 0 ? shapeAnalyses.Average(s => (double)s["Error"]) : 0;
            report["MaxError"] = shapeAnalyses.Count > 0 ? shapeAnalyses.Max(s => (double)s["Error"]) : 0;
        }
        catch (Exception ex)
        {
            report["ValidationError"] = ex.Message;
        }

        return report;
    }

    private double EstimateExpectedMaxDistance(int width, int height, double aspectRatio, string distanceType)
    {
        // 基于形状几何估算最大距离
        // 圆形/椭圆：半径
        // 矩形：min(width, height) / 2
        
        var minDim = Math.Min(width, height);
        var expectedRadius = minDim / 2.0;

        // 针对不同距离类型的修正
        return distanceType.ToLowerInvariant() switch
        {
            "manhattan" or "l1" => expectedRadius * 0.7, // L1距离通常比L2大
            "chessboard" => expectedRadius, // Chessboard距离与L2接近
            _ => expectedRadius // Euclidean
        };
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var maskSize = GetIntParam(@operator, "MaskSize", 5);
        var validMaskSizes = new[] { 3, 5 };
        if (!validMaskSizes.Contains(maskSize))
        {
            return ValidationResult.Invalid("MaskSize must be 3 or 5 for standard distance transform.");
        }

        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        if (threshold < 0 || threshold > 255)
        {
            return ValidationResult.Invalid("Threshold must be between 0 and 255.");
        }

        return ValidationResult.Valid();
    }
}
