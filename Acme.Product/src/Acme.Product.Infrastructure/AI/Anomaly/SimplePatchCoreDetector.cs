using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.AI.Anomaly;

public sealed class SimplePatchCoreOptions
{
    public int PatchSize { get; init; } = 32;

    public int PatchStride { get; init; } = 16;

    public double CoresetRatio { get; init; } = 0.2;

    public string Backbone { get; init; } = "simple_patchcore";

    public string FeatureExtractorId { get; init; } = "lab_gradient_stats";

    public string EmbeddingModelId { get; init; } = string.Empty;

    public string EmbeddingModelPath { get; init; } = string.Empty;
}

public sealed class SimplePatchCoreFeatureBank
{
    [JsonPropertyName("patch_size")]
    public int PatchSize { get; init; }

    [JsonPropertyName("patch_stride")]
    public int PatchStride { get; init; }

    [JsonPropertyName("feature_length")]
    public int FeatureLength { get; init; }

    [JsonPropertyName("features")]
    public List<float[]> Features { get; init; } = [];

    [JsonPropertyName("feature_schema_version")]
    public string FeatureSchemaVersion { get; init; } = "simple_patchcore:v3";

    [JsonPropertyName("backbone")]
    public string Backbone { get; init; } = "simple_patchcore";

    [JsonPropertyName("feature_extractor_id")]
    public string FeatureExtractorId { get; init; } = "lab_gradient_stats";

    [JsonPropertyName("embedding_model_id")]
    public string EmbeddingModelId { get; init; } = string.Empty;

    [JsonPropertyName("embedding_model_path")]
    public string EmbeddingModelPath { get; init; } = string.Empty;

    [JsonPropertyName("training_image_count")]
    public int TrainingImageCount { get; init; }

    [JsonPropertyName("mean_nearest_distance")]
    public double MeanNearestDistance { get; init; }

    [JsonPropertyName("std_nearest_distance")]
    public double StdNearestDistance { get; init; }

    [JsonPropertyName("created_at_utc")]
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class SimplePatchCoreAnalysisResult
{
    public required float Score { get; init; }

    public required bool IsAnomaly { get; init; }

    public required Mat Heatmap { get; init; }

    public required Mat Mask { get; init; }

    public required int PatchCount { get; init; }

    public required float ThresholdUsed { get; init; }
}

public static class SimplePatchCoreDetector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static SimplePatchCoreFeatureBank BuildFeatureBank(IEnumerable<Mat> normalImages, SimplePatchCoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(normalImages);
        ArgumentNullException.ThrowIfNull(options);

        var allFeatures = new List<float[]>();
        var trainingImageCount = 0;
        foreach (var image in normalImages)
        {
            if (image == null || image.Empty())
            {
                continue;
            }

            trainingImageCount++;
            allFeatures.AddRange(ExtractFeatures(image, options).Select(x => x.Feature));
        }

        if (allFeatures.Count == 0)
        {
            throw new InvalidOperationException("No valid normal images were provided for anomaly training.");
        }

        var selected = SelectCoreset(allFeatures, options.CoresetRatio);
        var distances = ComputeNearestNeighborDistances(selected, includeSelf: false);
        var mean = distances.Count == 0 ? 0d : distances.Average();
        var variance = distances.Count == 0 ? 0d : distances.Average(x => Math.Pow(x - mean, 2));

        return new SimplePatchCoreFeatureBank
        {
            PatchSize = options.PatchSize,
            PatchStride = options.PatchStride,
            FeatureLength = selected[0].Length,
            Features = selected,
            FeatureSchemaVersion = GetFeatureSchemaVersion(options),
            Backbone = options.Backbone,
            FeatureExtractorId = options.FeatureExtractorId,
            EmbeddingModelId = options.EmbeddingModelId,
            EmbeddingModelPath = options.EmbeddingModelPath,
            TrainingImageCount = trainingImageCount,
            MeanNearestDistance = mean,
            StdNearestDistance = Math.Sqrt(variance)
        };
    }

    public static SimplePatchCoreAnalysisResult Analyze(Mat image, SimplePatchCoreFeatureBank bank, double threshold)
    {
        return Analyze(image, bank, threshold, null);
    }

