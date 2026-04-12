using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "点线距离",
    Description = "Computes distance from a point to a line or segment.",
    Category = "检测",
    IconName = "distance",
    Keywords = new[] { "point", "line", "distance", "perpendicular", "segment" }
)]
[InputPort("Point", "Point", PortDataType.Point, IsRequired = true)]
[InputPort("Line", "Line", PortDataType.LineData, IsRequired = true)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OutputPort("FootPoint", "Foot Point", PortDataType.Point)]
[OperatorParam("DistanceModel", "Distance Model", "enum", DefaultValue = "Segment", Options = new[] { "Segment|Segment", "InfiniteLine|Infinite line" })]
[OperatorParam("Unit", "Unit", "enum", DefaultValue = "Pixel", Options = new[] { "Pixel|Pixel" })]
public class PointLineDistanceOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PointLineDistance;

    public PointLineDistanceOperator(ILogger<PointLineDistanceOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PointLineDistance requires Point and Line inputs"));
        }

        if (!inputs.TryGetValue("Point", out var pointObj) || !TryParsePoint(pointObj, out var point))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Point' is missing or invalid"));
        }

        if (!inputs.TryGetValue("Line", out var lineObj) || !TryParseLine(lineObj, out var line))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Line' is missing or invalid"));
        }

        if (!MeasurementGeometryHelper.IsFinite(line) || !double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Point/Line coordinates must be finite numbers"));
        }

        if (line.Length < 1e-9)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("[DegenerateGeometry] Line is zero length"));
        }

        var distanceModel = GetStringParam(@operator, "DistanceModel", "Segment");
        if (!TryParseDistanceModel(distanceModel, out var parsedModel))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("DistanceModel must be Segment or InfiniteLine"));
        }

        if (!GetStringParam(@operator, "Unit", "Pixel").Equals("Pixel", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Unit must be Pixel"));
        }

        var footPoint = parsedModel == DistanceModel.Segment
            ? MeasurementGeometryHelper.ProjectPointToSegment(point.X, point.Y, line)
            : MeasurementGeometryHelper.ProjectPointToInfiniteLine(point.X, point.Y, line);
        var distance = parsedModel == DistanceModel.Segment
            ? MeasurementGeometryHelper.DistancePointToSegment(point.X, point.Y, line)
            : MeasurementGeometryHelper.DistancePointToInfiniteLine(point.X, point.Y, line);

        var output = new Dictionary<string, object>
        {
            { "Distance", distance },
            { "FootPoint", footPoint },
            { "FootPointX", footPoint.X },
            { "FootPointY", footPoint.Y },
            { "DistanceModel", parsedModel.ToString() },
            { "Unit", "Pixel" },
            { "StatusCode", "OK" },
            { "StatusMessage", "Success" },
            { "Confidence", 1.0 },
            { "UncertaintyPx", 0.0 }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
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
            return true;
        }

        if (string.Equals(model, "InfiniteLine", StringComparison.OrdinalIgnoreCase))
        {
            parsed = DistanceModel.InfiniteLine;
            return true;
        }

        return false;
    }

    private static bool TryParsePoint(object? obj, out Position point)
    {
        point = new Position(0, 0);
        if (obj == null)
        {
            return false;
        }

        switch (obj)
        {
            case Position position:
                point = position;
                return true;
            case Point cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
            case Point2f cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
            case Point2d cvPoint:
                point = new Position(cvPoint.X, cvPoint.Y);
                return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacyDict)
        {
            var normalized = legacyDict.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            if (TryGetDouble(normalized, "X", out var parsedX) && TryGetDouble(normalized, "Y", out var parsedY))
            {
                point = new Position(parsedX, parsedY);
                return true;
            }
        }

        var text = obj.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var stripped = text.Trim().Trim('(', ')', '[', ']');
            var parts = stripped.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out var px) &&
                double.TryParse(parts[1], out var py))
            {
                point = new Position(px, py);
                return true;
            }
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

        if (raw is double d)
        {
            value = d;
            return true;
        }

        if (raw is float f)
        {
            value = f;
            return true;
        }

        if (raw is int i)
        {
            value = i;
            return true;
        }

        return double.TryParse(raw.ToString(), out value);
    }

    private enum DistanceModel
    {
        Segment = 0,
        InfiniteLine = 1
    }
}
