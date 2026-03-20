using System.Collections;
using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Application.Analysis;

public static class AnalysisPayloadSerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Dictionary<string, object>? DeserializeJsonDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions);
    }

    public static AnalysisDataDto? DeserializeAnalysisData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AnalysisDataDto>(json, JsonOptions);
    }

    public static void TrySetOutputDataJson(InspectionResult result, Dictionary<string, object>? outputData, ILogger logger)
    {
        if (outputData == null || outputData.Count == 0)
        {
            return;
        }

        var serializableData = BuildSerializableOutputData(outputData);
        if (serializableData.Count == 0)
        {
            return;
        }

        try
        {
            result.SetOutputDataJson(JsonSerializer.Serialize(serializableData));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AnalysisPayloadSerialization] 序列化 outputData 失败");
        }
    }

    public static void TrySetAnalysisDataJson(InspectionResult result, AnalysisDataDto? analysisData, ILogger logger)
    {
        if (analysisData == null || analysisData.Cards.Count == 0)
        {
            return;
        }

        try
        {
            result.SetAnalysisDataJson(JsonSerializer.Serialize(analysisData));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AnalysisPayloadSerialization] 序列化 analysisData 失败");
        }
    }

    public static Dictionary<string, object?> BuildSerializableOutputData(Dictionary<string, object> outputData)
    {
        var serializable = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in outputData)
        {
            if (IsExcludedOutput(kvp.Key, kvp.Value))
            {
                continue;
            }

            if (TryConvertOutputValue(kvp.Value, out var converted))
            {
                serializable[kvp.Key] = converted;
            }
        }

        return serializable;
    }

    private static bool IsExcludedOutput(string key, object? value)
    {
        if (key.Equals("Image", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("Defects", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value is byte[])
        {
            return true;
        }

        if (value == null)
        {
            return false;
        }

        return IsKnownImageCarrierType(value.GetType());
    }

    private static bool TryConvertOutputValue(object? value, out object? converted, int depth = 0)
    {
        const int maxDepth = 8;
        if (depth > maxDepth)
        {
            converted = value?.ToString();
            return converted != null;
        }

        if (value == null)
        {
            converted = null;
            return true;
        }

        if (IsKnownImageCarrierType(value.GetType()) || value is byte[])
        {
            converted = null;
            return false;
        }

        if (value is JsonElement jsonElement)
        {
            converted = jsonElement;
            return true;
        }

        if (IsSimpleValue(value))
        {
            converted = value;
            return true;
        }

        if (value is IDictionary<string, object> typedDict)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, nestedValue) in typedDict)
            {
                if (TryConvertOutputValue(nestedValue, out var nested, depth + 1))
                {
                    dict[key] = nested;
                }
            }

            converted = dict;
            return true;
        }

        if (value is IDictionary dictionary)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (TryConvertOutputValue(entry.Value, out var nested, depth + 1))
                {
                    dict[key] = nested;
                }
            }

            converted = dict;
            return true;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                if (TryConvertOutputValue(item, out var nested, depth + 1))
                {
                    list.Add(nested);
                }
            }

            converted = list;
            return true;
        }

        try
        {
            JsonSerializer.Serialize(value);
            converted = value;
            return true;
        }
        catch
        {
            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                converted = null;
                return false;
            }

            converted = text;
            return true;
        }
    }

    private static bool IsSimpleValue(object value)
    {
        var type = value.GetType();
        return type.IsPrimitive ||
               type.IsEnum ||
               value is string ||
               value is decimal ||
               value is DateTime ||
               value is DateTimeOffset ||
               value is Guid ||
               value is TimeSpan;
    }

    private static bool IsKnownImageCarrierType(Type type)
    {
        var fullName = type.FullName;
        return string.Equals(fullName, "OpenCvSharp.Mat", StringComparison.Ordinal) ||
               string.Equals(fullName, "Acme.Product.Infrastructure.Operators.ImageWrapper", StringComparison.Ordinal);
    }
}
