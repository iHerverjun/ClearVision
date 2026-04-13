// BlobLabelingOperator.cs
// 连通域标注算子
using System.Collections;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "连通域标注",
    Description = "Classifies connected blobs by geometric features and draws labels.",
    Category = "定位",
    IconName = "blob-label",
    Keywords = new[] { "blob", "label", "classify connected component" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Blobs", "Blobs", PortDataType.Contour, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Labels", "Labels", PortDataType.Any)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OperatorParam("LabelBy", "Label By", "enum", DefaultValue = "Area", Options = new[] { "Area|Area", "Circularity|Circularity", "AspectRatio|AspectRatio", "Position|Position" })]
[OperatorParam("Thresholds", "Thresholds", "string", DefaultValue = "[]")]
[OperatorParam("DrawLabels", "Draw Labels", "bool", DefaultValue = true)]
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
        if (!TryParseThresholds(thresholdsJson, out var thresholds, out var thresholdError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(thresholdError ?? "Thresholds is invalid"));
        }

        List<BlobMeasurement> blobs;
        if (inputs != null && inputs.TryGetValue("Blobs", out var blobInput))
        {
            if (!TryParseBlobInput(blobInput, out blobs))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Input 'Blobs' is present but invalid"));
            }
        }
        else
        {
            blobs = DetectBlobsFromImage(src);
        }

        var result = src.Clone();
        var labels = new List<Dictionary<string, object>>();

        for (var i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            if (blob.Area <= 0)
            {
                continue;
            }

            var feature = labelBy.ToLowerInvariant() switch
            {
                "circularity" => blob.Circularity,
                "aspectratio" => blob.AspectRatio,
                "position" => blob.Center.Y,
                _ => blob.Area
            };

            var label = ResolveLabel(labelBy, feature, blob.Center, src.Size(), thresholds);
            labels.Add(new Dictionary<string, object>
            {
                { "Index", i + 1 },
                { "Label", label },
                { "Area", blob.Area },
                { "Circularity", blob.Circularity },
                { "AspectRatio", blob.AspectRatio },
                { "CenterX", blob.Center.X },
                { "CenterY", blob.Center.Y }
            });

            if (drawLabels)
            {
                var color = LabelColor(label);
                if (blob.Contour.Length >= 3)
                {
                    Cv2.DrawContours(result, new[] { blob.Contour }, -1, color, 2);
                }
                else
                {
                    Cv2.Rectangle(result, blob.Rect, color, 2);
                }

                Cv2.PutText(result, label, new Point(blob.Rect.X, Math.Max(15, blob.Rect.Y - 4)), HersheyFonts.HersheySimplex, 0.5, color, 2);
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

        var thresholdsJson = GetStringParam(@operator, "Thresholds", string.Empty);
        if (!TryParseThresholds(thresholdsJson, out _, out var error))
        {
            return ValidationResult.Invalid(error ?? "Thresholds is invalid");
        }

        return ValidationResult.Valid();
    }

    private static List<BlobMeasurement> DetectBlobsFromImage(Mat src)
    {
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
        return contours
            .Select(CreateBlobMeasurement)
            .Where(blob => blob != null)
            .Cast<BlobMeasurement>()
            .ToList();
    }

    private static string ResolveLabel(
        string labelBy,
        double feature,
        Point center,
        Size imageSize,
        IReadOnlyList<LabelThreshold> thresholds)
    {
        var matched = thresholds.FirstOrDefault(threshold => feature >= threshold.Min && feature <= threshold.Max);
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
                if (center.Y < imageSize.Height / 3.0)
                {
                    return "Top";
                }

                if (center.Y < imageSize.Height * 2.0 / 3.0)
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

    private static bool TryParseThresholds(string json, out List<LabelThreshold> thresholds, out string? error)
    {
        thresholds = new List<LabelThreshold>();
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<LabelThreshold>>(json);
            if (parsed == null)
            {
                return true;
            }

            foreach (var threshold in parsed)
            {
                if (threshold == null ||
                    string.IsNullOrWhiteSpace(threshold.Name) ||
                    !double.IsFinite(threshold.Min) ||
                    !double.IsFinite(threshold.Max) ||
                    threshold.Min > threshold.Max)
                {
                    error = "Thresholds must be a JSON array of { Name, Min, Max } with finite values and Min <= Max";
                    return false;
                }
            }

            thresholds = parsed;
            return true;
        }
        catch
        {
            error = "Thresholds must be a valid JSON array";
            return false;
        }
    }

    private static BlobMeasurement? CreateBlobMeasurement(Point[] contour)
    {
        var area = Cv2.ContourArea(contour);
        if (area <= 0)
        {
            return null;
        }

        var perimeter = Math.Max(1e-6, Cv2.ArcLength(contour, true));
        var circularity = 4 * Math.PI * area / (perimeter * perimeter);
        var rect = Cv2.BoundingRect(contour);
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        var aspectRatio = rect.Width / (double)Math.Max(1, rect.Height);
        return new BlobMeasurement(contour, rect, center, area, circularity, aspectRatio);
    }

    private static bool TryParseBlobInput(object? raw, out List<BlobMeasurement> blobs)
    {
        blobs = new List<BlobMeasurement>();
        if (raw == null)
        {
            return false;
        }

        if (raw is Point[][] contourArray)
        {
            blobs = contourArray.Select(CreateBlobMeasurement).Where(blob => blob != null).Cast<BlobMeasurement>().ToList();
            return blobs.Count > 0;
        }

        if (raw is IEnumerable<Point[]> contours)
        {
            blobs = contours.Select(CreateBlobMeasurement).Where(blob => blob != null).Cast<BlobMeasurement>().ToList();
            return blobs.Count > 0;
        }

        if (raw is IEnumerable<object> enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryParseBlobItem(item, out var blob))
                {
                    blobs.Add(blob);
                }
            }

            return blobs.Count > 0;
        }

        return TryParseBlobItem(raw, out var singleBlob) && (blobs = new List<BlobMeasurement> { singleBlob }) != null;
    }

    private static bool TryParseBlobItem(object? raw, out BlobMeasurement blob)
    {
        blob = new BlobMeasurement(Array.Empty<Point>(), new Rect(), new Point(), 0, 0, 0);
        if (raw == null)
        {
            return false;
        }

        if (raw is Point[] contour)
        {
            var parsed = CreateBlobMeasurement(contour);
            if (parsed == null)
            {
                return false;
            }

            blob = parsed;
            return true;
        }

        if (raw is IDictionary<string, object> dict)
        {
            return TryParseBlobDictionary(dict, out blob);
        }

        if (raw is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0, StringComparer.OrdinalIgnoreCase);
            return TryParseBlobDictionary(normalized, out blob);
        }

        return false;
    }

    private static bool TryParseBlobDictionary(IDictionary<string, object> dict, out BlobMeasurement blob)
    {
        blob = new BlobMeasurement(Array.Empty<Point>(), new Rect(), new Point(), 0, 0, 0);
        if (!TryGetDouble(dict, "X", out var x) ||
            !TryGetDouble(dict, "Y", out var y) ||
            !TryGetDouble(dict, "Width", out var width) ||
            !TryGetDouble(dict, "Height", out var height))
        {
            return false;
        }

        var rect = new Rect((int)Math.Round(x), (int)Math.Round(y), Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height)));
        var area = TryGetDouble(dict, "Area", out var explicitArea) ? explicitArea : rect.Width * rect.Height;
        var circularity = TryGetDouble(dict, "Circularity", out var explicitCircularity)
            ? explicitCircularity
            : EstimateRectangularCircularity(rect);
        var aspectRatio = TryGetDouble(dict, "AspectRatio", out var explicitAspectRatio)
            ? explicitAspectRatio
            : rect.Width / (double)Math.Max(1, rect.Height);
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

        blob = new BlobMeasurement(Array.Empty<Point>(), rect, center, area, circularity, aspectRatio);
        return area > 0;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw))
        {
            raw = dict.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        if (raw == null)
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

    private static double EstimateRectangularCircularity(Rect rect)
    {
        var area = (double)rect.Width * rect.Height;
        var perimeter = 2.0 * (rect.Width + rect.Height);
        return perimeter <= 1e-6 ? 0 : 4 * Math.PI * area / (perimeter * perimeter);
    }

    private sealed record LabelThreshold(string Name, double Min, double Max);
    private sealed record BlobMeasurement(Point[] Contour, Rect Rect, Point Center, double Area, double Circularity, double AspectRatio);
}
