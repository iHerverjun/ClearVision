using System.Security.Cryptography;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "梯度形状匹配",
    Description = "基于梯度方向特征的形状匹配，支持可选 ROI 搜索。",
    Category = "匹配定位",
    IconName = "shape-match"
)]
[InputPort("Image", "搜索图像", PortDataType.Image, IsRequired = true)]
[InputPort("Template", "模板图像", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("Position", "匹配位置", PortDataType.Point)]
[OutputPort("Angle", "旋转角度", PortDataType.Float)]
[OutputPort("IsMatch", "是否匹配", PortDataType.Boolean)]
[OutputPort("Score", "匹配分数", PortDataType.Float)]
[OperatorParam("TemplatePath", "模板路径", "file", DefaultValue = "")]
[OperatorParam("MinScore", "最小分数(%)", "double", DefaultValue = 80.0, Min = 0.0, Max = 100.0)]
[OperatorParam("AngleRange", "角度范围(度)", "int", DefaultValue = 180, Min = 0, Max = 180)]
[OperatorParam("AngleStep", "角度步长", "int", DefaultValue = 1, Min = 1, Max = 10)]
[OperatorParam("MagnitudeThreshold", "梯度阈值", "int", DefaultValue = 30, Min = 0, Max = 255)]
[OperatorParam("EnableCache", "启用缓存", "bool", DefaultValue = true)]
[OperatorParam("UseRoi", "使用 ROI", "bool", DefaultValue = false)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0, Min = 0, Max = 100000)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0, Min = 0, Max = 100000)]
[OperatorParam("RoiWidth", "ROI Width", "int", DefaultValue = 0, Min = 0, Max = 100000)]
[OperatorParam("RoiHeight", "ROI Height", "int", DefaultValue = 0, Min = 0, Max = 100000)]
public class GradientShapeMatchOperator : OperatorBase
{
    private const int MaxMatcherCacheEntries = 8;
    private readonly Dictionary<string, MatcherCacheEntry> _matcherCache = new();
    private readonly LinkedList<string> _matcherCacheLru = new();
    private readonly object _cacheLock = new();

    public override OperatorType OperatorType => OperatorType.GradientShapeMatch;

    public GradientShapeMatchOperator(ILogger<GradientShapeMatchOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var templatePath = GetStringParam(@operator, "TemplatePath", "");
        var minScore = GetDoubleParam(@operator, "MinScore", 80.0, min: 0.0, max: 100.0);
        var angleRange = GetIntParam(@operator, "AngleRange", 180, min: 0, max: 180);
        var angleStep = GetIntParam(@operator, "AngleStep", 1, min: 1, max: 10);
        var magnitudeThreshold = GetIntParam(@operator, "MagnitudeThreshold", 30, min: 0, max: 255);
        var enableCache = GetBoolParam(@operator, "EnableCache", true);
        var useRoi = GetBoolParam(@operator, "UseRoi", false);
        var roiX = GetIntParam(@operator, "RoiX", 0, min: 0);
        var roiY = GetIntParam(@operator, "RoiY", 0, min: 0);
        var roiWidth = GetIntParam(@operator, "RoiWidth", 0, min: 0);
        var roiHeight = GetIntParam(@operator, "RoiHeight", 0, min: 0);

        Mat? templateFromInput = null;
        if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
        {
            templateFromInput = templateWrapper.GetMat();
        }

        var srcImage = imageWrapper.GetMat();
        var searchRegion = BuildSearchRegion(useRoi, roiX, roiY, roiWidth, roiHeight, srcImage.Width, srcImage.Height);

        try
        {
            var cacheKey = BuildCacheKey(templatePath, templateFromInput, angleRange, angleStep, magnitudeThreshold);
            var lease = GetOrCreateMatcher(cacheKey, enableCache, templatePath, templateFromInput, angleRange, angleStep, magnitudeThreshold);
            if (lease == null)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("未提供模板图像或路径"));
            }

