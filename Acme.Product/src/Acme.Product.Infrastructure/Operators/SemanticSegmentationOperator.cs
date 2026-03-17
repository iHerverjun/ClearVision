using System.Collections.Concurrent;
using System.Text.Json;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "语义分割",
    Description = "Runs an ONNX semantic segmentation model and returns class map, colored visualization, and per-class masks.",
    Category = "AI检测",
    IconName = "semantic-segmentation",
    Keywords = new[] { "semantic segmentation", "segmentation", "onnx", "mask", "语义分割" },
    Version = "1.0.0"
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("SegmentationMap", "Segmentation Map", PortDataType.Image)]
[OutputPort("ColoredMap", "Colored Map", PortDataType.Image)]
[OutputPort("ClassMasks", "Class Masks", PortDataType.Any)]
[OutputPort("ClassCount", "Class Count", PortDataType.Integer)]
[OutputPort("PresentClasses", "Present Classes", PortDataType.Any)]
[OperatorParam("ModelPath", "Model Path", "file", DefaultValue = "")]
[OperatorParam("InputSize", "Input Size", "string", DefaultValue = "512,512", Description = "Width,Height")]
[OperatorParam("NumClasses", "Num Classes", "int", DefaultValue = 21, Min = 2, Max = 4096)]
[OperatorParam("ClassNames", "Class Names", "string", DefaultValue = "", Description = "JSON array or comma-separated names")]
[OperatorParam("ExecutionProvider", "Execution Provider", "enum", DefaultValue = "cpu", Options = new[] { "cpu|CPU", "cuda|CUDA" })]
[OperatorParam("ScaleToUnitRange", "Scale To Unit Range", "bool", DefaultValue = true)]
[OperatorParam("ChannelOrder", "Channel Order", "enum", DefaultValue = "RGB", Options = new[] { "RGB|RGB", "BGR|BGR" })]
[OperatorParam("Mean", "Mean", "string", DefaultValue = "0,0,0")]
[OperatorParam("Std", "Std", "string", DefaultValue = "1,1,1")]
public sealed class SemanticSegmentationOperator : OperatorBase
{
    private static readonly ConcurrentDictionary<string, InferenceSession> SessionCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SessionLocks = new(StringComparer.OrdinalIgnoreCase);

    public SemanticSegmentationOperator(ILogger<SemanticSegmentationOperator> logger)
        : base(logger)
    {
    }

    public override OperatorType OperatorType => OperatorType.SemanticSegmentation;

