using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

public class BlobLabelingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BlobLabeling;

    public BlobLabelingOperator(ILogger<BlobLabelingOperator> logger) : base(logger)
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

        var labelBy = GetStringParam(@operator, "LabelBy", "Area");
        var drawLabels = GetBoolParam(@operator, "DrawLabels", true);
        var thresholdsJson = GetStringParam(@operator, "Thresholds", string.Empty);
        var thresholds = ParseThresholds(thresholdsJson);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var result = src.Clone();
        var labels = new List<Dictionary<string, object>>();

        for (var i = 0; i < contours.Length; i++)
        {
            var contour = contours[i];
            var area = Cv2.ContourArea(contour);
            if (area <= 0)
            {
                continue;
            }

            var perimeter = Math.Max(1e-6, Cv2.ArcLength(contour, true));
            var circularity = 4 * Math.PI * area / (perimeter * perimeter);
            var rect = Cv2.BoundingRect(contour);
            var aspectRatio = rect.Width / (double)Math.Max(1, rect.Height);
            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            var feature = labelBy.ToLowerInvariant() switch
            {
                "circularity" => circularity,
                "aspectratio" => aspectRatio,
                "position" => center.Y,
                _ => area
            };

            var label = ResolveLabel(labelBy, feature, rect, src.Size(), thresholds);
            labels.Add(new Dictionary<string, object>
            {
                { "Index", i + 1 },
                { "Label", label },
                { "Area", area },
                { "Circularity", circularity },
                { "AspectRatio", aspectRatio },
                { "CenterX", center.X },
                { "CenterY", center.Y }
            });

            if (drawLabels)
            {
                var color = LabelColor(label);
                Cv2.DrawContours(result, new[] { contour }, -1, color, 2);
                Cv2.PutText(result, label, new Point(rect.X, Math.Max(15, rect.Y - 4)), HersheyFonts.HersheySimplex, 0.5, color, 2);
            }
        }

        var output = new Dictionary<string, object>
        {
            { "Labels", labels },
            { "Count", labels.Count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var labelBy = GetStringParam(@operator, "LabelBy", "Area");
        var valid = new[] { "Area", "Circularity", "AspectRatio", "Position" };
        if (!valid.Contains(labelBy, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("LabelBy must be Area, Circularity, AspectRatio or Position");
        }

        return ValidationResult.Valid();
    }

    private static string ResolveLabel(
        string labelBy,
        double feature,
        Rect rect,
        Size imageSize,
        IReadOnlyList<LabelThreshold> thresholds)
    {
        var matched = thresholds.FirstOrDefault(t => feature >= t.Min && feature <= t.Max);
        if (matched != null)
        {
            return matched.Name;
        }

        switch (labelBy.ToLowerInvariant())
        {
            case "circularity":
                return feature > 0.8 ? "Round" : "Irregular";
            case "aspectratio":
                if (feature > 1.3)
                {
                    return "Wide";
                }

                if (feature < 0.7)
                {
                    return "Tall";
                }

                return "SquareLike";
            case "position":
                if (rect.Y < imageSize.Height / 3)
                {
                    return "Top";
                }

                if (rect.Y < imageSize.Height * 2 / 3)
                {
                    return "Middle";
                }

                return "Bottom";
            default:
                if (feature < 200)
                {
                    return "Small";
                }

                if (feature < 1500)
                {
                    return "Medium";
                }

                return "Large";
        }
    }

    private static Scalar LabelColor(string label)
    {
        var hash = Math.Abs(label.GetHashCode());
        var b = (byte)(50 + hash % 180);
        var g = (byte)(50 + (hash / 3) % 180);
        var r = (byte)(50 + (hash / 7) % 180);
        return new Scalar(b, g, r);
    }

    private static List<LabelThreshold> ParseThresholds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<LabelThreshold>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<LabelThreshold>>(json);
            return parsed ?? new List<LabelThreshold>();
        }
        catch
        {
            return new List<LabelThreshold>();
        }
    }

    private sealed record LabelThreshold(string Name, double Min, double Max);
}