            try
            {
                var result = lease.Entry.Matcher.Match(srcImage, minScore, searchRegion);
                var resultImage = srcImage.Clone();
                var boxColor = result.IsValid ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

                if (result.IsValid)
                {
                    var halfWidth = Math.Max(1, lease.Entry.TemplateWidth / 2);
                    var halfHeight = Math.Max(1, lease.Entry.TemplateHeight / 2);
                    Cv2.Rectangle(
                        resultImage,
                        new Point(result.Position.X - halfWidth, result.Position.Y - halfHeight),
                        new Point(result.Position.X + halfWidth, result.Position.Y + halfHeight),
                        boxColor,
                        2);
                    Cv2.DrawMarker(resultImage, result.Position, boxColor, MarkerTypes.Cross, 20, 2);
                }

                Cv2.PutText(resultImage, $"{(result.IsValid ? "OK" : "NG")}: Score={result.Score:F1}%", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, boxColor, 2);

                if (result.IsValid)
                {
                    Cv2.PutText(
                        resultImage,
                        $"Angle: {result.Angle:F1}deg",
                        new Point(result.Position.X - Math.Max(30, lease.Entry.TemplateWidth / 2), result.Position.Y - Math.Max(20, lease.Entry.TemplateHeight / 2) - 5),
                        HersheyFonts.HersheySimplex,
                        0.5,
                        boxColor,
                        1);
                }

                var position = new Position(result.Position.X, result.Position.Y);
                var output = new Dictionary<string, object>
                {
                    ["IsMatch"] = result.IsValid,
                    ["Score"] = result.Score,
                    ["Position"] = position,
                    ["X"] = position.X,
                    ["Y"] = position.Y,
                    ["Angle"] = result.Angle,
                    ["TemplateWidth"] = lease.Entry.TemplateWidth,
                    ["TemplateHeight"] = lease.Entry.TemplateHeight,
                    ["DisplayWidth"] = lease.Entry.TemplateWidth,
                    ["DisplayHeight"] = lease.Entry.TemplateHeight,
                    ["CacheEnabled"] = enableCache,
                    ["SearchRegion"] = new Dictionary<string, object>
                    {
                        ["Enabled"] = useRoi,
                        ["X"] = searchRegion.X,
                        ["Y"] = searchRegion.Y,
                        ["Width"] = searchRegion.Width,
                        ["Height"] = searchRegion.Height
                    }
                };

                if (!result.IsValid)
                {
                    output["Message"] = "No gradient shape match above threshold.";
                }

                return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
            }
            finally
            {
                if (!lease.FromCache)
                {
                    lease.Entry.Matcher.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"梯度形状匹配失败: {ex.Message}"));
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minScore = GetDoubleParam(@operator, "MinScore", 80.0);
        if (minScore < 0 || minScore > 100)
        {
            return ValidationResult.Invalid("最小分数必须在 0-100 之间");
        }

        var angleRange = GetIntParam(@operator, "AngleRange", 180);
        if (angleRange < 0 || angleRange > 180)
        {
            return ValidationResult.Invalid("角度范围必须在 0-180 之间");
        }

        if (GetBoolParam(@operator, "UseRoi", false))
        {
            var roiWidth = GetIntParam(@operator, "RoiWidth", 0);
            var roiHeight = GetIntParam(@operator, "RoiHeight", 0);
            if (roiWidth <= 0 || roiHeight <= 0)
            {
                return ValidationResult.Invalid("启用 ROI 时，RoiWidth 和 RoiHeight 必须大于 0");
            }
        }

