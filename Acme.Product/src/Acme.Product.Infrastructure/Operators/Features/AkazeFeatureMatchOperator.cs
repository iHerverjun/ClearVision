using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// AKAZE特征匹配算子 - 基于AKAZE特征的鲁棒模板匹配
/// </summary>
public class AkazeFeatureMatchOperator : FeatureMatchOperatorBase
{
    public override OperatorType OperatorType => OperatorType.AkazeFeatureMatch;

    public AkazeFeatureMatchOperator(ILogger<AkazeFeatureMatchOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 获取输入图像
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 获取参数
        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var threshold = GetDoubleParam(@operator, "Threshold", 0.001, min: 0.0001, max: 0.1);
        var minMatchCount = GetIntParam(@operator, "MinMatchCount", 10, min: 3, max: 100);
        var enableSymmetryTest = GetBoolParam(@operator, "EnableSymmetryTest", true);
        var maxFeatures = GetIntParam(@operator, "MaxFeatures", 500, min: 100, max: 2000);

        // 如果通过输入端口提供了模板图像
        Mat? templateFromInput = null;
        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateFromInput = templateWrapper.GetMat();
        }

        using var srcImage = imageWrapper.GetMat();
        
        // 转换为灰度图
        using var srcGray = new Mat();
        if (srcImage.Channels() > 1)
            Cv2.CvtColor(srcImage, srcGray, ColorConversionCodes.BGR2GRAY);
        else
            srcImage.CopyTo(srcGray);

        // 创建AKAZE检测器
        using var akaze = AKAZE.Create(threshold: (float)threshold);

        // 检测场景图像特征
        KeyPoint[] srcKeyPoints;
        using var srcDescriptors = new Mat();
        akaze.DetectAndCompute(srcGray, null, out srcKeyPoints, srcDescriptors);

        if (srcKeyPoints.Length < 4)
        {
            return Task.FromResult(CreateFailedOutput(srcImage, "场景特征点不足", 0, 0));
        }

        // 获取模板特征
        KeyPoint[]? templateKeyPoints = null;
        Mat? templateDescriptors = null;
        Mat? templateImage = templateFromInput;
        bool shouldDisposeTemplate = false;

