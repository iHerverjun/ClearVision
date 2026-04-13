using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

public sealed class TemplateMatcherConfig
{
    public int PyramidLevels { get; init; } = 3;
    public int AngleRange { get; init; } = 180;
    public int AngleStep { get; init; } = 5;
    public float WeakThreshold { get; init; } = 30.0f;
    public float StrongThreshold { get; init; } = 60.0f;
    public int NumFeatures { get; init; } = 150;
    public int SpreadT { get; init; } = 4;
    public int? MagnitudeThreshold { get; init; }
}

/// <summary>
/// LINEMOD-backed template matcher that adapts to the common shape matcher interface.
/// </summary>
public class TemplateMatcher : IShapeMatcher
{
    private readonly LineModShapeMatcher _lineModMatcher;
    private readonly TemplateMatcherConfig _config;
    private Mat? _trainedTemplate;
    private bool _isTrained;

    public string Name => "TemplateMatcher_LINEMOD";

    public TemplateMatcher(int pyramidLevels = 3, int angleRange = 180, int angleStep = 5)
        : this(new TemplateMatcherConfig
        {
            PyramidLevels = pyramidLevels,
            AngleRange = angleRange,
            AngleStep = angleStep
        })
    {
    }

    public TemplateMatcher(TemplateMatcherConfig config)
    {
        _config = new TemplateMatcherConfig
        {
            PyramidLevels = Math.Clamp(config.PyramidLevels, 1, 5),
            AngleRange = Math.Clamp(config.AngleRange, 0, 180),
            AngleStep = Math.Clamp(config.AngleStep, 1, 45),
            WeakThreshold = config.WeakThreshold,
            StrongThreshold = config.StrongThreshold,
            NumFeatures = Math.Clamp(config.NumFeatures, 1, 8191),
            SpreadT = Math.Clamp(config.SpreadT, 1, 16),
            MagnitudeThreshold = config.MagnitudeThreshold
        };

        _lineModMatcher = new LineModShapeMatcher
        {
            PyramidLevels = _config.PyramidLevels,
            WeakThreshold = _config.WeakThreshold,
            StrongThreshold = _config.StrongThreshold,
            NumFeatures = _config.NumFeatures,
            SpreadT = _config.SpreadT
        };
    }

    public bool Train(Mat template, Rect? roi = null)
    {
        if (template.Empty())
        {
            return false;
        }

        _trainedTemplate?.Dispose();
        _trainedTemplate = roi.HasValue
            ? new Mat(template, roi.Value).Clone()
            : template.Clone();

        try
        {
            var templates = _lineModMatcher.Train(
                _trainedTemplate,
                null,
                _config.AngleRange,
                _config.AngleStep);

            _isTrained = templates.Count > 0;
            return _isTrained;
        }
        catch
        {
            _isTrained = false;
            return false;
        }
    }

    public List<ShapeMatchResult> Match(Mat searchImage, float minScore, int maxMatches)
    {
        var results = new List<ShapeMatchResult>();
        if (!_isTrained || searchImage.Empty())
        {
            return results;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var lineModResults = _lineModMatcher.Match(
                searchImage,
                minScore / 100.0f,
                maxMatches);

            stopwatch.Stop();

            foreach (var match in lineModResults.Where(m => m.IsValid))
            {
                results.Add(new ShapeMatchResult
                {
                    IsValid = true,
                    Position = match.Position,
                    Score = (float)match.Score,
                    Angle = (float)match.Angle,
                    Scale = 1.0f,
                    TemplateWidth = _trainedTemplate?.Width ?? 0,
                    TemplateHeight = _trainedTemplate?.Height ?? 0,
                    MatchTimeMs = stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        { "MatchMode", "Template" },
                        { "Algorithm", "LINEMOD" },
                        { "PyramidLevels", _config.PyramidLevels },
                        { "ScoreScale", "Percent" },
                        { "RawScore", match.Score }
                    }
                });
            }
        }
        catch
        {
            // Preserve legacy behavior and surface an empty match list on algorithm failure.
        }

        return results;
    }

    public Dictionary<string, object> GetConfig()
    {
        var appliedParameters = new Dictionary<string, object>
        {
            { "WeakThreshold", _config.WeakThreshold },
            { "StrongThreshold", _config.StrongThreshold },
            { "NumFeatures", _config.NumFeatures },
            { "SpreadT", _config.SpreadT }
        };

        var legacyParameters = new Dictionary<string, object>
        {
            {
                "MagnitudeThreshold",
                _config.MagnitudeThreshold is null
                    ? "Not provided."
                    : $"Requested value {_config.MagnitudeThreshold.Value} is retained for compatibility only. LINEMOD uses WeakThreshold and StrongThreshold as the active gradient gates."
            }
        };

        return new Dictionary<string, object>
        {
            { "Name", Name },
            { "Mode", "Template" },
            { "PyramidLevels", _config.PyramidLevels },
            { "AngleRange", _config.AngleRange },
            { "AngleStep", _config.AngleStep },
            { "IsTrained", _isTrained },
            { "ScoreScale", "Percent" },
            { "AppliedParameters", appliedParameters },
            { "LegacyParameters", legacyParameters },
            { "Diagnostics", new List<string>
                {
                    "Score is reported in percent and is not re-scaled by the operator.",
                    _config.MagnitudeThreshold is null
                        ? "MagnitudeThreshold not supplied."
                        : "MagnitudeThreshold is a legacy compatibility input. WeakThreshold and StrongThreshold are the canonical LINEMOD thresholds."
                }
            }
        };
    }

    public void Dispose()
    {
        _trainedTemplate?.Dispose();
        _lineModMatcher.Dispose();
    }
}