        return ValidationResult.Valid();
    }

    private MatcherLease? GetOrCreateMatcher(
        string cacheKey,
        bool enableCache,
        string templatePath,
        Mat? templateFromInput,
        int angleRange,
        int angleStep,
        int magnitudeThreshold)
    {
        if (enableCache && TryGetCachedMatcher(cacheKey, out var cached))
        {
            return new MatcherLease(cached!, true);
        }

        Mat? templateImage = null;
        var shouldDispose = false;
        GradientShapeMatcher? matcher = null;
        try
        {
            if (templateFromInput != null)
            {
                templateImage = templateFromInput;
            }
            else if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
            {
                templateImage = Cv2.ImRead(templatePath, ImreadModes.Color);
                shouldDispose = true;
            }

            if (templateImage == null || templateImage.Empty())
            {
                return null;
            }

            matcher = new GradientShapeMatcher(magnitudeThreshold, angleStep);
            matcher.Train(templateImage, angleRange);

            var entry = new MatcherCacheEntry(matcher, templateImage.Width, templateImage.Height);
            if (enableCache)
            {
                AddOrUpdateCache(cacheKey, entry);
                return new MatcherLease(entry, true);
            }

            return new MatcherLease(entry, false);
        }
        catch
        {
            matcher?.Dispose();
            throw;
        }
        finally
        {
            if (shouldDispose)
            {
                templateImage?.Dispose();
            }
        }
    }

    private static Rect BuildSearchRegion(bool useRoi, int roiX, int roiY, int roiWidth, int roiHeight, int imageWidth, int imageHeight)
    {
        if (!useRoi)
        {
            return new Rect(0, 0, imageWidth, imageHeight);
        }

        var x = Math.Clamp(roiX, 0, imageWidth);
        var y = Math.Clamp(roiY, 0, imageHeight);
        var width = Math.Clamp(roiWidth, 0, imageWidth - x);
        var height = Math.Clamp(roiHeight, 0, imageHeight - y);
        return new Rect(x, y, width, height);
    }

    private static string BuildCacheKey(string templatePath, Mat? templateFromInput, int angleRange, int angleStep, int magnitudeThreshold)
    {
        if (templateFromInput != null && !templateFromInput.Empty())
        {
            var encoded = templateFromInput.ToBytes(".png");
            var hash = Convert.ToHexString(SHA256.HashData(encoded));
            return $"input:{hash}:{angleRange}:{angleStep}:{magnitudeThreshold}";
        }

        return $"path:{templatePath}:{angleRange}:{angleStep}:{magnitudeThreshold}";
    }

    private bool TryGetCachedMatcher(string cacheKey, out MatcherCacheEntry? entry)
    {
        lock (_cacheLock)
        {
            if (_matcherCache.TryGetValue(cacheKey, out var cached))
            {
                TouchCacheKey(cacheKey);
                entry = cached;
                return true;
            }
        }

        entry = null;
        return false;
    }

    private void AddOrUpdateCache(string cacheKey, MatcherCacheEntry entry)
    {
        lock (_cacheLock)
        {
            if (_matcherCache.TryGetValue(cacheKey, out var existing))
            {
                existing.Matcher.Dispose();
                _matcherCache[cacheKey] = entry;
                TouchCacheKey(cacheKey);
                return;
            }

            _matcherCache[cacheKey] = entry;
            _matcherCacheLru.AddFirst(cacheKey);

            while (_matcherCache.Count > MaxMatcherCacheEntries && _matcherCacheLru.Last != null)
            {
                var evictKey = _matcherCacheLru.Last.Value;
                _matcherCacheLru.RemoveLast();
                if (_matcherCache.Remove(evictKey, out var evicted))
                {
                    evicted.Matcher.Dispose();
                }
            }
        }
    }

    private void TouchCacheKey(string cacheKey)
    {
        var node = _matcherCacheLru.Find(cacheKey);
        if (node == null)
        {
            _matcherCacheLru.AddFirst(cacheKey);
            return;
        }

        if (node != _matcherCacheLru.First)
        {
            _matcherCacheLru.Remove(node);
            _matcherCacheLru.AddFirst(node);
        }
    }

    private sealed record MatcherCacheEntry(GradientShapeMatcher Matcher, int TemplateWidth, int TemplateHeight);

    private sealed record MatcherLease(MatcherCacheEntry Entry, bool FromCache);
}
