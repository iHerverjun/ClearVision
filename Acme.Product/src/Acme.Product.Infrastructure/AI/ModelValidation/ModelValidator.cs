using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.Product.Infrastructure.Services;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.AI.ModelValidation;

public enum ModelInputChannelOrder
{
    RGB = 0,
    BGR = 1
}

public sealed class ModelValidationPreprocessingOptions
{
    public int InputWidth { get; init; }
    public int InputHeight { get; init; }
    public float[] Mean { get; init; } = [0f, 0f, 0f];
    public float[] Std { get; init; } = [1f, 1f, 1f];
    public bool ScaleToUnitRange { get; init; } = true;
    public ModelInputChannelOrder ChannelOrder { get; init; } = ModelInputChannelOrder.RGB;

    public ModelValidationPreprocessingOptions Clone()
    {
        return new ModelValidationPreprocessingOptions
        {
            InputWidth = InputWidth,
            InputHeight = InputHeight,
            Mean = (float[])Mean.Clone(),
            Std = (float[])Std.Clone(),
            ScaleToUnitRange = ScaleToUnitRange,
            ChannelOrder = ChannelOrder
        };
    }
}

public sealed class ModelValidationPreprocessingCheckResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
}

public sealed class ModelValidationOutputComparison
{
    public string OutputName { get; init; } = string.Empty;
    public int[] ExpectedShape { get; init; } = [];
    public int[] ActualShape { get; init; } = [];
    public double MeanSquaredError { get; init; }
    public double MeanAbsoluteError { get; init; }
    public double MaxAbsoluteError { get; init; }
    public double MaxRelativeErrorPercent { get; init; }
    public bool IsWithinTolerance { get; init; }
}

public sealed class ModelValidationResult
{
    public bool IsValid { get; init; }
    public string ModelPath { get; init; } = string.Empty;
    public string ExecutionProvider { get; init; } = string.Empty;
    public string? ImagePath { get; init; }
    public string? ExpectedOutputPath { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public ModelValidationPreprocessingOptions Preprocessing { get; init; } = new();
    public ModelValidationPreprocessingCheckResult PreprocessingCheck { get; init; } = new();
    public List<ModelValidationOutputComparison> OutputComparisons { get; init; } = [];
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    public double MaxError => OutputComparisons.Count == 0 ? 0d : OutputComparisons.Max(x => x.MaxAbsoluteError);
    public double MeanError => OutputComparisons.Count == 0 ? 0d : OutputComparisons.Average(x => x.MeanAbsoluteError);
    public double MaxRelativeErrorPercent => OutputComparisons.Count == 0 ? 0d : OutputComparisons.Max(x => x.MaxRelativeErrorPercent);
}

public sealed class ModelValidationExpectedOutput
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("shape")]
    public int[] Shape { get; set; } = [];

    [JsonPropertyName("values")]
    public float[] Values { get; set; } = [];
}

public sealed class ModelValidationExpectationSet
{
    [JsonPropertyName("outputs")]
    public List<ModelValidationExpectedOutput> Outputs { get; set; } = [];

    public static ModelValidationExpectationSet Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Expected output path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Expected output file not found.", path);
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("outputs", out _))
        {
            var set = JsonSerializer.Deserialize<ModelValidationExpectationSet>(json, SerializerOptions);
            return set ?? new ModelValidationExpectationSet();
        }

        var single = JsonSerializer.Deserialize<ModelValidationExpectedOutput>(json, SerializerOptions);
        return new ModelValidationExpectationSet
        {
            Outputs = single == null ? [] : [single]
        };
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class ModelValidationTestCase
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [JsonPropertyName("expectedOutputPath")]
    public string ExpectedOutputPath { get; set; } = string.Empty;

    [JsonPropertyName("allowedMaxRelativeErrorPercent")]
    public double? AllowedMaxRelativeErrorPercent { get; set; }
}

public sealed class ModelValidationTestSuite
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("cases")]
    public List<ModelValidationTestCase> Cases { get; set; } = [];

    public static ModelValidationTestSuite Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Suite path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Suite file not found.", path);
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        var suite = JsonSerializer.Deserialize<ModelValidationTestSuite>(json, SerializerOptions);
        return suite ?? new ModelValidationTestSuite();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class ModelValidationSuiteResult
{
    public string SuiteName { get; init; } = string.Empty;
    public List<ModelValidationResult> CaseResults { get; init; } = [];
    public bool IsValid => CaseResults.All(x => x.IsValid);
    public int PassedCount => CaseResults.Count(x => x.IsValid);
    public int FailedCount => CaseResults.Count(x => !x.IsValid);
}

