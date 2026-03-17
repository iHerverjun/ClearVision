// BlobDetectionOperator.cs
// Blob检测算子 - 检测图像中的连通区域
// 作者：蘅芜君

using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;


using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Blob检测算子 - 检测图像中的连通区域
/// </summary>
[OperatorMeta(
    DisplayName = "Blob分析",
    Description = "连通区域分析",
    Category = "特征提取",
    IconName = "blob",
    Keywords = new[] { "连通域", "缺陷区域", "斑点", "面积提取", "缺陷分析", "Blob", "Connected components" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[InputPort("SourceImage", "Source Image", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "标记图像", PortDataType.Image)]
[OutputPort("Blobs", "Blob数据", PortDataType.Contour)]
[OutputPort("BlobFeatures", "Blob特征", PortDataType.Any)]
[OutputPort("BlobCount", "Blob数量", PortDataType.Integer)]
[OperatorParam("MinArea", "最小面积", "int", DefaultValue = 100, Min = 0)]
[OperatorParam("MaxArea", "最大面积", "int", DefaultValue = 100000, Min = 0)]
[OperatorParam("Color", "目标颜色", "enum", DefaultValue = "White", Options = new[] { "White|白色", "Black|黑色" })]
[OperatorParam("MinCircularity", "最小圆度", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("MinConvexity", "最小凸度", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("MinInertiaRatio", "最小惯性比", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("MinRectangularity", "最小矩形度", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("MinEccentricity", "最小离心率", "double", DefaultValue = 0.0, Min = 0.0, Max = 1.0)]
[OperatorParam("OutputDetailedFeatures", "输出详细特征", "bool", DefaultValue = false)]
[OperatorParam("FeatureFilter", "Feature Filter", "string", DefaultValue = "")]
[OperatorParam("EnableColorFilter", "启用颜色过滤", "bool", DefaultValue = false, Description = "启用HSV颜色范围预过滤")]
[OperatorParam("HueLow", "色相下限", "int", DefaultValue = 0, Min = 0, Max = 180)]
[OperatorParam("HueHigh", "色相上限", "int", DefaultValue = 180, Min = 0, Max = 180)]
[OperatorParam("SatLow", "饱和度下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("SatHigh", "饱和度上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
[OperatorParam("ValLow", "明度下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("ValHigh", "明度上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
public class BlobDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BlobAnalysis;

    public BlobDetectionOperator(ILogger<BlobDetectionOperator> logger) : base(logger)
    {
    }

    private Task<OperatorExecutionOutput> ExecuteCoreAsync_Legacy(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var minArea = GetFloatParam(@operator, "MinArea", 100f, min: 0);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f, min: 0);
        var color = GetStringParam(@operator, "Color", "White");
        var minCircularity = GetDoubleParam(@operator, "MinCircularity", 0.0, min: 0, max: 1.0);
        var minConvexity = GetDoubleParam(@operator, "MinConvexity", 0.0, min: 0, max: 1.0);
        var minInertiaRatio = GetDoubleParam(@operator, "MinInertiaRatio", 0.0, min: 0, max: 1.0);
        var enableColorFilter = GetBoolParam(@operator, "EnableColorFilter", false);
        var hueLow = GetIntParam(@operator, "HueLow", 0, 0, 180);
        var hueHigh = GetIntParam(@operator, "HueHigh", 180, 0, 180);
        var satLow = GetIntParam(@operator, "SatLow", 50, 0, 255);
        var satHigh = GetIntParam(@operator, "SatHigh", 255, 0, 255);
        var valLow = GetIntParam(@operator, "ValLow", 50, 0, 255);
        var valHigh = GetIntParam(@operator, "ValHigh", 255, 0, 255);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }
        
        // 颜色预过滤
        Mat processedSrc = src;
        Mat? colorMask = null;
        if (enableColorFilter)
        {
            colorMask = ApplyColorFilter(src, hueLow, hueHigh, satLow, satHigh, valLow, valHigh);
            if (colorMask != null)
            {
                // 应用掩码到原图
                processedSrc = new Mat();
                Cv2.BitwiseAnd(src, src, processedSrc, colorMask);
            }
        }

        // SimpleBlobDetector 内部会自动处理灰度转换，支持彩色和灰度输入
        var detector = new SimpleBlobDetector.Params();
        detector.FilterByArea = true;
        detector.MinArea = minArea;
        detector.MaxArea = maxArea;
        detector.FilterByColor = true;
        detector.BlobColor = color.Equals("Black", StringComparison.OrdinalIgnoreCase) ? (byte)0 : (byte)255;

        if (minCircularity > 0)
        {
            detector.FilterByCircularity = true;
            detector.MinCircularity = (float)minCircularity;
        }

        if (minConvexity > 0)
        {
            detector.FilterByConvexity = true;
            detector.MinConvexity = (float)minConvexity;
        }

        if (minInertiaRatio > 0)
        {
            detector.FilterByInertia = true;
            detector.MinInertiaRatio = (float)minInertiaRatio;
        }

        using var blobDetector = SimpleBlobDetector.Create(detector);
        var keypoints = blobDetector.Detect(processedSrc);

        // 准备彩色结果图（用于绘制彩色标注）
        var colorSrc = new Mat();
        if (processedSrc.Channels() == 1)
            Cv2.CvtColor(processedSrc, colorSrc, ColorConversionCodes.GRAY2BGR);
        else
            processedSrc.CopyTo(colorSrc);

        foreach (var kp in keypoints)
        {
            Cv2.Circle(colorSrc, (int)kp.Pt.X, (int)kp.Pt.Y, (int)kp.Size / 2, new Scalar(0, 255, 0), 2);
            Cv2.Circle(colorSrc, (int)kp.Pt.X, (int)kp.Pt.Y, 3, new Scalar(0, 0, 255), -1);
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "BlobCount", keypoints.Length },
            { "Blobs", keypoints.Select(kp => new Dictionary<string, object>
            {
                { "X", kp.Pt.X },
                { "Y", kp.Pt.Y },
                { "Size", kp.Size },
                { "Area", Math.PI * Math.Pow(kp.Size / 2, 2) }
            }).ToList() }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(colorSrc, additionalData)));
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        ImageWrapper? sourceWrapper = null;
        TryGetInputImage(inputs, "SourceImage", out sourceWrapper);

        var minArea = GetFloatParam(@operator, "MinArea", 100f, min: 0);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f, min: 0);
        var color = GetStringParam(@operator, "Color", "White");
        var minCircularity = GetDoubleParam(@operator, "MinCircularity", 0.0, min: 0, max: 1.0);
        var minConvexity = GetDoubleParam(@operator, "MinConvexity", 0.0, min: 0, max: 1.0);
        var minInertiaRatio = GetDoubleParam(@operator, "MinInertiaRatio", 0.0, min: 0, max: 1.0);
        var minRectangularity = GetDoubleParam(@operator, "MinRectangularity", 0.0, min: 0, max: 1.0);
        var minEccentricity = GetDoubleParam(@operator, "MinEccentricity", 0.0, min: 0, max: 1.0);
        var outputDetailedFeatures = GetBoolParam(@operator, "OutputDetailedFeatures", false);
        var featureFilter = GetStringParam(@operator, "FeatureFilter", string.Empty);
        var enableColorFilter = GetBoolParam(@operator, "EnableColorFilter", false);
        var hueLow = GetIntParam(@operator, "HueLow", 0, 0, 180);
        var hueHigh = GetIntParam(@operator, "HueHigh", 180, 0, 180);
        var satLow = GetIntParam(@operator, "SatLow", 50, 0, 255);
        var satHigh = GetIntParam(@operator, "SatHigh", 255, 0, 255);
        var valLow = GetIntParam(@operator, "ValLow", 50, 0, 255);
        var valHigh = GetIntParam(@operator, "ValHigh", 255, 0, 255);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var sourceMat = sourceWrapper?.GetMat();
        if (sourceMat == null || sourceMat.Empty())
        {
            sourceMat = src;
        }

        Mat? graySource = null;
        Mat? colorMask = null;

        try
        {
            if (sourceMat != null && !sourceMat.Empty())
            {
                graySource = new Mat();
                if (sourceMat.Channels() == 1)
                {
                    sourceMat.CopyTo(graySource);
                }
                else
                {
                    Cv2.CvtColor(sourceMat, graySource, ColorConversionCodes.BGR2GRAY);
                }
            }

            using var gray = new Mat();
            if (src.Channels() == 1)
            {
                src.CopyTo(gray);
            }
            else
            {
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            }

            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary);

            if (color.Equals("Black", StringComparison.OrdinalIgnoreCase))
            {
                Cv2.BitwiseNot(binary, binary);
            }

            if (enableColorFilter)
            {
                colorMask = ApplyColorFilter(sourceMat ?? src, hueLow, hueHigh, satLow, satHigh, valLow, valHigh);
                if (colorMask != null && colorMask.Size() == binary.Size())
                {
                    Cv2.BitwiseAnd(binary, colorMask, binary);
                }
            }

            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();
            var labelCount = Cv2.ConnectedComponentsWithStats(binary, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            var resultImage = new Mat();
            if (sourceMat.Channels() == 1)
            {
                Cv2.CvtColor(sourceMat, resultImage, ColorConversionCodes.GRAY2BGR);
            }
            else
            {
                sourceMat.CopyTo(resultImage);
            }

            var blobs = new List<Dictionary<string, object>>();
            var nextId = 1;
            string? filterError = null;

            for (var label = 1; label < labelCount; label++)
            {
                var area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);
                if (area < minArea || area > maxArea)
                {
                    continue;
                }

                var left = stats.At<int>(label, (int)ConnectedComponentsTypes.Left);
                var top = stats.At<int>(label, (int)ConnectedComponentsTypes.Top);
                var width = stats.At<int>(label, (int)ConnectedComponentsTypes.Width);
                var height = stats.At<int>(label, (int)ConnectedComponentsTypes.Height);

                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                var rect = new Rect(left, top, width, height);

                using var labelRoi = new Mat(labels, rect);
                using var mask = new Mat();
                Cv2.Compare(labelRoi, label, mask, CmpType.EQ);

                Cv2.FindContours(mask, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);
                if (contours.Length == 0)
                {
                    continue;
                }

                var externalIndex = FindExternalContourIndex(hierarchy);
                if (externalIndex < 0 || externalIndex >= contours.Length)
                {
                    externalIndex = 0;
                }

                var contour = contours[externalIndex];
                if (contour.Length < 3)
                {
                    continue;
                }

                var perimeter = Math.Max(1e-6, Cv2.ArcLength(contour, true));
                var hull = Cv2.ConvexHull(contour);
                var hullArea = hull.Length >= 3 ? Math.Abs(Cv2.ContourArea(hull)) : 0.0;
                var convexity = hullArea > 0 ? area / hullArea : 0.0;

                var rectArea = (double)width * height;
                var rectangularity = rectArea > 0 ? area / rectArea : 0.0;

                var circularity = area > 0 ? 4 * Math.PI * area / (perimeter * perimeter) : 0.0;

                var moments = Cv2.Moments(mask, true);
                var (eccentricity, inertiaRatio) = ComputeEccentricityAndInertia(moments);

                var holeCount = CountHoles(hierarchy, externalIndex);
                var eulerNumber = 1 - holeCount;

                var centerX = centroids.At<double>(label, 0);
                var centerY = centroids.At<double>(label, 1);

                var meanGray = 0.0;
                var grayDeviation = 0.0;
                if (graySource != null && !graySource.Empty() &&
                    rect.X >= 0 && rect.Y >= 0 &&
                    rect.X + rect.Width <= graySource.Width &&
                    rect.Y + rect.Height <= graySource.Height)
                {
                    using var grayRoi = new Mat(graySource, rect);
                    Cv2.MeanStdDev(grayRoi, out Scalar mean, out Scalar stddev, mask);
                    meanGray = mean.Val0;
                    grayDeviation = stddev.Val0;
                }

                if (minCircularity > 0 && circularity < minCircularity)
                {
                    continue;
                }

                if (minConvexity > 0 && convexity < minConvexity)
                {
                    continue;
                }

                if (minInertiaRatio > 0 && inertiaRatio < minInertiaRatio)
                {
                    continue;
                }

                if (minRectangularity > 0 && rectangularity < minRectangularity)
                {
                    continue;
                }

                if (minEccentricity > 0 && eccentricity < minEccentricity)
                {
                    continue;
                }

                var featureValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Area"] = area,
                    ["Perimeter"] = perimeter,
                    ["Circularity"] = circularity,
                    ["Convexity"] = convexity,
                    ["Rectangularity"] = rectangularity,
                    ["Eccentricity"] = eccentricity,
                    ["EulerNumber"] = eulerNumber,
                    ["MeanGray"] = meanGray,
                    ["GrayDeviation"] = grayDeviation,
                    ["Width"] = width,
                    ["Height"] = height,
                    ["X"] = left,
                    ["Y"] = top,
                    ["CenterX"] = centerX,
                    ["CenterY"] = centerY,
                    ["InertiaRatio"] = inertiaRatio,
                    ["ConvexHullArea"] = hullArea,
                    ["HoleCount"] = holeCount
                };

                if (!string.IsNullOrWhiteSpace(featureFilter))
                {
                    if (!TryEvaluateFeatureFilter(featureFilter, featureValues, out var passed, out var errorMessage))
                    {
                        filterError = errorMessage;
                        break;
                    }

                    if (!passed)
                    {
                        continue;
                    }
                }

                var blobInfo = new Dictionary<string, object>
                {
                    { "Id", nextId++ },
                    { "Area", area },
                    { "Perimeter", perimeter },
                    { "Circularity", circularity },
                    { "Convexity", convexity },
                    { "Rectangularity", rectangularity },
                    { "Eccentricity", eccentricity },
                    { "EulerNumber", eulerNumber },
                    { "MeanGray", meanGray },
                    { "GrayDeviation", grayDeviation },
                    { "X", left },
                    { "Y", top },
                    { "Width", width },
                    { "Height", height },
                    { "CenterX", centerX },
                    { "CenterY", centerY },
                    { "InertiaRatio", inertiaRatio },
                    { "ConvexHullArea", hullArea },
                    { "HoleCount", holeCount }
                };

                blobs.Add(blobInfo);

                var offsetContour = contour.Select(p => new Point(p.X + rect.X, p.Y + rect.Y)).ToArray();
                Cv2.DrawContours(resultImage, new[] { offsetContour }, -1, new Scalar(0, 255, 0), 2);
                Cv2.Circle(resultImage, (int)Math.Round(centerX), (int)Math.Round(centerY), 3, new Scalar(0, 0, 255), -1);
            }

            if (!string.IsNullOrWhiteSpace(filterError))
            {
                resultImage.Dispose();
                return Task.FromResult(OperatorExecutionOutput.Failure($"FeatureFilter invalid: {filterError}"));
            }

            var additionalData = new Dictionary<string, object>
            {
                { "BlobCount", blobs.Count },
                { "Blobs", blobs }
            };

            if (outputDetailedFeatures)
            {
                additionalData["BlobFeatures"] = blobs;
            }

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
        }
        finally
        {
            graySource?.Dispose();
            colorMask?.Dispose();
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minArea = GetFloatParam(@operator, "MinArea", 100f);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f);

        if (minArea < 0 || maxArea < 0)
        {
            return ValidationResult.Invalid("面积范围不能为负数");
        }

        if (minArea >= maxArea)
        {
            return ValidationResult.Invalid("最小面积必须小于最大面积");
        }

        var color = GetStringParam(@operator, "Color", "White");
        var validColors = new[] { "White", "Black" };
        if (!validColors.Contains(color, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Color must be White or Black.");
        }

        return ValidationResult.Valid();
    }

    private static (double eccentricity, double inertiaRatio) ComputeEccentricityAndInertia(Moments moments)
    {
        var m00 = moments.M00;
        if (m00 <= 0)
        {
            return (0, 0);
        }

        var a = moments.Mu20 / m00;
        var b = 2 * moments.Mu11 / m00;
        var c = moments.Mu02 / m00;
        var temp = Math.Sqrt(Math.Max(0, (a - c) * (a - c) + b * b));

        var lambda1 = (a + c + temp) / 2.0;
        var lambda2 = (a + c - temp) / 2.0;

        if (lambda1 <= 1e-12)
        {
            return (0, 0);
        }

        var inertiaRatio = lambda2 / lambda1;
        if (inertiaRatio < 0)
        {
            inertiaRatio = 0;
        }

        var eccentricity = Math.Sqrt(Math.Max(0, 1 - inertiaRatio));
        return (eccentricity, inertiaRatio);
    }

    private static int FindExternalContourIndex(HierarchyIndex[] hierarchy)
    {
        if (hierarchy == null || hierarchy.Length == 0)
        {
            return -1;
        }

        for (var i = 0; i < hierarchy.Length; i++)
        {
            if (hierarchy[i].Parent < 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int CountHoles(HierarchyIndex[] hierarchy, int externalIndex)
    {
        if (hierarchy == null || hierarchy.Length == 0 || externalIndex < 0)
        {
            return 0;
        }

        var holes = 0;
        for (var i = 0; i < hierarchy.Length; i++)
        {
            if (hierarchy[i].Parent == externalIndex)
            {
                holes++;
            }
        }

        return holes;
    }

    private static bool TryEvaluateFeatureFilter(
        string filter,
        IReadOnlyDictionary<string, double> values,
        out bool passed,
        out string? errorMessage)
    {
        passed = true;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        var normalized = NormalizeFilter(filter);
        var expression = ReplaceFeatureTokens(normalized, values);

        try
        {
            using var table = new DataTable();
            var result = table.Compute(expression, null);

            if (result is bool booleanResult)
            {
                passed = booleanResult;
                return true;
            }

            if (result is IConvertible)
            {
                var numeric = Convert.ToDouble(result, CultureInfo.InvariantCulture);
                passed = Math.Abs(numeric) > 1e-12;
                return true;
            }

            passed = false;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            passed = false;
            return false;
        }
    }

    private static string NormalizeFilter(string filter)
    {
        var normalized = filter.Trim();
        normalized = normalized.Replace("&&", " AND ", StringComparison.Ordinal);
        normalized = normalized.Replace("||", " OR ", StringComparison.Ordinal);
        normalized = Regex.Replace(normalized, "==", "=", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, "!=", "<>", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bAND\b", "AND", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bOR\b", "OR", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\bNOT\b", "NOT", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return normalized;
    }

    private static string ReplaceFeatureTokens(string expression, IReadOnlyDictionary<string, double> values)
    {
        var result = expression;
        foreach (var (key, value) in values)
        {
            var pattern = $@"\b{Regex.Escape(key)}\b";
            result = Regex.Replace(
                result,
                pattern,
                value.ToString(CultureInfo.InvariantCulture),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    /// <summary>
    /// 应用HSV颜色范围过滤
    /// </summary>
    private Mat? ApplyColorFilter(Mat src, int hueLow, int hueHigh, int satLow, int satHigh, int valLow, int valHigh)
    {
        try
        {
            using var hsv = new Mat();
            if (src.Channels() == 3)
            {
                Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
            }
            else if (src.Channels() == 1)
            {
                // 灰度图无法应用HSV过滤，返回空掩码
                return null;
            }
            else
            {
                return null;
            }

            // 创建HSV范围掩码
            var lower = new Scalar(hueLow, satLow, valLow);
            var upper = new Scalar(hueHigh, satHigh, valHigh);
            var mask = new Mat();
            Cv2.InRange(hsv, lower, upper, mask);

            return mask;
        }
        catch
        {
            return null;
        }
    }
}
