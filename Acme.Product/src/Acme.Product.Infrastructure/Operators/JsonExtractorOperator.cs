// JsonExtractorOperator.cs
// JSON 提取器算子 - Sprint 2 Task 2.2
// 按 JSONPath 从 JSON 字符串中提取字段值
// 作者：蘅芜君

using System.Text.Json;
using System.Text.Json.Nodes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// JSON 提取器算子 - 从 JSON 字符串中提取字段
/// 
/// 功能：
/// 1. 按 JSONPath 提取字段
/// 2. 输出为多种类型：Any、String、Float、Integer、Boolean
/// 3. 支持嵌套对象和数组访问
/// 
/// 使用场景：
/// - 解析 MES 返回的 JSON 响应
/// - 提取 HTTP API 响应中的特定字段
/// - 处理配置 JSON 数据
/// </summary>
public class JsonExtractorOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.JsonExtractor;

    public JsonExtractorOperator(ILogger<JsonExtractorOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (inputs == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("JsonExtractor 算子需要输入数据"));
        }

        // 获取 JSON 输入
        if (!inputs.TryGetValue("Json", out var jsonObj) || jsonObj == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供 Json 输入"));
        }

        var jsonString = jsonObj.ToString();
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Json 输入为空"));
        }

        // 获取参数
        var path = GetStringParam(@operator, "Path", "$");
        var outputType = GetStringParam(@operator, "OutputType", "Any");
        var defaultValue = GetStringParam(@operator, "DefaultValue", "");
        var required = GetBoolParam(@operator, "Required", false);

        try
        {
            // 解析 JSON
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

            // 按 Path 提取值
            var extractedValue = ExtractValue(rootNode, path);

            if (extractedValue == null)
            {
                if (required)
                {
                    return Task.FromResult(OperatorExecutionOutput.Failure($"未找到路径 '{path}' 的值"));
                }

                // 返回默认值
                var defaultResult = ConvertToOutputType(defaultValue, outputType);
                return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
                {
                    { "Value", defaultResult },
                    { "Found", false },
                    { "AsString", defaultValue },
                    { "Path", path }
                }));
            }

            // 转换为指定输出类型
            var result = ConvertToOutputType(extractedValue, outputType);
            var asString = extractedValue.ToString() ?? "";

            // 尝试解析为数值
            bool isNumber = float.TryParse(asString, out var asFloat);
            bool isInt = int.TryParse(asString, out var asInt);
            bool isBool = bool.TryParse(asString, out var asBool);

            Logger.LogDebug("[JsonExtractor] Path={Path}, Found=true, Type={ValueType}", path, result?.GetType().Name ?? "null");

            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "Value", result },
                { "Found", true },
                { "AsString", asString },
                { "AsFloat", asFloat },
                { "AsInteger", asInt },
                { "AsBoolean", isBool ? asBool : (result is bool b ? b : false) },
                { "IsNumber", isNumber },
                { "Path", path }
            }));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[JsonExtractor] 提取失败: {Path}", path);
            return Task.FromResult(OperatorExecutionOutput.Failure($"JSON 提取失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 按路径提取值（简化版 JSONPath）
    /// </summary>
    private JsonNode? ExtractValue(JsonNode root, string path)
    {
        if (string.IsNullOrEmpty(path) || path == "$")
        {
            return root;
        }

        // 移除开头的 $
        var normalizedPath = path.StartsWith("$") ? path[1..] : path;
        if (normalizedPath.StartsWith("."))
        {
            normalizedPath = normalizedPath[1..];
        }

        if (string.IsNullOrEmpty(normalizedPath))
        {
            return root;
        }

        // 分割路径
        var segments = normalizedPath.Split('.');
        var current = root;

        foreach (var segment in segments)
        {
            if (current == null)
            {
                return null;
            }

            // 处理数组索引，如 items[0] 或 [0]
            var bracketIndex = segment.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var propertyName = bracketIndex > 0 ? segment[..bracketIndex] : null;
                var indexStr = segment[(bracketIndex + 1)..].TrimEnd(']');

                // 先获取属性（如果有）
                if (!string.IsNullOrEmpty(propertyName) && current is JsonObject obj)
                {
                    if (!obj.TryGetPropertyValue(propertyName, out current))
                    {
                        return null;
                    }
                }

                // 然后获取数组索引
                if (int.TryParse(indexStr, out var index) && current is JsonArray arr)
                {
                    if (index < 0 || index >= arr.Count)
                    {
                        return null;
                    }
                    current = arr[index];
                }
                else
                {
                    return null;
                }
            }
            else if (current is JsonObject jsonObj)
            {
                // 对象属性访问
                if (!jsonObj.TryGetPropertyValue(segment, out current))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// 转换为指定输出类型
    /// </summary>
    private object? ConvertToOutputType(object? value, string outputType)
    {
        if (value == null)
        {
            return null;
        }

        var valueStr = value.ToString() ?? "";

        return outputType.ToLower() switch
        {
            "string" => valueStr,
            "float" or "double" => float.TryParse(valueStr, out var f) ? f : 0f,
            "integer" or "int" => int.TryParse(valueStr, out var i) ? i : 0,
            "boolean" or "bool" => bool.TryParse(valueStr, out var b) ? b : false,
            _ => value // Any - 返回原始值
        };
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var path = GetStringParam(@operator, "Path", "$");
        var outputType = GetStringParam(@operator, "OutputType", "Any");

        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Invalid("Path 不能为空");
        }

        var validTypes = new[] { "Any", "String", "Float", "Double", "Integer", "Int", "Boolean", "Bool" };
        if (!validTypes.Contains(outputType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"OutputType 必须是以下之一: {string.Join(", ", validTypes)}");
        }

        return ValidationResult.Valid();
    }
}
