using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.AI.Anomaly;
using Acme.Product.Infrastructure.AI.Runtime;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "异常检测",
    Description = "Runs a simplified PatchCore-style anomaly detector with train/inference modes and feature-bank persistence.",
    Category = "AI检测",
    IconName = "anomaly-detection",
    Keywords = new[] { "anomaly", "patchcore", "feature bank", "异常检测" },
    Version = "1.0.0"
)]
[AlgorithmInfo(
    Name = "Simplified PatchCore",
    CoreApi = "OpenCvSharp + memory-bank nearest-neighbor",
    TimeComplexity = "O(P * B)",
    SpaceComplexity = "O(B)",
    Dependencies = new[] { "OpenCvSharp" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = false)]
[InputPort("NormalImages", "Normal Images", PortDataType.Any, IsRequired = false)]
[OutputPort("AnomalyScore", "Anomaly Score", PortDataType.Float)]
[OutputPort("IsAnomaly", "Is Anomaly", PortDataType.Boolean)]
[OutputPort("AnomalyMap", "Anomaly Map", PortDataType.Image)]
[OutputPort("AnomalyMask", "Anomaly Mask", PortDataType.Image)]
[OutputPort("FeatureBankPath", "Feature Bank Path", PortDataType.String)]
[OutputPort("PatchCount", "Patch Count", PortDataType.Integer)]
[OperatorParam("Mode", "Mode", "enum", DefaultValue = "inference", Options = new[] { "inference|Inference", "train|Train" })]
[OperatorParam("FeatureBankPath", "Feature Bank Path", "file", DefaultValue = "")]
[OperatorParam("SaveFeatureBankPath", "Save Feature Bank Path", "file", DefaultValue = "")]
[OperatorParam("ModelId", "Model Id", "string", DefaultValue = "")]
[OperatorParam("ModelCatalogPath", "Model Catalog Path", "file", DefaultValue = "")]
[OperatorParam("Backbone", "Backbone", "string", DefaultValue = "simple_patchcore")]
[OperatorParam("PatchSize", "Patch Size", "int", DefaultValue = 32, Min = 4, Max = 256)]
[OperatorParam("PatchStride", "Patch Stride", "int", DefaultValue = 16, Min = 1, Max = 256)]
[OperatorParam("CoresetRatio", "Coreset Ratio", "double", DefaultValue = 0.2, Min = 0.01, Max = 1.0)]
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 0.35, Min = 0.0, Max = 1.0)]
public sealed class AnomalyDetectionOperator : OperatorBase
{
    private static readonly string[] SupportedCatalogTypes = ["anomaly_detection", "anomaly_feature_bank", "feature_bank"];

    public AnomalyDetectionOperator(ILogger<AnomalyDetectionOperator> logger)
        : base(logger)
    {
    }

    public override OperatorType OperatorType => OperatorType.AnomalyDetection;

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var mode = NormalizeMode(GetStringParam(@operator, "Mode", "inference"));
        var threshold = GetDoubleParam(@operator, "Threshold", 0.35, 0.0, 1.0);
        var options = new SimplePatchCoreOptions
        {
            PatchSize = GetIntParam(@operator, "PatchSize", 32, 4, 256),
            PatchStride = GetIntParam(@operator, "PatchStride", 16, 1, 256),
            CoresetRatio = GetDoubleParam(@operator, "CoresetRatio", 0.2, 0.01, 1.0)
        };

        if (mode == "train")
        {
            if (!TryGetNormalImages(inputs, out var normalImages, out var normalImagesError))
            {
                return OperatorExecutionOutput.Failure(normalImagesError);
            }

            SimplePatchCoreFeatureBank bank;
            try
            {
                bank = await RunCpuBoundWork(
                    () => SimplePatchCoreDetector.BuildFeatureBank(normalImages.Select(x => x.GetMat()), options),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Anomaly feature-bank training failed.");
                return OperatorExecutionOutput.Failure($"Anomaly training failed: {ex.Message}");
            }

            var savePath = ResolveFeatureBankSavePath(@operator);
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                try
                {
                    SimplePatchCoreDetector.Save(savePath, bank);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to save anomaly feature bank.");
                    return OperatorExecutionOutput.Failure($"Failed to save feature bank: {ex.Message}");
                }
            }

            var previewImage = ResolvePreviewImage(inputs, normalImages);
            SimplePatchCoreAnalysisResult analysisResult;
            try
            {
                analysisResult = await RunCpuBoundWork(
                    () => SimplePatchCoreDetector.Analyze(previewImage.GetMat(), bank, threshold),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Anomaly preview inference failed after training.");
                return OperatorExecutionOutput.Failure($"Anomaly preview failed: {ex.Message}");
            }

            return OperatorExecutionOutput.Success(CreateOutputs(analysisResult, savePath));
        }

        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return OperatorExecutionOutput.Failure("Input image is required for anomaly inference.");
        }

        string featureBankPath;
        try
        {
            featureBankPath = ResolveFeatureBankInputPath(@operator);
        }
        catch (Exception ex)
        {
            return OperatorExecutionOutput.Failure(ex.Message);
        }

        SimplePatchCoreFeatureBank loadedBank;
        try
        {
            loadedBank = SimplePatchCoreDetector.Load(featureBankPath);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load anomaly feature bank.");
            return OperatorExecutionOutput.Failure($"Failed to load feature bank: {ex.Message}");
        }

