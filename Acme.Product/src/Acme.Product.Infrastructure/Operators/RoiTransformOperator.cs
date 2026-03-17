using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Converts an upstream match pose (position/angle/scale) into a tracked ROI rectangle.
/// Intended to "bridge" shape matching output with downstream measurement operators (e.g. CaliperTool).
/// </summary>
[OperatorMeta(
    DisplayName = "ROI跟踪",
    Description = "Transforms a base ROI using match pose (CenterX/CenterY/Angle/Scale) and outputs SearchRegion.",
    Category = "辅助",
    IconName = "roi-track",
    Keywords = new[] { "ROI", "track", "transform", "match", "pose", "SearchRegion" }
)]
[InputPort("BaseRoi", "Base ROI", PortDataType.Rectangle, IsRequired = true)]
[InputPort("Matches", "Matches", PortDataType.Any, IsRequired = true)]
[OutputPort("SearchRegion", "Search Region", PortDataType.Rectangle)]
[OperatorParam("MatchIndex", "Match Index", "int", DefaultValue = 0, Min = 0, Max = 100)]
public class RoiTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RoiTransform;

    public RoiTransformOperator(ILogger<RoiTransformOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Inputs are required"));
        }

        if (!inputs.TryGetValue("BaseRoi", out var baseObj) || baseObj == null || !TryParseRect(baseObj, out var baseRoi))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("BaseRoi is required"));
        }

        if (!inputs.TryGetValue("Matches", out var matchesObj) || matchesObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Matches is required"));
        }

        if (!TryGetMatchDictionary(matchesObj, GetIntParam(@operator, "MatchIndex", 0, min: 0, max: 100), out var match))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Matches does not contain a usable match result"));
        }

        if (!TryReadPose(match, out var centerX, out var centerY, out var angleDeg, out var scale))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Match pose fields missing. Expected CenterX/CenterY (or X/Y) plus optional Angle/Scale."));
        }

        var tracked = RoiTracker.TransformRoi(
            baseRoi,
            new Point2f((float)centerX, (float)centerY),
            (float)angleDeg,
            (float)scale);

        var output = new Dictionary<string, object>
        {
            ["SearchRegion"] = new Dictionary<string, object>
            {
                ["X"] = tracked.X,
                ["Y"] = tracked.Y,
                ["Width"] = tracked.Width,
                ["Height"] = tracked.Height
            }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var idx = GetIntParam(@operator, "MatchIndex", 0);
        if (idx < 0)
        {
            return ValidationResult.Invalid("MatchIndex must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static bool TryParseRect(object obj, out Rect rect)
    {
        rect = default;

        if (obj is Rect r)
        {
            rect = r;
            return true;
        }

        if (obj is IDictionary<string, object> dict)
        {
            if (TryGetInt(dict, "X", out var x) &&
                TryGetInt(dict, "Y", out var y) &&
                TryGetInt(dict, "Width", out var w) &&
                TryGetInt(dict, "Height", out var h))
            {
                rect = new Rect(x, y, w, h);
                return true;
            }
        }

        if (obj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0, StringComparer.OrdinalIgnoreCase);
            if (TryGetInt(normalized, "X", out var x) &&
                TryGetInt(normalized, "Y", out var y) &&
                TryGetInt(normalized, "Width", out var w) &&
                TryGetInt(normalized, "Height", out var h))
            {
                rect = new Rect(x, y, w, h);
                return true;
            }
        }

        return false;
    }

    private static bool TryGetMatchDictionary(object matchesObj, int index, out IDictionary<string, object> match)
    {
        match = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (matchesObj is IDictionary<string, object> dict)
        {
            match = dict;
            return true;
        }

        if (matchesObj is IDictionary legacyDict)
        {
            match = legacyDict.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        if (matchesObj is string)
        {
            return false;
        }

        if (matchesObj is IEnumerable enumerable)
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                if (i == index)
                {
                    if (item is IDictionary<string, object> itemDict)
                    {
                        match = itemDict;
                        return true;
                    }

                    if (item is IDictionary itemLegacy)
                    {
                        match = itemLegacy.Cast<DictionaryEntry>()
                            .Where(e => e.Key != null)
                            .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0, StringComparer.OrdinalIgnoreCase);
                        return true;
                    }

                    return false;
                }

                i++;
            }
        }

        return false;
    }

    private static bool TryReadPose(
        IDictionary<string, object> match,
        out double centerX,
        out double centerY,
        out double angleDeg,
        out double scale)
    {
        centerX = 0;
        centerY = 0;
        angleDeg = 0;
        scale = 1;

        bool hasCenter =
            TryGetDouble(match, "CenterX", out centerX) &&
            TryGetDouble(match, "CenterY", out centerY);

        if (!hasCenter)
        {
            if (!TryGetDouble(match, "X", out var x) || !TryGetDouble(match, "Y", out var y))
            {
                return false;
            }

            // If Width/Height are present, assume X/Y are top-left; otherwise treat X/Y as the point itself.
            if (TryGetDouble(match, "Width", out var w) && TryGetDouble(match, "Height", out var h))
            {
                centerX = x + (w / 2.0);
                centerY = y + (h / 2.0);
            }
            else
            {
                centerX = x;
                centerY = y;
            }
        }

        _ = TryGetDouble(match, "Angle", out angleDeg) || TryGetDouble(match, "AngleDeg", out angleDeg);
        if (!TryGetDouble(match, "Scale", out scale))
        {
            scale = 1.0;
        }

        if (scale <= 0)
        {
            scale = 1.0;
        }

        return true;
    }

    private static bool TryGetInt(IDictionary<string, object> dict, string key, out int value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            int i => (value = i) == i,
            long l => (value = (int)l) == (int)l,
            float f => (value = (int)Math.Round(f)) == (int)Math.Round(f),
            double d => (value = (int)Math.Round(d)) == (int)Math.Round(d),
            _ => int.TryParse(raw.ToString(), out value)
        };
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            default:
                return double.TryParse(raw.ToString(), out value);
        }
    }
}

