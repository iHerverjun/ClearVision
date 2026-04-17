using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Calibration;

public sealed class HandEyeValidationReport
{
    public static HandEyeValidationReport Empty { get; } = new()
    {
        MeanError = 0d,
        MaxError = 0d,
        MeanRotationErrorDegrees = 0d,
        Quality = "unknown",
        HtmlReport = string.Empty,
        Suggestions = [],
        SuggestedValidationPosesJson = "[]"
    };

    public required double MeanError { get; init; }

    public required double MaxError { get; init; }

    public required double MeanRotationErrorDegrees { get; init; }

    public required string Quality { get; init; }

    public required string HtmlReport { get; init; }

    public required string[] Suggestions { get; init; }

    public required string SuggestedValidationPosesJson { get; init; }
}

public static class HandEyeCalibrationValidator
{
    public static HandEyeValidationReport Validate(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        IReadOnlyList<Matrix4x4> cameraToTargetPoses,
        Matrix4x4 handEyeMatrix,
        RobotHandEyeCalibrationType calibrationType)
    {
        var referenceTransforms = calibrationType switch
        {
            RobotHandEyeCalibrationType.EyeInHand => BuildEyeInHandReferenceTransforms(baseToToolPoses, cameraToTargetPoses, handEyeMatrix),
            RobotHandEyeCalibrationType.EyeToHand => BuildEyeToHandReferenceTransforms(baseToToolPoses, cameraToTargetPoses, handEyeMatrix),
            _ => []
        };

        if (referenceTransforms.Count == 0)
        {
            return HandEyeValidationReport.Empty;
        }

        var meanTransform = HandEyeCalibrationSolver.AverageTransforms(referenceTransforms);
        var translationErrors = referenceTransforms
            .Select(transform => TranslationError(transform, meanTransform))
            .ToArray();
        var rotationErrors = referenceTransforms
            .Select(transform => HandEyeCalibrationSolver.RotationErrorDegrees(transform, meanTransform))
            .ToArray();

        var meanError = translationErrors.Average();
        var maxError = translationErrors.Max();
        var meanRotationError = rotationErrors.Average();
        var quality = ClassifyQuality(meanError, maxError, meanRotationError);
        var suggestions = BuildSuggestions(baseToToolPoses, meanError, maxError, meanRotationError, quality);
        var suggestedPosesJson = BuildSuggestedValidationPoses(baseToToolPoses);

        return new HandEyeValidationReport
        {
            MeanError = meanError,
            MaxError = maxError,
            MeanRotationErrorDegrees = meanRotationError,
            Quality = quality,
            HtmlReport = BuildHtmlReport(calibrationType, meanError, maxError, meanRotationError, quality, translationErrors, rotationErrors, suggestions),
            Suggestions = suggestions,
            SuggestedValidationPosesJson = suggestedPosesJson
        };
    }

    private static List<Matrix4x4> BuildEyeInHandReferenceTransforms(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        IReadOnlyList<Matrix4x4> cameraToTargetPoses,
        Matrix4x4 cameraToTool)
    {
        var transforms = new List<Matrix4x4>(baseToToolPoses.Count);
        for (var i = 0; i < baseToToolPoses.Count; i++)
        {
            var targetToCamera = HandEyeCalibrationSolver.Invert(cameraToTargetPoses[i]);
            var toolToBase = HandEyeCalibrationSolver.Invert(baseToToolPoses[i]);
            transforms.Add(targetToCamera * cameraToTool * toolToBase);
        }

        return transforms;
    }

    private static List<Matrix4x4> BuildEyeToHandReferenceTransforms(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        IReadOnlyList<Matrix4x4> cameraToTargetPoses,
        Matrix4x4 cameraToBase)
    {
        var transforms = new List<Matrix4x4>(baseToToolPoses.Count);
        for (var i = 0; i < baseToToolPoses.Count; i++)
        {
            var targetToCamera = HandEyeCalibrationSolver.Invert(cameraToTargetPoses[i]);
            transforms.Add(targetToCamera * cameraToBase * baseToToolPoses[i]);
        }

        return transforms;
    }

    private static double TranslationError(Matrix4x4 left, Matrix4x4 right)
    {
        var leftTranslation = new Vector3(left.M41, left.M42, left.M43);
        var rightTranslation = new Vector3(right.M41, right.M42, right.M43);
        return (leftTranslation - rightTranslation).Length();
    }

    private static string ClassifyQuality(double meanError, double maxError, double meanRotationError)
    {
        if (meanError <= 0.0005 && maxError <= 0.0010 && meanRotationError <= 0.5)
        {
            return "good";
        }

        if (meanError <= 0.0015 && maxError <= 0.0030 && meanRotationError <= 1.5)
        {
            return "fair";
        }

        return "poor";
    }

