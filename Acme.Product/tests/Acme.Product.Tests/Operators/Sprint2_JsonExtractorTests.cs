// Sprint2_JsonExtractorTests.cs
// Sprint 2 Task 2.2 JsonExtractor 算子单元测试
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

/// <summary>
/// Sprint 2 Task 2.2: JsonExtractor 算子单元测试
/// </summary>
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
    public async Task JsonExtractor_ExtractStringValue_ReturnsString()
    {
        var json = """{"name": "John", "age": 30}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.name" },
            { "OutputType", "String" }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal("John", result.OutputData["AsString"]);
        Assert.Equal("John", result.OutputData["Value"]);
    }

    [Fact]
    public async Task JsonExtractor_ExtractNumber_AsFloat()
    {
        var json = """{"age": 30.5, "count": 100}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.age" },
            { "OutputType", "Float" }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.True((bool)result.OutputData["IsNumber"]);
        Assert.Equal(30.5f, result.OutputData["AsFloat"]);
    }

    [Fact]
    public async Task JsonExtractor_ExtractNumber_AsInteger()
    {
        var json = """{"age": 30, "count": 100}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.count" },
            { "OutputType", "Integer" }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(100, result.OutputData["AsInteger"]);
    }

    [Fact]
    public async Task JsonExtractor_ExtractBoolean_ReturnsBool()
    {
        var json = """{"isActive": true, "verified": false}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.isActive" },
            { "OutputType", "Boolean" }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.True((bool)result.OutputData["AsBoolean"]);
    }

    [Fact]
    public async Task JsonExtractor_NestedObject_ReturnsValue()
    {
        var json = """{"user": {"name": "John", "email": "john@example.com"}}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.user.email" },
            { "OutputType", "String" }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal("john@example.com", result.OutputData["Value"]);
    }

    [Fact]
    public async Task JsonExtractor_ArrayAccess_ReturnsElement()
    {
        var json = """{"items": [{"id": 1}, {"id": 2}, {"id": 3}]}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.items[1].id" },
            { "OutputType", "Integer" }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.True((bool)result.OutputData!["Found"]);
        Assert.Equal(2, result.OutputData["AsInteger"]);
    }

    [Fact]
    public async Task JsonExtractor_PathNotFound_ReturnsDefault()
    {
        var json = """{"name": "John"}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.nonexistent" },
            { "OutputType", "String" },
            { "DefaultValue", "default" },
            { "Required", false }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.True(result.IsSuccess);
        Assert.False((bool)result.OutputData!["Found"]);
        Assert.Equal("default", result.OutputData["Value"]);
    }

    [Fact]
    public async Task JsonExtractor_PathNotFound_Required_ReturnsFailure()
    {
        var json = """{"name": "John"}""";
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Path", "$.nonexistent" },
            { "Required", true }
        });

        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task JsonExtractor_InvalidJson_ReturnsFailure()
    {
        var json = """{"name": "John", invalid}""";
        var op = CreateOperator();
        var inputs = new Dictionary<string, object> { { "Json", json } };
        var result = await _operator.ExecuteAsync(op, inputs);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void JsonExtractor_ValidateParameters_InvalidPath_ReturnsError()
    {
        var op = CreateOperator(new Dictionary<string, object> { { "Path", "" } });
        var result = _operator.ValidateParameters(op);

        Assert.False(result.IsValid);
    }

    private Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator(Guid.NewGuid(), "TestJsonExtractor", OperatorType.JsonExtractor, 0, 0);

        op.AddParameter(new Parameter(
            Guid.NewGuid(), "Path", "JSONPath", "JSON 字段路径", "string", "$", isRequired: true));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "OutputType", "输出类型", "Any/String/Float/Integer/Boolean", "string", "Any", isRequired: true));
        op.AddParameter(new Parameter(
            Guid.NewGuid(), "DefaultValue", "默认值", "字段不存在时的默认值", "string", "", isRequired: false));
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