public sealed class ModelValidator : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;

    public ModelValidator(
        string modelPath,
        string executionProvider = "cpu",
        int gpuDeviceId = 0)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new ArgumentException("Model path is required.", nameof(modelPath));
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Model file not found.", modelPath);
        }

        ModelPath = Path.GetFullPath(modelPath);
        ExecutionProvider = NormalizeExecutionProvider(executionProvider);
        _session = new InferenceSession(ModelPath, CreateSessionOptions(ExecutionProvider, gpuDeviceId));
        _inputName = _session.InputMetadata.Keys.First();
        Preprocessing = CreateDefaultPreprocessingOptions(_session, _inputName);
    }

    public string ModelPath { get; }

    public string ExecutionProvider { get; }

    public ModelValidationPreprocessingOptions Preprocessing { get; private set; }

    public void SetPreprocessing(
        int inputWidth,
        int inputHeight,
        float[]? mean = null,
        float[]? std = null,
        ModelInputChannelOrder channelOrder = ModelInputChannelOrder.RGB,
        bool scaleToUnitRange = true)
    {
        Preprocessing = new ModelValidationPreprocessingOptions
        {
            InputWidth = inputWidth,
            InputHeight = inputHeight,
            Mean = mean is { Length: > 0 } ? (float[])mean.Clone() : [0f, 0f, 0f],
            Std = std is { Length: > 0 } ? (float[])std.Clone() : [1f, 1f, 1f],
            ChannelOrder = channelOrder,
            ScaleToUnitRange = scaleToUnitRange
        };
    }

    public ModelValidationPreprocessingCheckResult ValidatePreprocessing()
    {
        var result = new ModelValidationPreprocessingCheckResult();

        if (Preprocessing.InputWidth <= 0 || Preprocessing.InputHeight <= 0)
        {
            result.Errors.Add("Input size must be positive.");
        }

        if (Preprocessing.Mean.Length != 3)
        {
            result.Errors.Add("Mean must contain exactly 3 values.");
        }

        if (Preprocessing.Std.Length != 3)
        {
            result.Errors.Add("Std must contain exactly 3 values.");
        }

        if (Preprocessing.Std.Any(x => x <= 0f))
        {
            result.Errors.Add("Std values must be greater than zero.");
        }

        var inputMetadata = _session.InputMetadata[_inputName];
        var dimensions = inputMetadata.Dimensions;
        if (dimensions.Length != 4)
        {
            result.Warnings.Add($"Model input rank is {dimensions.Length}, current validator is tuned for 4D image tensors.");
            return result;
        }

        var channels = dimensions[1];
        var height = dimensions[2];
        var width = dimensions[3];

        if (channels > 0 && channels != 3)
        {
            result.Warnings.Add($"Model channel count is {channels}, current validator assumes 3-channel images.");
        }

        if (height > 0 && height != Preprocessing.InputHeight)
        {
            result.Errors.Add($"Configured input height {Preprocessing.InputHeight} does not match model input height {height}.");
        }

        if (width > 0 && width != Preprocessing.InputWidth)
        {
            result.Errors.Add($"Configured input width {Preprocessing.InputWidth} does not match model input width {width}.");
        }

        return result;
    }

    public ModelValidationResult Validate(
        string imagePath,
        string expectedOutputPath,
        double allowedMaxRelativeErrorPercent = 5.0)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (image.Empty())
        {
            throw new InvalidOperationException($"Unable to load image: {imagePath}");
        }

        return Validate(image, expectedOutputPath, allowedMaxRelativeErrorPercent, imagePath);
    }

    public ModelValidationResult Validate(
        Mat image,
        string expectedOutputPath,
        double allowedMaxRelativeErrorPercent = 5.0,
        string? imagePath = null)
    {
        if (image == null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        if (image.Empty())
        {
            throw new ArgumentException("Input image is empty.", nameof(image));
        }

        var preprocessingCheck = ValidatePreprocessing();
        var errors = new List<string>(preprocessingCheck.Errors);
        var warnings = new List<string>(preprocessingCheck.Warnings);
        var comparisons = new List<ModelValidationOutputComparison>();

        if (preprocessingCheck.IsValid)
        {
            var inputTensor = PreprocessImage(image, Preprocessing);
            var actualOutputs = RunInference(inputTensor);
            var expectedOutputs = ModelValidationExpectationSet.Load(expectedOutputPath);

            foreach (var expectedOutput in expectedOutputs.Outputs)
            {
                if (!TryGetActualOutput(actualOutputs, expectedOutput.Name, out var actualOutput))
                {
                    errors.Add($"Expected output '{expectedOutput.Name}' was not produced by the model.");
                    continue;
                }

                comparisons.Add(CompareOutputs(actualOutput, expectedOutput, allowedMaxRelativeErrorPercent, errors));
            }

            if (comparisons.Count == 0 && expectedOutputs.Outputs.Count > 0)
            {
                errors.Add("No output comparisons were completed.");
            }
        }

        var isValid = errors.Count == 0 && comparisons.All(x => x.IsWithinTolerance);
        if (!comparisons.All(x => x.IsWithinTolerance))
        {
            errors.Add("One or more outputs exceeded the configured error tolerance.");
        }

        return new ModelValidationResult
        {
            IsValid = isValid,
            ModelPath = ModelPath,
            ExecutionProvider = ExecutionProvider,
            ImagePath = imagePath == null ? null : Path.GetFullPath(imagePath),
            ExpectedOutputPath = Path.GetFullPath(expectedOutputPath),
            Preprocessing = Preprocessing.Clone(),
            PreprocessingCheck = preprocessingCheck,
            OutputComparisons = comparisons,
            Errors = errors,
            Warnings = warnings,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    public ModelValidationSuiteResult ValidateSuite(
        string suitePath,
        double defaultAllowedMaxRelativeErrorPercent = 5.0)
    {
        var suite = ModelValidationTestSuite.Load(suitePath);
        var suiteDirectory = Path.GetDirectoryName(Path.GetFullPath(suitePath)) ?? Environment.CurrentDirectory;
        var results = new List<ModelValidationResult>(suite.Cases.Count);

        foreach (var testCase in suite.Cases)
        {
            var imagePath = ResolveSuitePath(suiteDirectory, testCase.ImagePath);
            var expectedOutputPath = ResolveSuitePath(suiteDirectory, testCase.ExpectedOutputPath);
            var tolerance = testCase.AllowedMaxRelativeErrorPercent ?? defaultAllowedMaxRelativeErrorPercent;
            results.Add(Validate(imagePath, expectedOutputPath, tolerance));
        }

        return new ModelValidationSuiteResult
        {
            SuiteName = suite.Name,
            CaseResults = results
        };
    }

    public static string WriteMarkdownTemplate(string outputPath)
    {
        const string template = """
# Model Validation Report

- GeneratedAtUtc: {{GeneratedAtUtc}}
- ModelPath: {{ModelPath}}
- ExecutionProvider: {{ExecutionProvider}}
- ImagePath: {{ImagePath}}
- ExpectedOutputPath: {{ExpectedOutputPath}}
- IsValid: {{IsValid}}
- MeanError: {{MeanError}}
- MaxError: {{MaxError}}
- MaxRelativeErrorPercent: {{MaxRelativeErrorPercent}}

## Preprocessing

- InputSize: {{InputWidth}}x{{InputHeight}}
- ChannelOrder: {{ChannelOrder}}
- ScaleToUnitRange: {{ScaleToUnitRange}}
- Mean: {{Mean}}
- Std: {{Std}}

## Output Comparisons

{{OutputComparisons}}

## Errors

{{Errors}}

## Warnings

{{Warnings}}
""";

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory);
        File.WriteAllText(outputPath, template, Encoding.UTF8);
        return outputPath;
    }

    public static string WriteMarkdownReport(string outputPath, ModelValidationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Model Validation Report");
        builder.AppendLine();
        builder.AppendLine($"- GeneratedAtUtc: {result.GeneratedAtUtc:O}");
        builder.AppendLine($"- ModelPath: {result.ModelPath}");
        builder.AppendLine($"- ExecutionProvider: {result.ExecutionProvider}");
        builder.AppendLine($"- ImagePath: {result.ImagePath ?? "(in-memory image)"}");
        builder.AppendLine($"- ExpectedOutputPath: {result.ExpectedOutputPath ?? string.Empty}");
        builder.AppendLine($"- IsValid: {result.IsValid}");
        builder.AppendLine($"- MeanError: {result.MeanError:F6}");
        builder.AppendLine($"- MaxError: {result.MaxError:F6}");
        builder.AppendLine($"- MaxRelativeErrorPercent: {result.MaxRelativeErrorPercent:F4}%");
        builder.AppendLine();
        builder.AppendLine("## Preprocessing");
        builder.AppendLine();
        builder.AppendLine($"- InputSize: {result.Preprocessing.InputWidth}x{result.Preprocessing.InputHeight}");
        builder.AppendLine($"- ChannelOrder: {result.Preprocessing.ChannelOrder}");
        builder.AppendLine($"- ScaleToUnitRange: {result.Preprocessing.ScaleToUnitRange}");
        builder.AppendLine($"- Mean: {string.Join(", ", result.Preprocessing.Mean)}");
        builder.AppendLine($"- Std: {string.Join(", ", result.Preprocessing.Std)}");
        builder.AppendLine();
        builder.AppendLine("## Output Comparisons");
        builder.AppendLine();

        if (result.OutputComparisons.Count == 0)
        {
            builder.AppendLine("- No output comparisons were recorded.");
        }
        else
        {
            foreach (var comparison in result.OutputComparisons)
            {
                builder.AppendLine($"### {comparison.OutputName}");
                builder.AppendLine($"- ExpectedShape: [{string.Join(", ", comparison.ExpectedShape)}]");
                builder.AppendLine($"- ActualShape: [{string.Join(", ", comparison.ActualShape)}]");
                builder.AppendLine($"- MeanSquaredError: {comparison.MeanSquaredError:F8}");
                builder.AppendLine($"- MeanAbsoluteError: {comparison.MeanAbsoluteError:F8}");
                builder.AppendLine($"- MaxAbsoluteError: {comparison.MaxAbsoluteError:F8}");
                builder.AppendLine($"- MaxRelativeErrorPercent: {comparison.MaxRelativeErrorPercent:F4}%");
                builder.AppendLine($"- IsWithinTolerance: {comparison.IsWithinTolerance}");
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Errors");
        builder.AppendLine();
        if (result.Errors.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                builder.AppendLine($"- {error}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        if (result.Warnings.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var warning in result.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory);
        File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
        return outputPath;
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    private static string ResolveSuitePath(string suiteDirectory, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(suiteDirectory, path));
    }

    private static string NormalizeExecutionProvider(string executionProvider)
    {
        return string.IsNullOrWhiteSpace(executionProvider)
            ? "cpu"
            : executionProvider.Trim().ToLowerInvariant();
    }

    private static SessionOptions CreateSessionOptions(string executionProvider, int gpuDeviceId)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        if (executionProvider == "cuda" && GpuAvailabilityChecker.IsCudaAvailable)
        {
            options.AppendExecutionProvider_CUDA(gpuDeviceId);
            return options;
        }

        return options;
    }

    private static ModelValidationPreprocessingOptions CreateDefaultPreprocessingOptions(InferenceSession session, string inputName)
    {
        var dimensions = session.InputMetadata[inputName].Dimensions;
        var height = dimensions.Length >= 3 && dimensions[2] > 0 ? dimensions[2] : 224;
        var width = dimensions.Length >= 4 && dimensions[3] > 0 ? dimensions[3] : 224;

        return new ModelValidationPreprocessingOptions
        {
            InputWidth = width,
            InputHeight = height,
            Mean = [0f, 0f, 0f],
            Std = [1f, 1f, 1f],
            ScaleToUnitRange = true,
            ChannelOrder = ModelInputChannelOrder.RGB
        };
    }

    private static DenseTensor<float> PreprocessImage(Mat image, ModelValidationPreprocessingOptions options)
    {
        using var prepared = EnsureThreeChannel(image);
        using var resized = new Mat();
        Cv2.Resize(prepared, resized, new Size(options.InputWidth, options.InputHeight), 0, 0, InterpolationFlags.Linear);

        using var floatImage = new Mat();
        resized.ConvertTo(floatImage, MatType.CV_32FC3, options.ScaleToUnitRange ? 1.0 / 255.0 : 1.0);

        var tensor = new DenseTensor<float>([1, 3, options.InputHeight, options.InputWidth]);
        var indexer = floatImage.GetGenericIndexer<Vec3f>();

        for (var y = 0; y < options.InputHeight; y++)
        {
            for (var x = 0; x < options.InputWidth; x++)
            {
                var pixel = indexer[y, x];
                var c0 = options.ChannelOrder == ModelInputChannelOrder.RGB ? pixel.Item2 : pixel.Item0;
                var c1 = pixel.Item1;
                var c2 = options.ChannelOrder == ModelInputChannelOrder.RGB ? pixel.Item0 : pixel.Item2;

                tensor[0, 0, y, x] = (c0 - options.Mean[0]) / options.Std[0];
                tensor[0, 1, y, x] = (c1 - options.Mean[1]) / options.Std[1];
                tensor[0, 2, y, x] = (c2 - options.Mean[2]) / options.Std[2];
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

        throw new InvalidOperationException($"Unsupported image channel count: {image.Channels()}");
    }

    private Dictionary<string, ModelOutputSnapshot> RunInference(DenseTensor<float> inputTensor)
    {
        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)]);
        var outputs = new Dictionary<string, ModelOutputSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var output in results)
        {
            var tensor = output.AsTensor<float>();
            outputs[output.Name] = new ModelOutputSnapshot(output.Name, tensor.Dimensions.ToArray(), tensor.ToArray());
        }

        return outputs;
    }

    private static bool TryGetActualOutput(
        IReadOnlyDictionary<string, ModelOutputSnapshot> actualOutputs,
        string expectedName,
        out ModelOutputSnapshot snapshot)
    {
        if (actualOutputs.TryGetValue(expectedName, out snapshot))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(expectedName) && actualOutputs.Count == 1)
        {
            snapshot = actualOutputs.Values.First();
            return true;
        }

        snapshot = default;
        return false;
    }

    private static ModelValidationOutputComparison CompareOutputs(
        ModelOutputSnapshot actualOutput,
        ModelValidationExpectedOutput expectedOutput,
        double allowedMaxRelativeErrorPercent,
        List<string> errors)
    {
        if (!expectedOutput.Shape.SequenceEqual(actualOutput.Shape))
        {
            errors.Add(
                $"Output '{expectedOutput.Name}' shape mismatch. Expected [{string.Join(", ", expectedOutput.Shape)}], actual [{string.Join(", ", actualOutput.Shape)}].");

            return new ModelValidationOutputComparison
            {
                OutputName = actualOutput.Name,
                ExpectedShape = expectedOutput.Shape,
                ActualShape = actualOutput.Shape,
                IsWithinTolerance = false
            };
        }

        if (expectedOutput.Values.Length != actualOutput.Values.Length)
        {
            errors.Add(
                $"Output '{expectedOutput.Name}' value length mismatch. Expected {expectedOutput.Values.Length}, actual {actualOutput.Values.Length}.");

            return new ModelValidationOutputComparison
            {
                OutputName = actualOutput.Name,
                ExpectedShape = expectedOutput.Shape,
                ActualShape = actualOutput.Shape,
                IsWithinTolerance = false
            };
        }

        double sumSquared = 0d;
        double sumAbsolute = 0d;
        double maxAbsolute = 0d;
        double maxRelativePercent = 0d;

        for (var i = 0; i < expectedOutput.Values.Length; i++)
        {
            var expected = expectedOutput.Values[i];
            var actual = actualOutput.Values[i];
            var absolute = Math.Abs(actual - expected);
            var relativePercent = absolute / Math.Max(Math.Abs(expected), 1e-6f) * 100d;

            sumSquared += absolute * absolute;
            sumAbsolute += absolute;
            maxAbsolute = Math.Max(maxAbsolute, absolute);
            maxRelativePercent = Math.Max(maxRelativePercent, relativePercent);
        }

        var count = expectedOutput.Values.Length;
        var comparison = new ModelValidationOutputComparison
        {
            OutputName = actualOutput.Name,
            ExpectedShape = expectedOutput.Shape,
            ActualShape = actualOutput.Shape,
            MeanSquaredError = sumSquared / count,
            MeanAbsoluteError = sumAbsolute / count,
            MaxAbsoluteError = maxAbsolute,
            MaxRelativeErrorPercent = maxRelativePercent,
            IsWithinTolerance = maxRelativePercent <= allowedMaxRelativeErrorPercent
        };

        if (!comparison.IsWithinTolerance)
        {
            errors.Add(
                $"Output '{actualOutput.Name}' exceeded tolerance {allowedMaxRelativeErrorPercent:F2}%. Actual max relative error: {comparison.MaxRelativeErrorPercent:F4}%.");
        }

        return comparison;
    }

    private readonly record struct ModelOutputSnapshot(string Name, int[] Shape, float[] Values);
}
