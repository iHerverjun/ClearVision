using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "平行线查找",
    Description = "Finds best pair of near-parallel lines in an image.",
    Category = "定位",
    IconName = "parallel",
    Keywords = new[] { "parallel", "dual edge", "rails" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Line1", "Line 1", PortDataType.LineData)]
[OutputPort("Line2", "Line 2", PortDataType.LineData)]
[OutputPort("Distance", "Distance", PortDataType.Float)]
[OutputPort("Angle", "Angle", PortDataType.Float)]
[OutputPort("PairCount", "Pair Count", PortDataType.Integer)]
[OperatorParam("AngleTolerance", "Angle Tolerance", "double", DefaultValue = 5.0, Min = 0.0, Max = 45.0)]
[OperatorParam("MinLength", "Min Length", "double", DefaultValue = 40.0, Min = 1.0, Max = 100000.0)]
[OperatorParam("MinDistance", "Min Distance", "double", DefaultValue = 2.0, Min = 0.0, Max = 100000.0)]
[OperatorParam("MaxDistance", "Max Distance", "double", DefaultValue = 200.0, Min = 0.0, Max = 100000.0)]
public class ParallelLineFindOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ParallelLineFind;

    public ParallelLineFindOperator(ILogger<ParallelLineFindOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var angleTolerance = GetDoubleParam(@operator, "AngleTolerance", 5.0, 0.0, 45.0);
        var minLength = GetDoubleParam(@operator, "MinLength", 40.0, 1.0, 100000.0);
        var minDistance = GetDoubleParam(@operator, "MinDistance", 2.0, 0.0, 100000.0);
        var maxDistance = GetDoubleParam(@operator, "MaxDistance", 200.0, 0.0, 100000.0);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var edge = new Mat();
        Cv2.Canny(gray, edge, 60, 180);
        var lines = Cv2.HoughLinesP(edge, 1, Math.PI / 180, 80, minLength, 10);

        var candidates = (lines ?? Array.Empty<LineSegmentPoint>())
            .Select(l => new LineData(l.P1.X, l.P1.Y, l.P2.X, l.P2.Y))
            .Where(l => l.Length >= minLength)
            .ToList();

        var bestPair = default((LineData line1, LineData line2)?);
        var bestScore = double.MaxValue;
        var bestDistance = 0.0;
        var bestAngle = 0.0;

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i];
                var b = candidates[j];

                var angleDiff = AngleDifference(a.Angle, b.Angle);
                if (angleDiff > angleTolerance)
                {
                    continue;
                }

                var distance = DistancePointToLine(a.MidX, a.MidY, b);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                var score = Math.Abs(angleDiff) + Math.Abs(distance - (minDistance + maxDistance) / 2.0);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPair = (a, b);
                    bestDistance = distance;
                    bestAngle = angleDiff;
                }
            }
        }

        var resultImage = src.Clone();
        var pairCount = 0;
        var line1 = new LineData();
        var line2 = new LineData();

        if (bestPair.HasValue)
        {
            pairCount = 1;
            line1 = bestPair.Value.line1;
            line2 = bestPair.Value.line2;

            Cv2.Line(resultImage, new Point((int)line1.StartX, (int)line1.StartY), new Point((int)line1.EndX, (int)line1.EndY), new Scalar(0, 255, 0), 2);
            Cv2.Line(resultImage, new Point((int)line2.StartX, (int)line2.StartY), new Point((int)line2.EndX, (int)line2.EndY), new Scalar(255, 0, 0), 2);
        }

        var output = new Dictionary<string, object>
        {
            { "Line1", line1 },
            { "Line2", line2 },
            { "Distance", bestDistance },
            { "Angle", bestAngle },
            { "PairCount", pairCount }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var angleTolerance = GetDoubleParam(@operator, "AngleTolerance", 5.0);
        if (angleTolerance < 0 || angleTolerance > 45)
        {
            return ValidationResult.Invalid("AngleTolerance must be in [0, 45]");
        }

        var minDistance = GetDoubleParam(@operator, "MinDistance", 2.0);
        var maxDistance = GetDoubleParam(@operator, "MaxDistance", 200.0);
        if (minDistance < 0 || maxDistance < 0 || minDistance > maxDistance)
        {
            return ValidationResult.Invalid("Invalid MinDistance/MaxDistance range");
        }

        return ValidationResult.Valid();
    }

    private static double AngleDifference(float a, float b)
    {
        var diff = Math.Abs(a - b);
        while (diff > 180)
        {
            diff -= 180;
        }

        if (diff > 90)
        {
            diff = 180 - diff;
        }

        return diff;
    }

    private static double DistancePointToLine(double px, double py, LineData line)
    {
        var a = line.EndY - line.StartY;
        var b = line.StartX - line.EndX;
        var c = line.EndX * line.StartY - line.StartX * line.EndY;
        var denominator = Math.Sqrt(a * a + b * b);
        if (denominator < 1e-9)
        {
            return 0;
        }

        return Math.Abs(a * px + b * py + c) / denominator;
    }
}