    private static string[] BuildSuggestions(
        IReadOnlyList<Matrix4x4> baseToToolPoses,
        double meanError,
        double maxError,
        double meanRotationError,
        string quality)
    {
        var suggestions = new List<string>();

        if (baseToToolPoses.Count < 8)
        {
            suggestions.Add("增加采样姿态到 8-10 组以上，以提升解算稳定性。");
        }

        var translationSpan = ComputeTranslationSpan(baseToToolPoses);
        if (translationSpan < 0.05)
        {
            suggestions.Add("扩大机器人采样空间，避免姿态分布过于集中。");
        }

        if (meanRotationError > 1.0)
        {
            suggestions.Add("增加带明显俯仰/偏航差异的姿态，提升旋转可观测性。");
        }

        if (maxError > 0.002)
        {
            suggestions.Add("检查标定板角点检测与机器人时间同步，剔除异常样本后重新标定。");
        }

        if (quality == "good")
        {
            suggestions.Add("当前标定质量良好，可直接进入验证工位或上线前复核。");
        }

        return suggestions.Count == 0
            ? ["标定结果稳定，建议保留当前样本集作为基线。"]
            : suggestions.ToArray();
    }

    private static double ComputeTranslationSpan(IReadOnlyList<Matrix4x4> transforms)
    {
        if (transforms.Count == 0)
        {
            return 0d;
        }

        var xs = transforms.Select(x => (double)x.M41).ToArray();
        var ys = transforms.Select(x => (double)x.M42).ToArray();
        var zs = transforms.Select(x => (double)x.M43).ToArray();
        return Math.Max(xs.Max() - xs.Min(), Math.Max(ys.Max() - ys.Min(), zs.Max() - zs.Min()));
    }

    private static string BuildSuggestedValidationPoses(IReadOnlyList<Matrix4x4> baseToToolPoses)
    {
        var average = HandEyeCalibrationSolver.AverageTransforms(baseToToolPoses.ToList());
        var center = new Vector3(average.M41, average.M42, average.M43);
        var offsets = new[]
        {
            new Vector3(-0.05f, 0.00f, 0.00f),
            new Vector3(0.05f, 0.00f, 0.00f),
            new Vector3(0.00f, -0.05f, 0.00f),
            new Vector3(0.00f, 0.05f, 0.00f),
            new Vector3(0.00f, 0.00f, -0.05f),
            new Vector3(0.00f, 0.00f, 0.05f),
            new Vector3(0.04f, 0.04f, 0.00f),
            new Vector3(-0.04f, -0.04f, 0.00f)
        };

        var poses = offsets.Select(offset => new
        {
            translation = new[]
            {
                Math.Round(center.X + offset.X, 6),
                Math.Round(center.Y + offset.Y, 6),
                Math.Round(center.Z + offset.Z, 6)
            },
            rpy_degrees = new[] { 0, 0, 0 }
        });

        return JsonSerializer.Serialize(poses, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildHtmlReport(
        RobotHandEyeCalibrationType calibrationType,
        double meanError,
        double maxError,
        double meanRotationError,
        string quality,
        IReadOnlyList<double> translationErrors,
        IReadOnlyList<double> rotationErrors,
        IReadOnlyList<string> suggestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>HandEye Calibration Report</title></head><body>");
        sb.AppendLine("<h1>Hand-Eye Calibration Validation Report</h1>");
        sb.AppendLine($"<p><strong>Calibration Type:</strong> {calibrationType}</p>");
        sb.AppendLine("<table border=\"1\" cellspacing=\"0\" cellpadding=\"6\">");
        sb.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
        sb.AppendLine($"<tr><td>Mean Error</td><td>{meanError:F6}</td></tr>");
        sb.AppendLine($"<tr><td>Max Error</td><td>{maxError:F6}</td></tr>");
        sb.AppendLine($"<tr><td>Mean Rotation Error (deg)</td><td>{meanRotationError:F3}</td></tr>");
        sb.AppendLine($"<tr><td>Quality</td><td>{quality}</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("<h2>Sample Errors</h2>");
        sb.AppendLine("<table border=\"1\" cellspacing=\"0\" cellpadding=\"6\">");
        sb.AppendLine("<tr><th>#</th><th>Translation Error</th><th>Rotation Error (deg)</th></tr>");

        for (var i = 0; i < translationErrors.Count; i++)
        {
            sb.AppendLine($"<tr><td>{i + 1}</td><td>{translationErrors[i]:F6}</td><td>{rotationErrors[i]:F3}</td></tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("<h2>Suggestions</h2><ul>");
        foreach (var suggestion in suggestions)
        {
            sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(suggestion)}</li>");
        }

        sb.AppendLine("</ul></body></html>");
        return sb.ToString();
    }
}
