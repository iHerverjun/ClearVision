// OperatorPreviewService.cs
// 算子预览服务
// 提供算子预览执行与结果格式化输出能力
// 作者：蘅芜君
using System.Collections;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// Executes a single operator against one input image for quick parameter tuning.
/// </summary>
public sealed class OperatorPreviewService
{
    private readonly IOperatorFactory _operatorFactory;
    private readonly IFlowExecutionService _flowExecutionService;
    private readonly ILogger<OperatorPreviewService> _logger;

    public OperatorPreviewService(
        IOperatorFactory operatorFactory,
        IFlowExecutionService flowExecutionService,
        ILogger<OperatorPreviewService> logger)
    {
        _operatorFactory = operatorFactory;
        _flowExecutionService = flowExecutionService;
        _logger = logger;
    }

    public async Task<OperatorPreviewResult> PreviewAsync(
        OperatorType type,
        Dictionary<string, object>? parameters,
        Mat image,
        CancellationToken cancellationToken = default)
    {
        if (image == null || image.Empty())
        {
            return OperatorPreviewResult.Failure("Input image is required.");
        }

        var previewOperator = _operatorFactory.CreateOperator(type, $"{type}_Preview", 0, 0);
        ApplyParameters(previewOperator, parameters);

        var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Image"] = new ImageWrapper(image.Clone())
        };

        OperatorExecutionResult? execution = null;

        try
        {
            execution = await _flowExecutionService.ExecuteOperatorAsync(previewOperator, inputs);

            var result = new OperatorPreviewResult
            {
                IsSuccess = execution.IsSuccess,
                ExecutionTimeMs = execution.ExecutionTimeMs,
                ErrorMessage = execution.ErrorMessage
            };

            if (execution.OutputData != null)
            {
                result.ImageBase64 = TryExtractImageBase64(execution.OutputData);
                result.Outputs = BuildSerializableOutputs(execution.OutputData);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Operator preview canceled. Type={OperatorType}",
                type);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Operator preview failed. Type={OperatorType}",
                type);
            return OperatorPreviewResult.Failure(ex.Message);
        }
        finally
        {
            if (execution?.OutputData != null)
            {
                DisposeImageCarriers(execution.OutputData);
            }
        }
    }

    private static void ApplyParameters(Operator previewOperator, Dictionary<string, object>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return;

        foreach (var (name, rawValue) in parameters)
        {
            var normalizedValue = NormalizeInputValue(rawValue);
            var existing = previewOperator.Parameters
                .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.SetValue(normalizedValue);
                continue;
            }

            previewOperator.AddParameter(new Core.ValueObjects.Parameter(
                Guid.NewGuid(),
                name,
                name,
                string.Empty,
                "string",
                normalizedValue));
        }
    }

    private static object? NormalizeInputValue(object? rawValue)
    {
        if (rawValue is not JsonElement jsonElement)
            return rawValue;

        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number when jsonElement.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when jsonElement.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => jsonElement
                .EnumerateArray()
                .Select(item => NormalizeInputValue(item))
                .ToList(),
            JsonValueKind.Object => jsonElement
                .EnumerateObject()
                .ToDictionary(
                    item => item.Name,
                    item => NormalizeInputValue(item.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => null
        };
    }

    private static string? TryExtractImageBase64(Dictionary<string, object> outputData)
    {
        var preferredKeys = new[] { "Image", "OutputImage", "PreviewImage" };

        foreach (var key in preferredKeys)
        {
            if (outputData.TryGetValue(key, out var value) && TryConvertToBase64(value, out var base64))
            {
                return base64;
            }
        }

        foreach (var value in outputData.Values)
        {
            if (TryConvertToBase64(value, out var base64))
            {
                return base64;
            }
        }

        return null;
    }

    private static bool TryConvertToBase64(object? value, out string base64)
    {
        base64 = string.Empty;

        if (value is ImageWrapper imageWrapper)
        {
            base64 = Convert.ToBase64String(imageWrapper.GetBytes());
            return true;
        }

        if (value is byte[] imageBytes)
        {
            base64 = Convert.ToBase64String(imageBytes);
            return true;
        }

        if (value is Mat mat && !mat.Empty())
        {
            base64 = Convert.ToBase64String(mat.ToBytes(".png"));
            return true;
        }

        return false;
    }

    private static Dictionary<string, object?> BuildSerializableOutputs(Dictionary<string, object> outputData)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in outputData)
        {
            if (IsImageCarrier(value))
                continue;

            if (TryNormalizeOutputValue(value, out var normalized))
            {
                result[key] = normalized;
            }
        }

        return result;
    }

    private static bool IsImageCarrier(object? value)
    {
        return value is ImageWrapper || value is Mat || value is byte[];
    }

    private static bool TryNormalizeOutputValue(object? value, out object? normalized, int depth = 0)
    {
        const int maxDepth = 8;
        if (depth > maxDepth)
        {
            normalized = value?.ToString();
            return normalized != null;
        }

        if (value == null)
        {
            normalized = null;
            return true;
        }

        if (IsImageCarrier(value))
        {
            normalized = null;
            return false;
        }

        if (value is JsonElement element)
        {
            normalized = NormalizeInputValue(element);
            return true;
        }

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string || value is decimal ||
            value is DateTime || value is DateTimeOffset || value is Guid || value is TimeSpan)
        {
            normalized = value;
            return true;
        }

        if (value is IDictionary<string, object> typedDict)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in typedDict)
            {
                if (TryNormalizeOutputValue(v, out var nested, depth + 1))
                {
                    dict[k] = nested;
                }
            }

            normalized = dict;
            return true;
        }

        if (value is IDictionary dictionary)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (TryNormalizeOutputValue(entry.Value, out var nested, depth + 1))
                {
                    dict[key] = nested;
                }
            }

            normalized = dict;
            return true;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                if (TryNormalizeOutputValue(item, out var nested, depth + 1))
                {
                    list.Add(nested);
                }
            }

            normalized = list;
            return true;
        }

        try
        {
            JsonSerializer.Serialize(value);
            normalized = value;
            return true;
        }
        catch
        {
            normalized = value.ToString();
            return normalized != null;
        }
    }

    private static void DisposeImageCarriers(Dictionary<string, object> outputData)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var value in outputData.Values)
        {
            DisposeImageCarriers(value, visited);
        }
    }

    private static void DisposeImageCarriers(object? value, HashSet<object> visited)
    {
        if (value == null)
            return;

        if (!visited.Add(value))
            return;

        if (value is ImageWrapper wrapper)
        {
            wrapper.Dispose();
            return;
        }

        if (value is Mat mat)
        {
            mat.Dispose();
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                DisposeImageCarriers(entry.Value, visited);
            }

            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                DisposeImageCarriers(item, visited);
            }
        }
    }
}

public sealed class OperatorPreviewResult
{
    public bool IsSuccess { get; set; }

    public string? ImageBase64 { get; set; }

    public Dictionary<string, object?> Outputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public long ExecutionTimeMs { get; set; }

    public string? ErrorMessage { get; set; }

    public static OperatorPreviewResult Failure(string message)
    {
        return new OperatorPreviewResult
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }
}
