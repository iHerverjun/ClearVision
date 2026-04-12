using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class Sprint2_JsonExtractorTests
{
    private readonly ILogger<JsonExtractorOperator> _loggerMock;
    private readonly JsonExtractorOperator _operator;

    public Sprint2_JsonExtractorTests()
    {
        _loggerMock = Substitute.For<ILogger<JsonExtractorOperator>>();
        _operator = new JsonExtractorOperator(_loggerMock);
    }

    [Fact]
    public async Task JsonExtractor_ExtractStringValue_ReturnsValueAndIsSuccess()
    {
        var json = """{"name": "John", "age": 30}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.name" },
            { "OutputType", "String" }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["IsSuccess"]);
        Assert.Equal("John", result.OutputData["Value"]);
        Assert.False(result.OutputData.ContainsKey("Found"));
    }

    [Fact]
    public async Task JsonExtractor_ExtractNumber_AsFloat()
    {
        var json = """{"age": 30.5, "count": 100}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.age" },
            { "OutputType", "Float" }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["IsSuccess"]);
        Assert.Equal(30.5f, result.OutputData["Value"]);
    }

    [Fact]
    public async Task JsonExtractor_ExtractNumber_AsDouble_ShouldPreserveDoubleType()
    {
        var json = """{"ratio": 30.125}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.ratio" },
            { "OutputType", "Double" }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["IsSuccess"]);
        Assert.IsType<double>(result.OutputData["Value"]);
        Assert.Equal(30.125d, (double)result.OutputData["Value"], 6);
    }

    [Fact]
    public async Task JsonExtractor_ArrayAccess_ReturnsElement()
    {
        var json = """{"items": [{"id": 1}, {"id": 2}, {"id": 3}]}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.items[1].id" },
            { "OutputType", "Integer" }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["IsSuccess"]);
        Assert.Equal(2, result.OutputData["Value"]);
    }

    [Fact]
    public async Task JsonExtractor_PathNotFound_ReturnsDefaultValue()
    {
        var json = """{"name": "John"}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.nonexistent" },
            { "OutputType", "String" },
            { "DefaultValue", "default" },
            { "Required", false }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.True(result.IsSuccess);
        Assert.False((bool)result.OutputData!["IsSuccess"]);
        Assert.Equal("default", result.OutputData["Value"]);
    }

    [Fact]
    public async Task JsonExtractor_PathNotFound_Required_ReturnsFailure()
    {
        var json = """{"name": "John"}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.nonexistent" },
            { "Required", true }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task JsonExtractor_InvalidJson_ReturnsFailure()
    {
        var json = """{"name": "John", invalid}""";
        var op = CreateOperator();

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task JsonExtractor_InvalidTargetConversion_ShouldFail()
    {
        var json = """{"name":"John"}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.name" },
            { "OutputType", "Integer" }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.False(result.IsSuccess);
        Assert.Contains("output type", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JsonExtractor_LegacyPathParameter_IsIgnored()
    {
        var json = """{"name": "John", "age": 30}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "JsonPath", "$.name" },
            { "OutputType", "String" }
        });
        op.AddParameter(new Parameter(
            Guid.NewGuid(),
            "Path",
            "LegacyPath",
            "legacy alias that should be ignored",
            "string",
            "$.age",
            isRequired: false));
        op.UpdateParameter("Path", "$.age");

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object> { { "Json", json } });

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["IsSuccess"]);
        Assert.Equal("John", result.OutputData["Value"]);
    }

    [Fact]
    public void JsonExtractor_ValidateParameters_InvalidJsonPath_ReturnsError()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "JsonPath", "" } });
        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void JsonExtractor_ValidateParameters_InvalidOutputType_ReturnsError()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "OutputType", "UnknownType" } });
        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestJsonExtractor", OperatorType.JsonExtractor, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "JsonPath", "JSONPath", "JSON 字段路径", "string", "$.data", isRequired: true));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "OutputType", "输出类型", "Any/String/Float/Integer/Boolean", "string", "Any", isRequired: true));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "DefaultValue", "默认值", "字段不存在时的默认值", "string", string.Empty, isRequired: false));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Required", "是否必需", "字段不存在时是否报错", "bool", false, isRequired: false));

        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                op.UpdateParameter(key, value);
            }
        }

        return op;
    }
}
