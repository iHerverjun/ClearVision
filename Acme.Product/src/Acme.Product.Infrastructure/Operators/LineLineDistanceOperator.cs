using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "线线距离",
    Description = "Computes distance and angle between two lines or segments.",
    Category = "检测",
    IconName = "parallel",
    Keywords = new[] { "line distance", "angle", "parallel", "segment" }
)]
[InputPort("Line1", "Line 1", PortDataType.LineData, IsRequired = true)]
[InputPort("Line2", "Line 2", PortDataType.LineData, IsRequired = true)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OutputPort("Intersection", "Intersection", PortDataType.Point)]
[OutputPort("HasIntersection", "Has Intersection", PortDataType.Boolean)]
[OutputPort("IsParallel", "Is Parallel", PortDataType.Boolean)]
[OperatorParam("ParallelThreshold", "Parallel Threshold", "double", DefaultValue = 2.0, Min = 0.0, Max = 45.0)]
[OperatorParam("DistanceModel", "Distance Model", "enum", DefaultValue = "Segment", Options = new[] { "Segment|Segment", "InfiniteLine|Infinite line" })]
[OperatorParam("Unit", "Unit", "enum", DefaultValue = "Pixel", Options = new[] { "Pixel|Pixel" })]
public class LineLineDistanceOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.LineLineDistance;

    public LineLineDistanceOperator(ILogger<LineLineDistanceOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("LineLineDistance requires Line1 and Line2"));
        }

        if (!inputs.TryGetValue("Line1", out var line1Obj) || !TryParseLine(line1Obj, out var line1))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line1' is missing or invalid"));
        }

        if (!inputs.TryGetValue("Line2", out var line2Obj) || !TryParseLine(line2Obj, out var line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line2' is missing or invalid"));
        }

        var parallelThreshold = GetDoubleParam(@operator, "ParallelThreshold", 2.0, 0, 45);
        var distanceModel = GetStringParam(@operator, "DistanceModel", "Segment");
        var unit = GetStringParam(@operator, "Unit", "Pixel");

        if (!TryParseDistanceModel(distanceModel, out var parsedModel))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DistanceModel must be Segment or InfiniteLine"));
        }

        if (!unit.Equals("Pixel", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Unit must be Pixel"));
        }

        var len1 = line1.Length;
        var len2 = line2.Length;
        if (!MeasurementGeometryHelper.IsFinite(line1) || !MeasurementGeometryHelper.IsFinite(line2))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Line coordinates must be finite numbers"));
        }

        if (len1 < 1e-9 || len2 < 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Input line is zero length"));
        }

        var angleDeg = MeasurementGeometryHelper.AngleBetweenLineDirections(line1, line2);
        var isParallel = angleDeg <= parallelThreshold;

        var hasInfiniteIntersection = MeasurementGeometryHelper.TryGetInfiniteLineIntersection(line1, line2, out var infiniteIntersection);
        var hasSegmentIntersection = MeasurementGeometryHelper.TryGetSegmentIntersection(line1, line2, out var segmentIntersection);
        var hasIntersection = parsedModel == DistanceModel.Segment ? hasSegmentIntersection : (!isParallel && hasInfiniteIntersection);
        var intersection = parsedModel == DistanceModel.Segment
            ? (hasSegmentIntersection ? segmentIntersection : MeasurementGeometryHelper.NoIntersection)
            : (hasInfiniteIntersection ? infiniteIntersection : MeasurementGeometryHelper.NoIntersection);

        var distance = parsedModel switch
        {
            DistanceModel.Segment => MeasurementGeometryHelper.DistanceSegmentToSegment(line1, line2),
            DistanceModel.InfiniteLine => isParallel
                ? MeasurementGeometryHelper.DistancePointToInfiniteLine(line1.StartX, line1.StartY, line2)
                : 0.0,
            _ => 0.0
        };

        var output = new Dictionary<string, object>
        {
            { "Distance", distance },
            { "Angle", angleDeg },
            { "Intersection", hasIntersection ? intersection : MeasurementGeometryHelper.NoIntersection },
            { "HasIntersection", hasIntersection },
            { "IsParallel", isParallel },
            { "DistanceModel", parsedModel.ToString() },
            { "Unit", "Pixel" },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", parsedModel == DistanceModel.Segment ? 0.01 : 0.0 }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "ParallelThreshold", 2.0);
        if (threshold < 0 || threshold > 45)
        {
            return ValidationResult.Invalid("ParallelThreshold must be within [0, 45]");
        }

        if (!TryParseDistanceModel(GetStringParam(@operator, "DistanceModel", "Segment"), out _))
        {
            return ValidationResult.Invalid("DistanceModel must be Segment or InfiniteLine");
        }

        if (!GetStringParam(@operator, "Unit", "Pixel").Equals("Pixel", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Unit must be Pixel");
        }

        return ValidationResult.Valid();
    }

    private static bool TryParseDistanceModel(string model, out DistanceModel parsed)
    {
        parsed = DistanceModel.Segment;
        if (string.Equals(model, "Segment", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.Segment;
            return true;
        }

        if (string.Equals(model, "InfiniteLine", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.InfiniteLine;
            return true;
        }

        return false;
    }

    private static bool TryParseLine(object? obj, out LineData line)
    {
        line = new LineData();
        if (obj == null)
        {
            return false;
        }

        if (obj is LineData lineData)
        {
            line = lineData;
            return true;
        }

        if (obj is IDictionary<string, object> dict)
        {
            if (TryGetDouble(dict, "StartX", out var sx) &&
                TryGetDouble(dict, "StartY", out var sy) &&
                TryGetDouble(dict, "EndX", out var ex) &&
                TryGetDouble(dict, "EndY", out var ey))
            {
                line = new LineData((float)sx, (float)sy, (float)ex, (float)ey);
                return true;
            }

            if (TryGetDouble(dict, "X1", out sx) &&
                TryGetDouble(dict, "Y1", out sy) &&
                TryGetDouble(dict, "X2", out ex) &&
                TryGetDouble(dict, "Y2", out ey))
            {
                line = new LineData((float)sx, (float)sy, (float)ex, (float)ey);
                return true;
            }
        }

        if (obj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParseLine(normalized, out line);
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }

    private enum DistanceModel
    {
        Segment = 0,
        InfiniteLine = 1
    }
}
