// ParameterRecommender.cs
// 参数推荐器
// 根据算子类型与上下文推荐默认参数配置
// 作者：蘅芜君
using Acme.Product.Core.Enums;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// Recommends operator parameters based on input image statistics.
/// </summary>
public sealed class ParameterRecommender
{
    public Dictionary<string, object> Recommend(OperatorType type, Mat inputImage)
    {
        if (inputImage == null || inputImage.Empty())
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        return type switch
        {
            OperatorType.Thresholding => RecommendThresholding(inputImage),
            OperatorType.Filtering or OperatorType.GaussianBlur => RecommendFiltering(inputImage),
            OperatorType.BlobAnalysis => RecommendBlobAnalysis(inputImage),
            OperatorType.SharpnessEvaluation => RecommendSharpnessEvaluation(inputImage),
            _ => new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static Dictionary<string, object> RecommendThresholding(Mat source)
    {
        using var gray = CreateOwnedGrayMat(source);
        using var binary = new Mat();

        var otsuThreshold = Cv2.Threshold(
            gray,
            binary,
            0,
            255,
            ThresholdTypes.Binary | ThresholdTypes.Otsu);

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Threshold"] = Math.Round(otsuThreshold, 2),
            ["MaxValue"] = 255.0,
            ["Type"] = (int)ThresholdTypes.Binary,
            ["UseOtsu"] = true
        };
    }

    private static Dictionary<string, object> RecommendFiltering(Mat source)
    {
        using var gray = CreateOwnedGrayMat(source);
        using var laplacian = new Mat();

        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var stddev);

        var noiseScore = stddev.Val0;

        var kernelSize = noiseScore switch
        {
            > 50 => 9,
            > 30 => 7,
            > 15 => 5,
            _ => 3
        };

        var sigmaX = Math.Round(Math.Clamp(noiseScore / 25.0, 0.8, 3.5), 2);

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["KernelSize"] = kernelSize,
            ["SigmaX"] = sigmaX,
            ["SigmaY"] = 0.0,
            ["BorderType"] = 4
        };
    }

    private static Dictionary<string, object> RecommendBlobAnalysis(Mat source)
    {
        using var gray = CreateOwnedGrayMat(source);
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        Cv2.FindContours(
            binary,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var areas = contours
            .Select(contour => Cv2.ContourArea(contour))
            .Where(area => area > 1)
            .OrderBy(area => area)
            .ToArray();

        var whiteRatio = Cv2.CountNonZero(binary) / (double)(binary.Rows * binary.Cols);
        var targetColor = whiteRatio <= 0.5 ? "White" : "Black";

        if (areas.Length == 0)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["MinArea"] = 10,
                ["MaxArea"] = 100000,
                ["Color"] = targetColor
            };
        }

        var p20 = Percentile(areas, 0.20);
        var p95 = Percentile(areas, 0.95);
        var minArea = Math.Max(1, (int)Math.Round(p20 * 0.6));
        var maxArea = Math.Max(minArea + 1, (int)Math.Round(p95 * 1.4));

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["MinArea"] = minArea,
            ["MaxArea"] = maxArea,
            ["Color"] = targetColor
        };
    }

    private static Dictionary<string, object> RecommendSharpnessEvaluation(Mat source)
    {
        using var gray = CreateOwnedGrayMat(source);
        using var laplacian = new Mat();

        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var stddev);
        var score = stddev.Val0 * stddev.Val0;
        var threshold = Math.Clamp(score * 0.8, 20.0, 5000.0);

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Method"] = "Laplacian",
            ["ThresholdMode"] = "Manual",
            ["Threshold"] = Math.Round(threshold, 2),
            ["RoiX"] = 0,
            ["RoiY"] = 0,
            ["RoiW"] = 0,
            ["RoiH"] = 0
        };
    }

    /// <summary>
    /// Always returns a new Mat owned by the caller, even when the source is already grayscale.
    /// </summary>
    private static Mat CreateOwnedGrayMat(Mat source)
    {
        if (source.Channels() == 1)
        {
            return source.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Clamp(index, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }
}