    protected override async Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return OperatorExecutionOutput.Failure("Input image is required.");
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return OperatorExecutionOutput.Failure("Input image is invalid.");
        }

        var modelPath = GetStringParam(@operator, "ModelPath", string.Empty);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return OperatorExecutionOutput.Failure("ModelPath is required.");
        }

        if (!File.Exists(modelPath))
        {
            return OperatorExecutionOutput.Failure($"Model file not found: {modelPath}");
        }

        if (!TryParseSize(GetStringParam(@operator, "InputSize", "512,512"), out var inputWidth, out var inputHeight))
        {
            return OperatorExecutionOutput.Failure("InputSize must be in 'width,height' format.");
        }

        var numClasses = GetIntParam(@operator, "NumClasses", 21, min: 2, max: 4096);
        if (!TryParseFloatTriplet(GetStringParam(@operator, "Mean", "0,0,0"), out var mean) ||
            !TryParseFloatTriplet(GetStringParam(@operator, "Std", "1,1,1"), out var std) ||
            std.Any(x => x <= 0f))
        {
            return OperatorExecutionOutput.Failure("Mean/Std must contain 3 numeric values and Std must be > 0.");
        }

        var executionProvider = NormalizeExecutionProvider(GetStringParam(@operator, "ExecutionProvider", "cpu"));
        var useUnitRange = GetBoolParam(@operator, "ScaleToUnitRange", true);
        var channelOrder = ParseChannelOrder(GetStringParam(@operator, "ChannelOrder", "RGB"));
        var classNames = ParseClassNames(GetStringParam(@operator, "ClassNames", string.Empty), numClasses);

        InferenceSession session;
        try
        {
            session = await GetOrCreateSessionAsync(modelPath, executionProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load segmentation model.");
            return OperatorExecutionOutput.Failure($"Failed to load segmentation model: {ex.Message}");
        }

        SegmentationExecutionResult executionResult;
        try
        {
            executionResult = await RunCpuBoundWork(
                () => ExecuteSegmentation(session, src, inputWidth, inputHeight, numClasses, classNames, channelOrder, mean, std, useUnitRange),
                cancellationToken);
        }
        catch (OnnxRuntimeException ex)
        {
            Logger.LogError(ex, "Segmentation inference failed.");
            return OperatorExecutionOutput.Failure($"Segmentation inference failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Segmentation execution failed.");
            return OperatorExecutionOutput.Failure($"Segmentation execution failed: {ex.Message}");
        }

        var output = new Dictionary<string, object>
        {
            ["SegmentationMap"] = new ImageWrapper(executionResult.SegmentationMap),
            ["ColoredMap"] = new ImageWrapper(executionResult.ColoredMap),
            ["ClassMasks"] = executionResult.ClassMasks.ToDictionary(
                pair => pair.Key,
                pair => (object)new ImageWrapper(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            ["ClassCount"] = executionResult.PresentClasses.Length,
            ["PresentClasses"] = executionResult.PresentClasses
        };

        return OperatorExecutionOutput.Success(output);
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var modelPath = GetStringParam(@operator, "ModelPath", string.Empty);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return ValidationResult.Invalid("ModelPath is required.");
        }

        if (!File.Exists(modelPath))
        {
            return ValidationResult.Invalid($"Model file not found: {modelPath}");
        }

        if (!TryParseSize(GetStringParam(@operator, "InputSize", "512,512"), out _, out _))
        {
            return ValidationResult.Invalid("InputSize must be in 'width,height' format.");
        }

        _ = GetIntParam(@operator, "NumClasses", 21, min: 2, max: 4096);

        if (!TryParseFloatTriplet(GetStringParam(@operator, "Mean", "0,0,0"), out _))
        {
            return ValidationResult.Invalid("Mean must contain 3 numeric values.");
        }

        if (!TryParseFloatTriplet(GetStringParam(@operator, "Std", "1,1,1"), out var std) || std.Any(x => x <= 0f))
        {
            return ValidationResult.Invalid("Std must contain 3 positive numeric values.");
        }

        var executionProvider = NormalizeExecutionProvider(GetStringParam(@operator, "ExecutionProvider", "cpu"));
        if (executionProvider is not ("cpu" or "cuda"))
        {
            return ValidationResult.Invalid("ExecutionProvider must be 'cpu' or 'cuda'.");
        }

        _ = ParseChannelOrder(GetStringParam(@operator, "ChannelOrder", "RGB"));
        _ = ParseClassNames(GetStringParam(@operator, "ClassNames", string.Empty), GetIntParam(@operator, "NumClasses", 21, min: 2, max: 4096));

        return ValidationResult.Valid();
    }

    private async Task<InferenceSession> GetOrCreateSessionAsync(string modelPath, string executionProvider, CancellationToken cancellationToken)
    {
        var resolvedModelPath = Path.GetFullPath(modelPath);
        var effectiveProvider = executionProvider == "cuda" && !GpuAvailabilityChecker.IsCudaAvailable ? "cpu" : executionProvider;
        var cacheKey = $"{resolvedModelPath}|{effectiveProvider}";

        if (SessionCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var gate = SessionLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (SessionCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
            };

            if (effectiveProvider == "cuda")
            {
                options.AppendExecutionProvider_CUDA(0);
            }

            var session = new InferenceSession(resolvedModelPath, options);
            SessionCache[cacheKey] = session;
            return session;
        }
        finally
        {
            gate.Release();
        }
    }

    private static SegmentationExecutionResult ExecuteSegmentation(
        InferenceSession session,
        Mat sourceImage,
        int inputWidth,
        int inputHeight,
        int numClasses,
        string[] classNames,
        SegmentationChannelOrder channelOrder,
        float[] mean,
        float[] std,
        bool scaleToUnitRange)
    {
        var inputName = session.InputMetadata.Keys.First();
        var tensor = PreprocessImage(sourceImage, inputWidth, inputHeight, channelOrder, mean, std, scaleToUnitRange);

        using var results = session.Run([NamedOnnxValue.CreateFromTensor(inputName, tensor)]);
        var output = results.First().AsTensor<float>();
        var dims = output.Dimensions.ToArray();

        if (dims.Length != 4)
        {
            throw new InvalidOperationException($"Segmentation output must be 4D. Actual rank: {dims.Length}.");
        }

        var isChannelsFirst = dims[1] == numClasses;
        var isChannelsLast = dims[3] == numClasses;
        if (!isChannelsFirst && !isChannelsLast)
        {
            throw new InvalidOperationException(
                $"Unable to infer class dimension from output shape [{string.Join(", ", dims)}] with NumClasses={numClasses}.");
        }

        var outputHeight = isChannelsFirst ? dims[2] : dims[1];
        var outputWidth = isChannelsFirst ? dims[3] : dims[2];

        var classMapType = numClasses <= byte.MaxValue ? MatType.CV_8UC1 : MatType.CV_16UC1;
        using var smallClassMap = new Mat(outputHeight, outputWidth, classMapType, Scalar.Black);
        var presentClasses = new HashSet<int>();

        if (classMapType == MatType.CV_8UC1)
        {
            var classMapIndexer = smallClassMap.GetGenericIndexer<byte>();
            for (var y = 0; y < outputHeight; y++)
            {
                for (var x = 0; x < outputWidth; x++)
                {
                    var bestClass = 0;
                    var bestScore = float.NegativeInfinity;
                    for (var c = 0; c < numClasses; c++)
                    {
                        var score = isChannelsFirst ? output[0, c, y, x] : output[0, y, x, c];
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestClass = c;
                        }
                    }

                    classMapIndexer[y, x] = (byte)bestClass;
                    presentClasses.Add(bestClass);
                }
            }
        }
        else
        {
            var classMapIndexer = smallClassMap.GetGenericIndexer<ushort>();
            for (var y = 0; y < outputHeight; y++)
            {
                for (var x = 0; x < outputWidth; x++)
                {
                    var bestClass = 0;
                    var bestScore = float.NegativeInfinity;
                    for (var c = 0; c < numClasses; c++)
                    {
                        var score = isChannelsFirst ? output[0, c, y, x] : output[0, y, x, c];
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestClass = c;
                        }
                    }

                    classMapIndexer[y, x] = (ushort)bestClass;
                    presentClasses.Add(bestClass);
                }
            }
        }

        var finalClassMap = new Mat();
        Cv2.Resize(smallClassMap, finalClassMap, sourceImage.Size(), 0, 0, InterpolationFlags.Nearest);

        var coloredMap = BuildColoredMap(finalClassMap, numClasses);
        var classMasks = BuildClassMasks(finalClassMap, presentClasses, classNames);

        return new SegmentationExecutionResult(
            finalClassMap,
            coloredMap,
            classMasks,
            presentClasses.Select(c => classNames[c]).ToArray());
    }

    private static DenseTensor<float> PreprocessImage(
        Mat sourceImage,
        int inputWidth,
        int inputHeight,
        SegmentationChannelOrder channelOrder,
        float[] mean,
        float[] std,
        bool scaleToUnitRange)
    {
        using var prepared = EnsureThreeChannel(sourceImage);
        using var resized = new Mat();
        Cv2.Resize(prepared, resized, new Size(inputWidth, inputHeight), 0, 0, InterpolationFlags.Linear);

        using var floatImage = new Mat();
        resized.ConvertTo(floatImage, MatType.CV_32FC3, scaleToUnitRange ? 1.0 / 255.0 : 1.0);

        var tensor = new DenseTensor<float>([1, 3, inputHeight, inputWidth]);
        var indexer = floatImage.GetGenericIndexer<Vec3f>();

        for (var y = 0; y < inputHeight; y++)
        {
            for (var x = 0; x < inputWidth; x++)
            {
                var pixel = indexer[y, x];
                var c0 = channelOrder == SegmentationChannelOrder.RGB ? pixel.Item2 : pixel.Item0;
                var c1 = pixel.Item1;
                var c2 = channelOrder == SegmentationChannelOrder.RGB ? pixel.Item0 : pixel.Item2;

                tensor[0, 0, y, x] = (c0 - mean[0]) / std[0];
                tensor[0, 1, y, x] = (c1 - mean[1]) / std[1];
                tensor[0, 2, y, x] = (c2 - mean[2]) / std[2];
            }
        }

        return tensor;
    }

    private static Mat EnsureThreeChannel(Mat image)
    {
        if (image.Channels() == 3)
        {
            return image.Clone();
        }

        if (image.Channels() == 1)
        {
            var converted = new Mat();
            Cv2.CvtColor(image, converted, ColorConversionCodes.GRAY2BGR);
            return converted;
        }

        throw new InvalidOperationException($"Unsupported image channel count: {image.Channels()}.");
    }

    private static Mat BuildColoredMap(Mat classMap, int numClasses)
    {
        var colored = new Mat(classMap.Rows, classMap.Cols, MatType.CV_8UC3, Scalar.Black);
        var colorIndexer = colored.GetGenericIndexer<Vec3b>();

        if (classMap.Type() == MatType.CV_8UC1)
        {
            var classIndexer = classMap.GetGenericIndexer<byte>();
            for (var y = 0; y < classMap.Rows; y++)
            {
                for (var x = 0; x < classMap.Cols; x++)
                {
                    colorIndexer[y, x] = GetPaletteColor(classIndexer[y, x], numClasses);
                }
            }
        }
        else
        {
            var classIndexer = classMap.GetGenericIndexer<ushort>();
            for (var y = 0; y < classMap.Rows; y++)
            {
                for (var x = 0; x < classMap.Cols; x++)
                {
                    colorIndexer[y, x] = GetPaletteColor(classIndexer[y, x], numClasses);
                }
            }
        }

        return colored;
    }

    private static Dictionary<string, Mat> BuildClassMasks(Mat classMap, IEnumerable<int> presentClasses, string[] classNames)
    {
        var masks = new Dictionary<string, Mat>(StringComparer.OrdinalIgnoreCase);
        foreach (var classId in presentClasses.OrderBy(x => x))
        {
            var mask = new Mat();
            Cv2.Compare(classMap, classId, mask, CmpType.EQ);
            masks[classNames[classId]] = mask;
        }

        return masks;
    }

    private static string NormalizeExecutionProvider(string executionProvider)
    {
        return string.IsNullOrWhiteSpace(executionProvider)
            ? "cpu"
            : executionProvider.Trim().ToLowerInvariant();
    }

    private static SegmentationChannelOrder ParseChannelOrder(string raw)
    {
        return raw.Trim().ToUpperInvariant() switch
        {
            "RGB" => SegmentationChannelOrder.RGB,
            "BGR" => SegmentationChannelOrder.BGR,
            _ => throw new InvalidOperationException("ChannelOrder must be RGB or BGR.")
        };
    }

    private static bool TryParseSize(string raw, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out width) &&
               int.TryParse(parts[1], out height) &&
               width > 0 &&
               height > 0;
    }

    private static bool TryParseFloatTriplet(string raw, out float[] values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        var parsed = new float[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], out parsed[i]))
            {
                return false;
            }
        }

        values = parsed;
        return true;
    }

    private static string[] ParseClassNames(string raw, int numClasses)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Enumerable.Range(0, numClasses).Select(i => $"class_{i}").ToArray();
        }

        string[] names;
        try
        {
            names = raw.TrimStart().StartsWith("[", StringComparison.Ordinal)
                ? JsonSerializer.Deserialize<string[]>(raw) ?? []
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"ClassNames must be a JSON array or comma-separated list: {ex.Message}", ex);
        }

        if (names.Length == 0)
        {
            return Enumerable.Range(0, numClasses).Select(i => $"class_{i}").ToArray();
        }

        if (names.Length < numClasses)
        {
            var expanded = Enumerable.Range(0, numClasses).Select(i => i < names.Length ? names[i] : $"class_{i}").ToArray();
            return expanded;
        }

        return names.Take(numClasses).ToArray();
    }

    private static Vec3b GetPaletteColor(int classId, int numClasses)
    {
        if (numClasses <= 0)
        {
            return new Vec3b(0, 0, 0);
        }

        var hue = (classId * 53) % 180;
        using var hsv = new Mat(1, 1, MatType.CV_8UC3, new Scalar(hue, 220, 255));
        using var bgr = new Mat();
        Cv2.CvtColor(hsv, bgr, ColorConversionCodes.HSV2BGR);
        return bgr.Get<Vec3b>(0, 0);
    }

    private enum SegmentationChannelOrder
    {
        RGB = 0,
        BGR = 1
    }

    private sealed record SegmentationExecutionResult(
        Mat SegmentationMap,
        Mat ColoredMap,
        Dictionary<string, Mat> ClassMasks,
        string[] PresentClasses);
}
