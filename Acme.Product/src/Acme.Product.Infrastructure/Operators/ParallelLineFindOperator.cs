// ParallelLineFindOperator.cs
// 平行线查找算子
// 在图像中检测满足约束的平行线对
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

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

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);
        using var edge = new Mat();
        Cv2.Canny(blurred, edge, 60, 180);
        using var closed = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(edge, closed, MorphTypes.Close, kernel);

        var lines = Cv2.HoughLinesP(closed, 1, Math.PI / 180, 80, minLength, 10);
        var candidates = (lines ?? Array.Empty<LineSegmentPoint>())
            .Select(line => CreateCandidate(new LineData(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y)))
            .Where(candidate => candidate.Line.Length >= minLength)
            .ToList();

        candidates = PruneCandidates(candidates, angleTolerance);

        var bestPair = default((ParallelLineCandidate line1, ParallelLineCandidate line2)?);
        var bestScore = double.MaxValue;
        var bestDistance = 0.0;
        var bestAngle = 0.0;

        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                var first = candidates[i];
                var second = candidates[j];

                var angleDiff = AngleDifference((float)first.Angle, (float)second.Angle);
                if (angleDiff > angleTolerance)
                {
                    continue;
                }

                var distance = Math.Abs(first.SignedOffset - second.SignedOffset);
                if (distance < minDistance || distance > maxDistance)
                {
                    continue;
                }

                var overlapRatio = ComputeOverlapRatio(first, second);
                if (overlapRatio < 0.25)
                {
                    continue;
                }

                var score = ComputePairScore(first, second, angleDiff, distance, overlapRatio, minDistance, maxDistance);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPair = (first, second);
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
            line1 = bestPair.Value.line1.Line;
            line2 = bestPair.Value.line2.Line;
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

    private static ParallelLineCandidate CreateCandidate(LineData line)
    {
        var directionX = line.EndX - line.StartX;
        var directionY = line.EndY - line.StartY;
        var length = Math.Max(1e-6f, line.Length);
        var unitX = directionX / length;
        var unitY = directionY / length;

        if (unitX < 0 || (Math.Abs(unitX) < 1e-6 && unitY < 0))
        {
            unitX = -unitX;
            unitY = -unitY;
        }

        var angle = Math.Atan2(unitY, unitX) * 180.0 / Math.PI;
        if (angle < 0)
        {
            angle += 180.0;
        }

        var normalX = -unitY;
        var normalY = unitX;
        var projectionStart = (unitX * line.StartX) + (unitY * line.StartY);
        var projectionEnd = (unitX * line.EndX) + (unitY * line.EndY);

        return new ParallelLineCandidate(
            line,
            angle,
            (normalX * line.MidX) + (normalY * line.MidY),
            Math.Min(projectionStart, projectionEnd),
            Math.Max(projectionStart, projectionEnd));
    }

    private static List<ParallelLineCandidate> PruneCandidates(IReadOnlyList<ParallelLineCandidate> candidates, double angleTolerance)
    {
        const int maxCandidates = 48;
        var kept = new List<ParallelLineCandidate>();

        foreach (var candidate in candidates.OrderByDescending(c => c.Line.Length))
        {
            var redundant = kept.Any(existing =>
                AngleDifference((float)candidate.Angle, (float)existing.Angle) <= Math.Max(1.0, angleTolerance * 0.35) &&
                Math.Abs(candidate.SignedOffset - existing.SignedOffset) <= 4.0 &&
                Math.Abs(candidate.Line.MidX - existing.Line.MidX) <= 12.0 &&
                Math.Abs(candidate.Line.MidY - existing.Line.MidY) <= 12.0);

            if (redundant)
            {
                continue;
            }

            kept.Add(candidate);
            if (kept.Count >= maxCandidates)
            {
                break;
            }
        }

        return kept;
    }

    private static double ComputeOverlapRatio(ParallelLineCandidate first, ParallelLineCandidate second)
    {
        var overlapStart = Math.Max(first.ProjectionMin, second.ProjectionMin);
        var overlapEnd = Math.Min(first.ProjectionMax, second.ProjectionMax);
        var overlap = Math.Max(0.0, overlapEnd - overlapStart);
        var shorter = Math.Max(1e-6, Math.Min(first.Line.Length, second.Line.Length));
        return overlap / shorter;
    }

    private static double ComputePairScore(
        ParallelLineCandidate first,
        ParallelLineCandidate second,
        double angleDiff,
        double distance,
        double overlapRatio,
        double minDistance,
        double maxDistance)
    {
        var preferredDistance = (minDistance + maxDistance) / 2.0;
        var lengthBonus = Math.Min(first.Line.Length, second.Line.Length) * 0.05;
        return (angleDiff * 6.0) +
               (Math.Abs(distance - preferredDistance) * 0.15) +
               ((1.0 - overlapRatio) * 30.0) -
               lengthBonus;
    }

    private sealed record ParallelLineCandidate(LineData Line, double Angle, double SignedOffset, double ProjectionMin, double ProjectionMax);
}