        try
        {
            var result = await RunCpuBoundWork(
                () => SimplePatchCoreDetector.Analyze(imageWrapper.GetMat(), loadedBank, threshold),
                cancellationToken);
            return OperatorExecutionOutput.Success(CreateOutputs(result, featureBankPath));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Anomaly inference failed.");
            return OperatorExecutionOutput.Failure($"Anomaly inference failed: {ex.Message}");
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var mode = NormalizeMode(GetStringParam(@operator, "Mode", "inference"));
        if (mode is not ("inference" or "train"))
        {
            return ValidationResult.Invalid("Mode must be 'inference' or 'train'.");
        }

        var backbone = GetStringParam(@operator, "Backbone", "simple_patchcore").Trim();
        if (!string.IsNullOrWhiteSpace(backbone) &&
            !string.Equals(backbone, "simple_patchcore", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(backbone, "patchcore", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Backbone currently supports 'simple_patchcore' only.");
        }

        _ = GetIntParam(@operator, "PatchSize", 32, 4, 256);
        _ = GetIntParam(@operator, "PatchStride", 16, 1, 256);
        _ = GetDoubleParam(@operator, "CoresetRatio", 0.2, 0.01, 1.0);
        _ = GetDoubleParam(@operator, "Threshold", 0.35, 0.0, 1.0);

        if (mode == "inference")
        {
            try
            {
                _ = ResolveFeatureBankInputPath(@operator);
            }
            catch (Exception ex)
            {
                return ValidationResult.Invalid(ex.Message);
            }
        }

        return ValidationResult.Valid();
    }

    private static Dictionary<string, object> CreateOutputs(SimplePatchCoreAnalysisResult result, string? featureBankPath)
    {
        return new Dictionary<string, object>
        {
            ["AnomalyScore"] = result.Score,
            ["IsAnomaly"] = result.IsAnomaly,
            ["AnomalyMap"] = new ImageWrapper(result.Heatmap),
            ["AnomalyMask"] = new ImageWrapper(result.Mask),
            ["FeatureBankPath"] = featureBankPath ?? string.Empty,
            ["PatchCount"] = result.PatchCount
        };
    }

    private string ResolveFeatureBankInputPath(Operator @operator)
    {
        var explicitPath = GetStringParam(@operator, "FeatureBankPath", string.Empty);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var resolvedExplicitPath = Path.GetFullPath(explicitPath);
            if (!File.Exists(resolvedExplicitPath))
            {
                throw new InvalidOperationException($"Feature bank file not found: {resolvedExplicitPath}");
            }

            return resolvedExplicitPath;
        }

        var modelId = GetStringParam(@operator, "ModelId", string.Empty);
        var catalogPath = GetStringParam(@operator, "ModelCatalogPath", string.Empty);
        var resolved = ModelCatalog.ResolveExplicitOrCatalogPath(
            explicitPath: null,
            modelId,
            catalogPath,
            SupportedCatalogTypes,
            out _);

        if (!File.Exists(resolved))
        {
            throw new InvalidOperationException($"Feature bank file not found: {resolved}");
        }

        return resolved;
    }

    private string ResolveFeatureBankSavePath(Operator @operator)
    {
        var explicitSavePath = GetStringParam(@operator, "SaveFeatureBankPath", string.Empty);
        if (!string.IsNullOrWhiteSpace(explicitSavePath))
        {
            return Path.GetFullPath(explicitSavePath);
        }

        var featureBankPath = GetStringParam(@operator, "FeatureBankPath", string.Empty);
        if (!string.IsNullOrWhiteSpace(featureBankPath))
        {
            return Path.GetFullPath(featureBankPath);
        }

        var modelId = GetStringParam(@operator, "ModelId", string.Empty);
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return string.Empty;
        }

        return ModelCatalog.ResolveExplicitOrCatalogPath(
            explicitPath: null,
            modelId,
            GetStringParam(@operator, "ModelCatalogPath", string.Empty),
            SupportedCatalogTypes,
            out _);
    }

    private static ImageWrapper ResolvePreviewImage(Dictionary<string, object>? inputs, IReadOnlyList<ImageWrapper> normalImages)
    {
        return ImageWrapper.TryGetFromInputs(inputs, "Image", out var preview) && preview != null
            ? preview
            : normalImages[0];
    }

    private static bool TryGetNormalImages(Dictionary<string, object>? inputs, out List<ImageWrapper> images, out string error)
    {
        images = [];
        error = string.Empty;

        if (inputs == null || !inputs.TryGetValue("NormalImages", out var normalImagesObj) || normalImagesObj == null)
        {
            error = "NormalImages is required in train mode.";
            return false;
        }

        if (normalImagesObj is IEnumerable<ImageWrapper> wrappers)
        {
            images = wrappers.Where(x => x != null).ToList();
            if (images.Count == 0)
            {
                error = "NormalImages is empty.";
                return false;
            }

            return true;
        }

        if (normalImagesObj is IEnumerable<object> values)
        {
            foreach (var value in values)
            {
                if (ImageWrapper.TryGetFromObject(value, out var image) && image != null)
                {
                    images.Add(image);
                }
            }
        }

        if (images.Count == 0)
        {
            error = "NormalImages must contain at least one valid image.";
            return false;
        }

        return true;
    }

    private static string NormalizeMode(string raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? "inference"
            : raw.Trim().ToLowerInvariant();
    }
}
