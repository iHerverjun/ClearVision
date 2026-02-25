using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint7_Robustness")]
public class OperatorRobustnessTests
{
    private static readonly HashSet<OperatorType> LargeImageAuditTypes =
    [
        OperatorType.Filtering,
        OperatorType.Thresholding,
        OperatorType.EdgeDetection,
        OperatorType.Morphology,
        OperatorType.BlobAnalysis,
        OperatorType.SharpnessEvaluation
    ];

    private readonly OperatorFactory _factory;
    private readonly IReadOnlyDictionary<OperatorType, IOperatorExecutor> _executors;
    private readonly IReadOnlyDictionary<OperatorType, OperatorMetadata> _metadata;

    public OperatorRobustnessTests()
    {
        _factory = new OperatorFactory();
        _executors = CreateExecutors();
        _metadata = _factory
            .GetAllMetadata()
            .GroupBy(item => item.Type)
            .ToDictionary(group => group.Key, group => group.First());
    }

    [Fact]
    public async Task Operators_ShouldReturnResult_InsteadOfUnhandledException_ForRobustnessMatrix()
    {
        var failures = new List<string>();
        var auditedTypes = _executors
            .Keys
            .OrderBy(type => (int)type)
            .ToList();

        Assert.NotEmpty(auditedTypes);

        foreach (var type in auditedTypes)
        {
            var executor = _executors[type];
            var metadata = _metadata.GetValueOrDefault(type);
            var hasImageInput = metadata?.InputPorts.Any(port => port.DataType == PortDataType.Image) == true;
            var hasRequiredImageInput = metadata?.InputPorts.Any(port => port.IsRequired && port.DataType == PortDataType.Image) == true;

            await RunCaseAsync(type, "null_inputs", executor, failures, null, expectFailure: hasRequiredImageInput);
            await RunCaseAsync(type, "empty_params", executor, failures, null, parameterMutator: op => op.Parameters.Clear());
            await RunCaseAsync(type, "invalid_params", executor, failures, null, parameterMutator: ApplyInvalidParameters);

            if (!hasImageInput)
                continue;

            await RunCaseAsync(
                type,
                "empty_image",
                executor,
                failures,
                inputFactory: _ => CreateImageInputs(metadata, new Mat()),
                expectFailure: true);

            await RunCaseAsync(
                type,
                "image_1x1",
                executor,
                failures,
                inputFactory: _ => CreateImageInputs(metadata, new Mat(1, 1, MatType.CV_8UC3, Scalar.Black)));

            await RunCaseAsync(
                type,
                "all_black",
                executor,
                failures,
                inputFactory: _ => CreateImageInputs(metadata, new Mat(256, 256, MatType.CV_8UC3, Scalar.Black)));

            await RunCaseAsync(
                type,
                "all_white",
                executor,
                failures,
                inputFactory: _ => CreateImageInputs(metadata, new Mat(256, 256, MatType.CV_8UC3, Scalar.White)));

            if (LargeImageAuditTypes.Contains(type))
            {
                await RunCaseAsync(
                    type,
                    "large_image",
                    executor,
                    failures,
                    inputFactory: _ => CreateImageInputs(metadata, CreateLargeImage()));
            }
        }

        Assert.True(
            failures.Count == 0,
            "Unhandled robustness failures detected:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static async Task RunCaseAsync(
        OperatorType type,
        string caseName,
        IOperatorExecutor executor,
        List<string> failures,
        Func<Operator, Dictionary<string, object>?>? inputFactory,
        bool expectFailure = false,
        Action<Operator>? parameterMutator = null)
    {
        var op = new OperatorFactory().CreateOperator(type, $"{type}_{caseName}", 0, 0);
        parameterMutator?.Invoke(op);

        Dictionary<string, object>? inputs = null;
        OperatorExecutionOutput? result = null;

        try
        {
            inputs = inputFactory?.Invoke(op);
            result = await executor.ExecuteAsync(op, inputs);

            Assert.NotNull(result);
            if (expectFailure && result.IsSuccess)
            {
                failures.Add($"{type} / {caseName}: expected failure but got success.");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"{type} / {caseName}: threw {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            DisposeObjectGraph(result?.OutputData);
            DisposeObjectGraph(inputs);
        }
    }

    private static void ApplyInvalidParameters(Operator op)
    {
        foreach (var parameter in op.Parameters)
        {
            var dataType = parameter.DataType.ToLowerInvariant();
            if (dataType.Contains("int", StringComparison.Ordinal))
            {
                parameter.SetValue(int.MaxValue);
            }
            else if (dataType.Contains("double", StringComparison.Ordinal) || dataType.Contains("float", StringComparison.Ordinal))
            {
                parameter.SetValue(double.MaxValue);
            }
            else if (dataType.Contains("bool", StringComparison.Ordinal))
            {
                parameter.SetValue("not_a_bool");
            }
            else if (dataType.Contains("enum", StringComparison.Ordinal))
            {
                parameter.SetValue("__invalid_enum_value__");
            }
            else
            {
                parameter.SetValue(string.Empty);
            }
        }
    }

    private static Dictionary<string, object> CreateImageInputs(OperatorMetadata? metadata, Mat image)
    {
        var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var wrapper = new ImageWrapper(image);

        var imagePortNames = metadata?.InputPorts
            .Where(port => port.DataType == PortDataType.Image)
            .Select(port => port.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imagePortNames == null || imagePortNames.Count == 0)
        {
            inputs["Image"] = wrapper;
            return inputs;
        }

        foreach (var imagePort in imagePortNames)
        {
            inputs[imagePort] = wrapper;
        }

        return inputs;
    }

    private static Mat CreateLargeImage()
    {
        try
        {
            return new Mat(10000, 10000, MatType.CV_8UC1, Scalar.Black);
        }
        catch
        {
            return new Mat(4096, 4096, MatType.CV_8UC1, Scalar.Black);
        }
    }

    private static IReadOnlyDictionary<OperatorType, IOperatorExecutor> CreateExecutors()
    {
        return new Dictionary<OperatorType, IOperatorExecutor>
        {
            [OperatorType.Filtering] = new GaussianBlurOperator(NullLogger<GaussianBlurOperator>.Instance),
            [OperatorType.Thresholding] = new ThresholdOperator(NullLogger<ThresholdOperator>.Instance),
            [OperatorType.EdgeDetection] = new CannyEdgeOperator(NullLogger<CannyEdgeOperator>.Instance),
            [OperatorType.Morphology] = new MorphologyOperator(NullLogger<MorphologyOperator>.Instance),
            [OperatorType.BlobAnalysis] = new BlobDetectionOperator(NullLogger<BlobDetectionOperator>.Instance),
            [OperatorType.SharpnessEvaluation] = new SharpnessEvaluationOperator(NullLogger<SharpnessEvaluationOperator>.Instance)
        };
    }

    private static void DisposeObjectGraph(object? value)
    {
        if (value == null)
            return;

        if (value is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                DisposeObjectGraph(entry.Value);
            }

            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                DisposeObjectGraph(item);
            }
        }
    }
}