    public static SimplePatchCoreAnalysisResult Analyze(
        Mat image,
        SimplePatchCoreFeatureBank bank,
        double threshold,
        SimplePatchCoreOptions? overrideOptions)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(bank);

        if (image.Empty())
        {
            throw new InvalidOperationException("Input image is empty.");
        }

        if (bank.Features.Count == 0)
        {
            throw new InvalidOperationException("Feature bank is empty.");
        }

        var options = CreateAnalysisOptions(bank, overrideOptions);

        var patches = ExtractFeatures(image, options);
        if (patches.Count == 0)
        {
            throw new InvalidOperationException("Failed to extract image patches for anomaly analysis.");
        }

        if (bank.FeatureLength > 0 && patches.Any(patch => patch.Feature.Length != bank.FeatureLength))
        {
            throw new InvalidOperationException(
                $"Feature length mismatch between input patches and feature bank. Expected {bank.FeatureLength}, got {patches.First().Feature.Length}.");
        }

        using var patchScoreMap = new Mat(image.Rows, image.Cols, MatType.CV_32FC1, Scalar.All(0));

        var maxScore = 0f;
        foreach (var patch in patches)
        {
            var distance = ComputeNearestDistance(patch.Feature, bank.Features);
            var normalized = NormalizeDistance(distance, bank);
            maxScore = Math.Max(maxScore, normalized);

            using var roi = new Mat(patchScoreMap, patch.Region);
            roi.SetTo(Math.Max(normalized, (float)roi.Mean().Val0));
        }

        using var blurred = new Mat();
        Cv2.GaussianBlur(patchScoreMap, blurred, new Size(0, 0), 3.0);

        using var normalizedMap = new Mat();
        Cv2.Normalize(blurred, normalizedMap, 0, 255, NormTypes.MinMax, MatType.CV_8UC1);

        var mask = new Mat();
        Cv2.Threshold(normalizedMap, mask, Math.Clamp(threshold, 0d, 1d) * 255d, 255, ThresholdTypes.Binary);

        var heatmap = new Mat();
        Cv2.ApplyColorMap(normalizedMap, heatmap, ColormapTypes.Turbo);