        try
        {
            if (templateFromInput != null)
            {
                // 使用输入的模板图像
                using var templateGray = new Mat();
                if (templateFromInput.Channels() > 1)
                    Cv2.CvtColor(templateFromInput, templateGray, ColorConversionCodes.BGR2GRAY);
                else
                    templateFromInput.CopyTo(templateGray);

                akaze.DetectAndCompute(templateGray, null, out templateKeyPoints!, templateDescriptors!);
            }
            else if (!string.IsNullOrEmpty(templatePath))
            {
                // 从缓存或文件加载模板
                var cached = GetOrLoadTemplate(templatePath, img =>
                {
                    KeyPoint[] kpts;
                    Mat descs = new Mat();
                    akaze.DetectAndCompute(img, null, out kpts, descs);
                    return (kpts, descs);
                });

                if (cached.HasValue)
                {
                    (templateImage, templateKeyPoints, templateDescriptors) = cached.Value;
                    shouldDisposeTemplate = true;
                }
            }

            if (templateKeyPoints == null || templateKeyPoints.Length < 4 || templateDescriptors == null || templateDescriptors.Empty())
            {
                return Task.FromResult(CreateFailedOutput(srcImage, "模板特征点不足", 0, 0));
            }

            // 限制特征点数量
            var (filteredKpts, filteredDescs) = FilterFeatures(templateKeyPoints, templateDescriptors, maxFeatures);
            
            if (shouldDisposeTemplate)
                templateDescriptors?.Dispose();
            templateDescriptors = filteredDescs;
            templateKeyPoints = filteredKpts;

            // 匹配描述符
            List<DMatch> goodMatches;
            if (enableSymmetryTest)
            {
                goodMatches = MatchWithSymmetryTest(templateDescriptors, srcDescriptors);
            }
            else
            {
                using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
                var matches = matcher.KnnMatch(templateDescriptors, srcDescriptors, k: 2);
                
                goodMatches = new List<DMatch>();
                foreach (var m in matches)
                {
                    if (m.Length >= 2 && m[0].Distance < 0.75 * m[1].Distance)
                        goodMatches.Add(m[0]);
                }
            }

            // 计算单应性矩阵
            var (homography, inliers) = ComputeHomography(templateKeyPoints, srcKeyPoints, goodMatches);

            // 判断匹配结果
            bool isMatch = inliers >= minMatchCount;
            double inlierRatio = goodMatches.Count > 0 ? (double)inliers / goodMatches.Count : 0;
            
            if (inliers < minMatchCount)
                isMatch = false;
            else if (inlierRatio < 0.25)
                isMatch = false;

            // 创建输出图像
            using var resultImage = srcImage.Clone();
            var boxColor = isMatch ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

            if (isMatch && homography != null && !homography.Empty())
            {
                DrawPerspectiveBox(resultImage, homography, 
                    templateImage?.Width ?? 100, 
                    templateImage?.Height ?? 100, 
                    boxColor);
            }

            // 绘制匹配信息
            var center = new Point(resultImage.Width / 2, resultImage.Height / 2);
            if (goodMatches.Count > 0)
            {
                var bestMatch = goodMatches[0];
                center = new Point((int)srcKeyPoints[bestMatch.TrainIdx].Pt.X, 
                                  (int)srcKeyPoints[bestMatch.TrainIdx].Pt.Y);
                Cv2.DrawMarker(resultImage, center, boxColor, MarkerTypes.Cross, 20, 2);
            }

            string info = $"{(isMatch ? "OK" : "NG")}: Inliers={inliers}/{goodMatches.Count}";
            Cv2.PutText(resultImage, info, new Point(10, 30), 
                HersheyFonts.HersheySimplex, 0.6, boxColor, 2);

            if (!isMatch)
            {
                string reason = inliers < minMatchCount 
                    ? $"内点数不足 ({inliers} < {minMatchCount})"
                    : $"内点比例不足 ({inlierRatio:F2})";
                Cv2.PutText(resultImage, reason, new Point(10, 60), 
                    HersheyFonts.HersheySimplex, 0.6, boxColor, 2);
            }

            homography?.Dispose();

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, new Dictionary<string, object>
            {
                { "IsMatch", isMatch },
                { "Score", inlierRatio },
                { "Inliers", inliers },
                { "TotalMatches", goodMatches.Count },
                { "X", center.X },
                { "Y", center.Y }
            })));
        }
        finally
        {
            if (shouldDisposeTemplate)
            {
                templateImage?.Dispose();
                templateDescriptors?.Dispose();
            }
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 0.001);
        if (threshold < 0.0001 || threshold > 0.1)
        {
            return ValidationResult.Invalid("检测阈值必须在 0.0001-0.1 之间");
        }

        var minMatchCount = GetIntParam(@operator, "MinMatchCount", 10);
        if (minMatchCount < 3 || minMatchCount > 100)
        {
            return ValidationResult.Invalid("最小匹配数必须在 3-100 之间");
        }

        return ValidationResult.Valid();
    }

    private OperatorExecutionOutput CreateFailedOutput(Mat input, string reason, int score, int totalMatches)
    {
        using var output = input.Clone();
        Cv2.PutText(output, $"NG: {reason}", new Point(10, 30), 
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);
        Cv2.PutText(output, $"Score: {score}/{totalMatches}", new Point(10, 60), 
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(output, new Dictionary<string, object>
        {
            { "IsMatch", false },
            { "Score", 0.0 },
            { "Inliers", score },
            { "TotalMatches", totalMatches },
            { "Message", reason },
            { "X", 0 },
            { "Y", 0 }
        }));
    }
}
