using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ComparatorOperatorTests
{
    private readonly ComparatorOperator _operator = new(Substitute.For<ILogger<ComparatorOperator>>());

    [Fact]
    public async Task ExecuteAsync_WithCompareValueFallback_ShouldCompareAgainstConfiguredValue()
    {
        var op = new Operator("cmp", OperatorType.Comparator, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Condition", "GreaterThan", "string"));
        op.AddParameter(TestHelpers.CreateParameter("CompareValue", 10.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["ValueA"] = 12.0
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Result"].Should().Be(true);
        result.OutputData["Difference"].Should().Be(2.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutValueA_ShouldFailClosed()
    {
        var op = new Operator("cmp", OperatorType.Comparator, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Condition", "GreaterThan", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ValueA");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullValueB_ShouldFallbackToCompareValue()
    {
        var op = new Operator("cmp", OperatorType.Comparator, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Condition", "GreaterThan", "string"));
        op.AddParameter(TestHelpers.CreateParameter("CompareValue", 10.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["ValueA"] = 12.0,
            ["ValueB"] = null!
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Result"].Should().Be(true);
        result.OutputData["Difference"].Should().Be(2.0);
    }
}

public class DelayOperatorTests
{
    private readonly DelayOperator _operator = new(Substitute.For<ILogger<DelayOperator>>());

    [Fact]
    public async Task ExecuteAsync_ShouldDelayAndPassThroughInput()
    {
        var op = new Operator("delay", OperatorType.Delay, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Milliseconds", 25, "int"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Input"] = "payload"
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Output"].Should().Be("payload");
        Convert.ToInt32(result.OutputData["ElapsedMs"]).Should().BeGreaterThanOrEqualTo(15);
    }
}

public class VariableReadOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReadTypedValueFromContext()
    {
        var context = new VariableContext();
        context.SetValue("temperature", 42.5);
        var sut = new VariableReadOperator(Substitute.For<ILogger<VariableReadOperator>>(), context);
        var op = new Operator("read", OperatorType.VariableRead, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("VariableName", "temperature", "string"));
        op.AddParameter(TestHelpers.CreateParameter("DataType", "Double", "string"));

        var result = await sut.ExecuteAsync(op);

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Exists"].Should().Be(true);
        result.OutputData["Value"].Should().Be(42.5);
    }
}

public class VariableWriteOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldWriteInputValueIntoContext()
    {
        var context = new VariableContext();
        var sut = new VariableWriteOperator(Substitute.For<ILogger<VariableWriteOperator>>(), context);
        var op = new Operator("write", OperatorType.VariableWrite, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("VariableName", "batchId", "string"));
        op.AddParameter(TestHelpers.CreateParameter("DataType", "String", "string"));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Value"] = "LAB-001"
        });

        result.IsSuccess.Should().BeTrue();
        context.GetValue<string>("batchId").Should().Be("LAB-001");
    }
}

public class VariableIncrementOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldIncrementAndExposeResetState()
    {
        var context = new VariableContext();
        context.SetValue("counter", 5L);
        var sut = new VariableIncrementOperator(Substitute.For<ILogger<VariableIncrementOperator>>(), context);
        var op = new Operator("inc", OperatorType.VariableIncrement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("VariableName", "counter", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Delta", 2, "int"));

        var result = await sut.ExecuteAsync(op);

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["PreviousValue"].Should().Be(5L);
        result.OutputData["NewValue"].Should().Be(7L);
        result.OutputData["WasReset"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResetConditionMatches_ShouldWriteResetValueBackToContext()
    {
        var context = new VariableContext();
        context.SetValue("counter", 10L);
        var sut = new VariableIncrementOperator(Substitute.For<ILogger<VariableIncrementOperator>>(), context);
        var op = new Operator("inc_reset", OperatorType.VariableIncrement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("VariableName", "counter", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Delta", 2, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ResetCondition", "GreaterThan", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ResetThreshold", 5, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ResetValue", 1, "int"));

        var result = await sut.ExecuteAsync(op);

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["WasReset"].Should().Be(true);
        result.OutputData["NewValue"].Should().Be(3L);
        context.GetValue<long>("counter").Should().Be(3L);
    }
}

public class CycleCounterOperatorTests
{
    [Fact]
    public async Task ExecuteAsync_IncrementAction_ShouldAdvanceCycleCount()
    {
        var context = new VariableContext();
        var sut = new CycleCounterOperator(Substitute.For<ILogger<CycleCounterOperator>>(), context);
        var op = new Operator("cycle", OperatorType.CycleCounter, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Action", "Increment", "string"));
        op.AddParameter(TestHelpers.CreateParameter("MaxCycles", 3, "int"));

        var result = await sut.ExecuteAsync(op);

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["CycleCount"].Should().Be(1L);
        result.OutputData["IsLimitReached"].Should().Be(false);
    }
}

public class StringFormatOperatorTests
{
    private readonly StringFormatOperator _operator = new(Substitute.For<ILogger<StringFormatOperator>>());

    [Fact]
    public async Task ExecuteAsync_TemplateMode_ShouldReplaceIndexedAndNamedPlaceholders()
    {
        var op = new Operator("format", OperatorType.StringFormat, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Template", "Result={0}; Name={Name}", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Arg1"] = "OK",
            ["Name"] = "StationA"
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Result"].Should().Be("Result=OK; Name=StationA");
    }
}

public class CommentOperatorTests
{
    private readonly CommentOperator _operator = new(Substitute.For<ILogger<CommentOperator>>());

    [Fact]
    public async Task ExecuteAsync_ShouldPassThroughInputAndExposeMessage()
    {
        var op = new Operator("comment", OperatorType.Comment, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Text", "lab checkpoint", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Input"] = "payload"
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["Output"].Should().Be("payload");
        result.OutputData["Message"].Should().Be("lab checkpoint");
    }
}