        return new SimplePatchCoreAnalysisResult
        {
            Score = maxScore,
            IsAnomaly = maxScore >= threshold,
            Heatmap = heatmap,
            Mask = mask,
            PatchCount = patches.Count,
            ThresholdUsed = (float)Math.Clamp(threshold, 0d, 1d)
        };
    }

    public static void Save(string path, SimplePatchCoreFeatureBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Feature bank path must not be empty.", nameof(path));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(bank, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static SimplePatchCoreFeatureBank Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Feature bank path must not be empty.", nameof(path));
        }

        var resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException($"Feature bank file not found: {resolved}", resolved);
        }

        var json = File.ReadAllText(resolved);
        return JsonSerializer.Deserialize<SimplePatchCoreFeatureBank>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse feature bank: {resolved}");
    }

    private static List<PatchFeature> ExtractFeatures(Mat image, SimplePatchCoreOptions options)
    {
        var extractorId = string.IsNullOrWhiteSpace(options.FeatureExtractorId)
            ? "lab_gradient_stats"
            : options.FeatureExtractorId.Trim().ToLowerInvariant();

        return extractorId switch
        {
            "lab_gradient_stats" => ExtractLabGradientStatsFeatures(image, options),
            "onnx_embedding" => ExtractOnnxEmbeddingFeatures(image, options),
            _ => throw new InvalidOperationException($"Unsupported feature extractor: {options.FeatureExtractorId}")
        };
    }

    private static List<PatchFeature> ExtractOnnxEmbeddingFeatures(Mat image, SimplePatchCoreOptions options)
    {
        var patchSize = Math.Min(Math.Min(options.PatchSize, image.Rows), image.Cols);
        var stride = Math.Max(1, options.PatchStride);
        var positionsY = EnumeratePatchPositions(image.Rows, patchSize, stride);
        var positionsX = EnumeratePatchPositions(image.Cols, patchSize, stride);

        var regions = new List<Rect>();
        foreach (var y in positionsY)
        {
            foreach (var x in positionsX)
            {
                regions.Add(new Rect(x, y, patchSize, patchSize));
            }
        }

        if (regions.Count == 0)
        {
            return [];
        }

        var patches = new List<Mat>(regions.Count);
        try
        {
            foreach (var region in regions)
            {
                using var roi = new Mat(image, region);
                patches.Add(roi.Clone());
            }

            var embeddings = OnnxPatchEmbeddingExtractor.ExtractEmbeddings(patches, options);
            if (embeddings.Count != regions.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding extractor returned {embeddings.Count} features for {regions.Count} patches.");
            }

            var features = new List<PatchFeature>(regions.Count);
            for (var i = 0; i < regions.Count; i++)
            {
                features.Add(new PatchFeature(regions[i], embeddings[i]));
            }

            return features;
        }
        finally
        {
            foreach (var patch in patches)
            {
                patch.Dispose();
            }
        }
    }

    private static string GetFeatureSchemaVersion(SimplePatchCoreOptions options)
    {
        var extractorId = string.IsNullOrWhiteSpace(options.FeatureExtractorId)
            ? "lab_gradient_stats"
            : options.FeatureExtractorId.Trim().ToLowerInvariant();

        return extractorId switch
        {
            "onnx_embedding" => "simple_patchcore:onnx_embedding:v1",
            _ => "simple_patchcore:v3"
        };
    }

    private static List<PatchFeature> ExtractLabGradientStatsFeatures(Mat image, SimplePatchCoreOptions options)
    {
        using var lab = new Mat();
        using var gray = new Mat();
        using var gradX = new Mat();
        using var gradY = new Mat();
        using var gradMagnitude = new Mat();

        Cv2.CvtColor(image, lab, ColorConversionCodes.BGR2Lab);
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Sobel(gray, gradX, MatType.CV_32FC1, 1, 0, 3);
        Cv2.Sobel(gray, gradY, MatType.CV_32FC1, 0, 1, 3);
        Cv2.Magnitude(gradX, gradY, gradMagnitude);

        var patchSize = Math.Min(Math.Min(options.PatchSize, image.Rows), image.Cols);
        var stride = Math.Max(1, options.PatchStride);
        var positionsY = EnumeratePatchPositions(image.Rows, patchSize, stride);
        var positionsX = EnumeratePatchPositions(image.Cols, patchSize, stride);

        var features = new List<PatchFeature>();
        foreach (var y in positionsY)
        {
            foreach (var x in positionsX)
            {
                var region = new Rect(x, y, patchSize, patchSize);
                using var labPatch = new Mat(lab, region);
                using var gradientPatch = new Mat(gradMagnitude, region);

                Cv2.MeanStdDev(labPatch, out var mean, out var stddev);
                var gradientMean = Cv2.Mean(gradientPatch).Val0;

                var feature = new float[]
                {
                    (float)(mean.Val0 / 255.0),
                    (float)(mean.Val1 / 255.0),
                    (float)(mean.Val2 / 255.0),
                    (float)(stddev.Val0 / 255.0),
                    (float)(stddev.Val1 / 255.0),
                    (float)(stddev.Val2 / 255.0),
                    (float)(gradientMean / 255.0)
                };

                features.Add(new PatchFeature(region, feature));
            }
        }

        return features;
    }

    private static List<int> EnumeratePatchPositions(int length, int patchSize, int stride)
    {
        var positions = new List<int>();
        if (length <= patchSize)
        {
            positions.Add(0);
            return positions;
        }

        for (var start = 0; start <= length - patchSize; start += stride)
        {
            positions.Add(start);
        }

        if (positions[^1] != length - patchSize)
        {
            positions.Add(length - patchSize);
        }

        return positions;
    }

    private static SimplePatchCoreOptions CreateAnalysisOptions(SimplePatchCoreFeatureBank bank, SimplePatchCoreOptions? overrideOptions)
    {
        return new SimplePatchCoreOptions
        {
            PatchSize = overrideOptions?.PatchSize ?? bank.PatchSize,
            PatchStride = overrideOptions?.PatchStride ?? bank.PatchStride,
            CoresetRatio = 1.0,
            Backbone = !string.IsNullOrWhiteSpace(overrideOptions?.Backbone) ? overrideOptions.Backbone : bank.Backbone,
            FeatureExtractorId = !string.IsNullOrWhiteSpace(overrideOptions?.FeatureExtractorId) ? overrideOptions.FeatureExtractorId : bank.FeatureExtractorId,
            EmbeddingModelId = !string.IsNullOrWhiteSpace(overrideOptions?.EmbeddingModelId) ? overrideOptions.EmbeddingModelId : bank.EmbeddingModelId,
            EmbeddingModelPath = !string.IsNullOrWhiteSpace(overrideOptions?.EmbeddingModelPath) ? overrideOptions.EmbeddingModelPath : bank.EmbeddingModelPath
        };
    }

    private static List<float[]> SelectCoreset(IReadOnlyList<float[]> features, double ratio)
    {
        if (features.Count == 0)
        {
            throw new InvalidOperationException("Feature collection is empty.");
        }

        var sampleCount = Math.Clamp((int)Math.Round(features.Count * Math.Clamp(ratio, 1d / features.Count, 1d)), 1, features.Count);
        if (sampleCount >= features.Count)
        {
            return features.Select(CloneFeature).ToList();
        }

        var selected = new List<float[]>(sampleCount);
        var selectedIndices = new HashSet<int>();

        var seedIndex = Enumerable.Range(0, features.Count)
            .OrderByDescending(i => SquaredNorm(features[i]))
            .First();

        selectedIndices.Add(seedIndex);
        selected.Add(CloneFeature(features[seedIndex]));

        while (selected.Count < sampleCount)
        {
            var bestIndex = -1;
            var bestDistance = double.NegativeInfinity;

            for (var i = 0; i < features.Count; i++)
            {
                if (selectedIndices.Contains(i))
                {
                    continue;
                }

                var minDistance = selected.Min(item => SquaredDistance(features[i], item));
                if (minDistance > bestDistance)
                {
                    bestDistance = minDistance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                break;
            }

            selectedIndices.Add(bestIndex);
            selected.Add(CloneFeature(features[bestIndex]));
        }

        return selected;
    }

    private static List<double> ComputeNearestNeighborDistances(IReadOnlyList<float[]> features, bool includeSelf)
    {
        var distances = new List<double>(features.Count);
        for (var i = 0; i < features.Count; i++)
        {
            var best = double.PositiveInfinity;
            for (var j = 0; j < features.Count; j++)
            {
                if (!includeSelf && i == j)
                {
                    continue;
                }

                best = Math.Min(best, Math.Sqrt(SquaredDistance(features[i], features[j])));
            }

            if (!double.IsInfinity(best))
            {
                distances.Add(best);
            }
        }

        return distances;
    }

    private static float NormalizeDistance(double distance, SimplePatchCoreFeatureBank bank)
    {
        var baseline = bank.MeanNearestDistance;
        var span = Math.Max(1e-6, bank.StdNearestDistance * 3.0 + baseline);
        var normalized = (distance - baseline) / span;
        return (float)Math.Clamp(normalized, 0d, 1d);
    }

    private static double ComputeNearestDistance(float[] feature, IReadOnlyList<float[]> bank)
    {
        var best = double.PositiveInfinity;
        foreach (var candidate in bank)
        {
            best = Math.Min(best, Math.Sqrt(SquaredDistance(feature, candidate)));
        }

        return best;
    }

    private static double SquaredNorm(IReadOnlyList<float> feature)
    {
        double sum = 0;
        for (var i = 0; i < feature.Count; i++)
        {
            sum += feature[i] * feature[i];
        }

        return sum;
    }

    private static double SquaredDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double sum = 0;
        var length = Math.Min(left.Count, right.Count);
        for (var i = 0; i < length; i++)
        {
            var delta = left[i] - right[i];
            sum += delta * delta;
        }

        return sum;
    }

    private static float[] CloneFeature(float[] feature)
    {
        var clone = new float[feature.Length];
        Array.Copy(feature, clone, feature.Length);
        return clone;
    }

    private sealed record PatchFeature(Rect Region, float[] Feature);
}
