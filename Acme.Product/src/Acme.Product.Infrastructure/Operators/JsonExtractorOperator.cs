using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "JSON 提取器",
    Description = "按 JSONPath 从字符串中提取字段",
    Category = "数据处理",
    IconName = "json"
)]
[InputPort("Json", "JSON字符串", PortDataType.String, IsRequired = true)]
[OutputPort("Value", "提取值", PortDataType.Any)]
[OutputPort("IsSuccess", "是否命中路径", PortDataType.Boolean)]
[OperatorParam("JsonPath", "JSONPath", "string", DefaultValue = "$.data")]
[OperatorParam("OutputType", "输出类型", "string", DefaultValue = "Any")]
[OperatorParam("DefaultValue", "默认值", "string", DefaultValue = "")]
[OperatorParam("Required", "是否必需", "bool", DefaultValue = false)]
public class JsonExtractorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.JsonExtractor;

    public JsonExtractorOperator(ILogger<JsonExtractorOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("JsonExtractor 算子需要输入数据"));
        }

        if (!inputs.TryGetValue("Json", out var jsonObj) || jsonObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供 Json 输入"));
        }

        var jsonString = jsonObj.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Json 输入为空"));
        }

        var jsonPath = GetStringParam(@operator, "JsonPath", "$.data");
        var outputType = GetStringParam(@operator, "OutputType", "Any");
        var defaultValue = GetStringParam(@operator, "DefaultValue", string.Empty);
        var required = GetBoolParam(@operator, "Required", false);

        try
        {
            JsonNode? rootNode;
            try
            {
                rootNode = JsonNode.Parse(jsonString);
            }
            catch (JsonException ex)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"JSON 解析失败: {ex.Message}"));
            }

            if (rootNode == null)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("JSON 解析结果为空"));
            }

            var extractedValue = ExtractValue(rootNode, jsonPath);
            if (extractedValue == null)
            {
                if (required)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure($"未找到路径 '{jsonPath}' 对应值"));
                }

                if (!TryConvertToOutputType(defaultValue, outputType, out var defaultResult))
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure($"DefaultValue cannot be converted to output type '{outputType}'."));
                }

                return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Value", defaultResult ?? string.Empty },
                    { "IsSuccess", false }
                }));
            }

            if (!TryConvertToOutputType(extractedValue, outputType, out var result))
            {
                return Task.FromResult(OperatorExecutionOutput.Failure($"Extracted value cannot be converted to output type '{outputType}'."));
            }

            Logger.LogDebug(
                "[JsonExtractor] JsonPath={JsonPath}, IsSuccess=true, ValueType={ValueType}",
                jsonPath,
                result?.GetType().Name ?? "null");

            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Value", result ?? string.Empty },
                { "IsSuccess", true }
            }));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[JsonExtractor] 提取失败: {JsonPath}", jsonPath);
            return Task.FromResult(OperatorExecutionOutput.Failure($"JSON 提取失败: {ex.Message}"));
        }
    }

    private static JsonNode? ExtractValue(JsonNode root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "$")
        {
            return root;
        }

        var normalizedPath = path.StartsWith("$", StringComparison.Ordinal) ? path[1..] : path;
        if (normalizedPath.StartsWith(".", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath[1..];
        }

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return root;
        }

        var segments = normalizedPath.Split('.');
        var current = root;

        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            var bracketIndex = segment.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var propertyName = bracketIndex > 0 ? segment[..bracketIndex] : null;
                var indexStr = segment[(bracketIndex + 1)..].TrimEnd(']');

                if (!string.IsNullOrEmpty(propertyName) && current is JsonObject obj)
                {
                    if (!obj.TryGetPropertyValue(propertyName, out current))
                    {
                        return null;
                    }
                }

                if (int.TryParse(indexStr, out var index) && current is JsonArray arr)
                {
                    if (index < 0 || index >= arr.Count)
                    {
                        return null;
                    }

                    current = arr[index];
                    continue;
                }

                return null;
            }

            if (current is JsonObject jsonObj)
            {
                if (!jsonObj.TryGetPropertyValue(segment, out current))
                {
                    return null;
                }

                continue;
            }

            return null;
        }

        return current;
    }

    private static bool TryConvertToOutputType(object? value, string outputType, out object? convertedValue)
    {
        convertedValue = null;
        if (value == null)
        {
            return false;
        }

        var valueStr = value.ToString() ?? string.Empty;
        switch (outputType.ToLowerInvariant())
        {
            case "string":
                convertedValue = valueStr;
                return true;
            case "float":
                if (float.TryParse(valueStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var f) && float.IsFinite(f))
                {
                    convertedValue = f;
                    return true;
                }

                return false;
            case "double":
                if (double.TryParse(valueStr, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                {
                    convertedValue = d;
                    return true;
                }

                return false;
            case "integer":
            case "int":
                if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                {
                    convertedValue = i;
                    return true;
                }

                return false;
            case "boolean":
            case "bool":
                if (bool.TryParse(valueStr, out var b))
                {
                    convertedValue = b;
                    return true;
                }

                return false;
            default:
                convertedValue = value;
                return true;
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var jsonPath = GetStringParam(@operator, "JsonPath", "$.data");
        var outputType = GetStringParam(@operator, "OutputType", "Any");

        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return ValidationResult.Invalid("JsonPath 不能为空");
        }

        var validTypes = new[] { "Any", "String", "Float", "Double", "Integer", "Int", "Boolean", "Bool" };
        if (!validTypes.Contains(outputType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"OutputType 必须是以下之一: {string.Join(", ", validTypes)}");
        }

        return ValidationResult.Valid();
    }
}
