using System.Reflection;
using System.Text.Json;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using FluentAssertions;

namespace Acme.Product.Tests.Services;

public class AnalysisDataBuilderTests
{
    [Fact]
    public async Task Build_Should_Create_Cards_For_Whitelisted_Operators_Only()
    {
        var flow = new OperatorFlow("analysis-data");
        var ocr = new Operator("OCR", OperatorType.OcrRecognition, 0, 0);
        var code = new Operator("Code", OperatorType.CodeRecognition, 100, 0);
        var width = new Operator("Width", OperatorType.WidthMeasurement, 200, 0);
        var threshold = new Operator("Threshold", OperatorType.Thresholding, 300, 0);
        var textSave = new Operator("TextSave", OperatorType.TextSave, 400, 0);
        var resultOutput = new Operator("ResultOutput", OperatorType.ResultOutput, 500, 0);

        flow.AddOperator(ocr);
        flow.AddOperator(code);
        flow.AddOperator(width);
        flow.AddOperator(threshold);
        flow.AddOperator(textSave);
        flow.AddOperator(resultOutput);

        var operatorResults = new List<OperatorExecutionResult>
        {
            new()
            {
                OperatorId = ocr.Id,
                OperatorName = ocr.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Text"] = "ABC123",
                    ["Confidence"] = 98.2
                }
            },
            new()
            {
                OperatorId = code.Id,
                OperatorName = code.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Text"] = "QR-42",
                    ["CodeType"] = "QR",
                    ["Confidence"] = 0.96
                }
            },
            new()
            {
                OperatorId = width.Id,
                OperatorName = width.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Width"] = 12.5,
                    ["MinWidth"] = 12.1,
                    ["MaxWidth"] = 12.8
                }
            },
            new()
            {
                OperatorId = threshold.Id,
                OperatorName = threshold.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Width"] = 999,
                    ["Text"] = "should-not-render"
                }
            },
            new()
            {
                OperatorId = textSave.Id,
                OperatorName = textSave.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Text"] = "saved-text",
                    ["FilePath"] = "C:/tmp/out.txt"
                }
            },
            new()
            {
                OperatorId = resultOutput.Id,
                OperatorName = resultOutput.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Text"] = "export-payload"
                }
            }
        };

        var analysisData = await BuildAnalysisDataAsync(flow, operatorResults, InspectionStatus.OK);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(analysisData));

        ReadInt(json.RootElement, "version").Should().BeGreaterThan(0);

        var cards = ReadArray(json.RootElement, "cards");
        cards.GetArrayLength().Should().Be(3, "只有白名单算子应该产出 analysisData 卡片");

        var categoriesByType = cards.EnumerateArray().ToDictionary(
            card => ReadString(card, "sourceOperatorType"),
            card => ReadString(card, "category"),
            StringComparer.OrdinalIgnoreCase);

        categoriesByType.Should().Contain(new KeyValuePair<string, string>("OcrRecognition", "recognition"));
        categoriesByType.Should().Contain(new KeyValuePair<string, string>("CodeRecognition", "recognition"));
        categoriesByType.Should().Contain(new KeyValuePair<string, string>("WidthMeasurement", "measurement"));
        categoriesByType.Keys.Should().NotContain(new[]
        {
            "Thresholding",
            "TextSave",
            "ResultOutput"
        });

        var widthCard = cards.EnumerateArray()
            .Single(card => ReadString(card, "sourceOperatorType").Equals("WidthMeasurement", StringComparison.OrdinalIgnoreCase));
        ReadString(widthCard, "title").Should().NotBeNullOrWhiteSpace();
        ReadString(widthCard, "status").Should().Be("OK");
        ReadArray(widthCard, "fields").EnumerateArray()
            .Select(field => ReadString(field, "key"))
            .Should()
            .Contain(new[] { "width", "minWidth", "maxWidth" });
    }

    [Fact]
    public async Task Build_Should_Ignore_NonWhitelisted_Operators_Even_When_Output_Looks_Like_Analysis()
    {
        var flow = new OperatorFlow("non-whitelisted");
        var threshold = new Operator("Threshold", OperatorType.Thresholding, 0, 0);
        var textSave = new Operator("TextSave", OperatorType.TextSave, 100, 0);
        var resultOutput = new Operator("ResultOutput", OperatorType.ResultOutput, 200, 0);

        flow.AddOperator(threshold);
        flow.AddOperator(textSave);
        flow.AddOperator(resultOutput);

        var operatorResults = new List<OperatorExecutionResult>
        {
            new()
            {
                OperatorId = threshold.Id,
                OperatorName = threshold.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Width"] = 15.2,
                    ["Text"] = "fake-measurement"
                }
            },
            new()
            {
                OperatorId = textSave.Id,
                OperatorName = textSave.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["Text"] = "fake-ocr"
                }
            },
            new()
            {
                OperatorId = resultOutput.Id,
                OperatorName = resultOutput.Name,
                IsSuccess = true,
                OutputData = new Dictionary<string, object>
                {
                    ["RecognizedText"] = "fake-result"
                }
            }
        };

        var analysisData = await BuildAnalysisDataAsync(flow, operatorResults, InspectionStatus.OK);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(analysisData));
        var cards = ReadArray(json.RootElement, "cards");

        cards.GetArrayLength().Should().Be(0, "非白名单算子即使输出了 Width/Text 等字段，也不应生成显式分析卡片");
    }

    private static async Task<object> BuildAnalysisDataAsync(
        OperatorFlow flow,
        IReadOnlyCollection<OperatorExecutionResult> operatorResults,
        InspectionStatus status)
    {
        var applicationAssembly = typeof(InspectionService).Assembly;
        var builderType = RequireType(
            applicationAssembly,
            "Acme.Product.Application.Analysis.AnalysisDataBuilder");

        var builder = CreateInstance(builderType, applicationAssembly);
        var method = builderType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate => candidate.Name.Contains("Build", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("未找到 AnalysisDataBuilder 的 Build 方法。");

        var flowExecutionResult = new FlowExecutionResult
        {
            IsSuccess = true,
            OperatorResults = operatorResults.ToList()
        };

        var arguments = method.GetParameters()
            .Select(parameter => ResolveMethodArgument(parameter.ParameterType, flow, operatorResults, flowExecutionResult, status))
            .ToArray();

        var result = method.Invoke(builder, arguments)
            ?? throw new InvalidOperationException("AnalysisDataBuilder 返回了 null。");

        if (result is Task task)
        {
            await task;
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task)
                ?? throw new InvalidOperationException("AnalysisDataBuilder 的异步方法未返回结果。");
        }

        return result;
    }

    private static object CreateInstance(Type type, Assembly applicationAssembly)
    {
        foreach (var constructor in type.GetConstructors().OrderBy(ctor => ctor.GetParameters().Length))
        {
            try
            {
                var arguments = constructor.GetParameters()
                    .Select(parameter => ResolveConstructorArgument(parameter.ParameterType, applicationAssembly))
                    .ToArray();
                return constructor.Invoke(arguments);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        throw new InvalidOperationException($"无法创建类型 {type.FullName} 的实例。");
    }

    private static object ResolveConstructorArgument(Type parameterType, Assembly applicationAssembly)
    {
        if (parameterType == typeof(IServiceProvider))
        {
            return new SimpleServiceProvider();
        }

        if (parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.Logging.ILogger<>))
        {
            var genericArgument = parameterType.GetGenericArguments()[0];
            var nullLoggerType = typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(genericArgument);
            return nullLoggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? throw new InvalidOperationException($"无法获取 {nullLoggerType.FullName}.Instance");
        }

        if (parameterType.IsInterface && parameterType.Name.Contains("AnalysisCardRegistry", StringComparison.OrdinalIgnoreCase))
        {
            var registryType = RequireType(
                applicationAssembly,
                "Acme.Product.Application.Analysis.AnalysisCardRegistry");
            return CreateInstance(registryType, applicationAssembly);
        }

        if (parameterType.IsClass && parameterType.Namespace?.Contains(".Analysis", StringComparison.OrdinalIgnoreCase) == true)
        {
            return CreateInstance(parameterType, applicationAssembly);
        }

        if (parameterType.IsArray && parameterType.GetElementType()?.Namespace?.Contains(".Analysis", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Array.CreateInstance(parameterType.GetElementType()!, 0);
        }

        if (parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var itemType = parameterType.GetGenericArguments()[0];
            if (itemType.Namespace?.Contains(".Analysis", StringComparison.OrdinalIgnoreCase) == true)
            {
                var mapperTypes = applicationAssembly.GetTypes()
                    .Where(candidate =>
                        !candidate.IsAbstract &&
                        itemType.IsAssignableFrom(candidate) &&
                        candidate.Name.Contains("Mapper", StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => CreateInstance(candidate, applicationAssembly))
                    .ToArray();

                var typedArray = Array.CreateInstance(itemType, mapperTypes.Length);
                for (var index = 0; index < mapperTypes.Length; index++)
                {
                    typedArray.SetValue(mapperTypes[index], index);
                }

                return typedArray;
            }
        }

        throw new InvalidOperationException($"暂不支持构造函数参数类型: {parameterType.FullName}");
    }

    private static object ResolveMethodArgument(
        Type parameterType,
        OperatorFlow flow,
        IReadOnlyCollection<OperatorExecutionResult> operatorResults,
        FlowExecutionResult flowExecutionResult,
        InspectionStatus status)
    {
        if (parameterType == typeof(OperatorFlow))
        {
            return flow;
        }

        if (parameterType == typeof(InspectionStatus))
        {
            return status;
        }

        if (parameterType == typeof(FlowExecutionResult))
        {
            return flowExecutionResult;
        }

        if (typeof(IEnumerable<OperatorExecutionResult>).IsAssignableFrom(parameterType))
        {
            return operatorResults.ToList();
        }

        if (parameterType == typeof(List<OperatorExecutionResult>) || parameterType == typeof(IReadOnlyCollection<OperatorExecutionResult>))
        {
            return operatorResults.ToList();
        }

        throw new InvalidOperationException($"无法为 Build 方法参数 {parameterType.FullName} 提供值。");
    }

    private static Type RequireType(Assembly assembly, params string[] candidateNames)
    {
        foreach (var candidateName in candidateNames)
        {
            var type = assembly.GetType(candidateName, throwOnError: false, ignoreCase: false);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"未找到类型: {string.Join(", ", candidateNames)}");
    }

    private static JsonElement ReadArray(JsonElement element, string propertyName)
    {
        var property = GetProperty(element, propertyName);
        property.ValueKind.Should().Be(JsonValueKind.Array, $"{propertyName} 应该是数组");
        return property;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return GetProperty(element, propertyName).GetInt32();
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return GetProperty(element, propertyName).GetString() ?? string.Empty;
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException($"JSON 中缺少属性 {propertyName}。");
    }

    private sealed class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
