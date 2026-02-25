using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class PointAlignmentOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PointAlignment;

    public PointAlignmentOperator(ILogger<PointAlignmentOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CurrentPoint and ReferencePoint are required"));
        }

        if (!inputs.TryGetValue("CurrentPoint", out var currentObj) || !TryParsePoint(currentObj, out var currentPoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'CurrentPoint' is missing or invalid"));
        }

        if (!inputs.TryGetValue("ReferencePoint", out var referenceObj) || !TryParsePoint(referenceObj, out var referencePoint))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input 'ReferencePoint' is missing or invalid"));
        }

        var outputUnit = GetStringParam(@operator, "OutputUnit", "Pixel");
        var pixelSize = GetDoubleParam(@operator, "PixelSize", 1.0, 1e-9, 1_000_000);

        var offsetX = currentPoint.X - referencePoint.X;
        var offsetY = currentPoint.Y - referencePoint.Y;
        var distance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);

        if (outputUnit.Equals("mm", StringComparison.OrdinalIgnoreCase))
        {
            offsetX *= pixelSize;
            offsetY *= pixelSize;
            distance *= pixelSize;
        }

        var output = new Dictionary<string, object>
        {
            { "OffsetX", offsetX },
            { "OffsetY", offsetY },
            { "Distance", distance }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var outputUnit = GetStringParam(@operator, "OutputUnit", "Pixel");
        var validUnits = new[] { "Pixel", "mm" };
        if (!validUnits.Contains(outputUnit, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("OutputUnit must be Pixel or mm");
        }

        var pixelSize = GetDoubleParam(@operator, "PixelSize", 1.0);
        if (pixelSize <= 0)
        {
            return ValidationResult.Invalid("PixelSize must be greater than 0");
        }

        return ValidationResult.Valid();
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
            case Position p:
                point = p;
                return true;
            case Point p:
                point = new Position(p.X, p.Y);
                return true;
            case Point2f p:
                point = new Position(p.X, p.Y);
                return true;
            case Point2d p:
                point = new Position(p.X, p.Y);
                return true;
        }

        if (obj is IDictionary<string, object> dict &&
            TryGetDouble(dict, "X", out var x) &&
            TryGetDouble(dict, "Y", out var y))
        {
            point = new Position(x, y);
            return true;
        }

        if (obj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            return TryParsePoint(normalized, out point);
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
}
