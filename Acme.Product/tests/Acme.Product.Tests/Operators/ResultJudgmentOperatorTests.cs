using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ResultJudgmentOperatorTests
{
    private readonly ResultJudgmentOperator _operator;

    public ResultJudgmentOperatorTests()
    {
        _operator = new ResultJudgmentOperator(Substitute.For<ILogger<ResultJudgmentOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeResultJudgment()
    {
        _operator.OperatorType.Should().Be(OperatorType.ResultJudgment);
    }

    [Fact]
    public async Task ExecuteAsync_WithNumericEqualWithinTolerance_ShouldReturnOk()
    {
        var op = new Operator("test", OperatorType.ResultJudgment, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Condition", "Equal", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ExpectValue", "10.0", "string"));
        op.AddParameter(TestHelpers.CreateParameter("NumericAbsTolerance", 0.01, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NumericRelTolerance", 0.0, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Value"] = 10.005
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsOk"].Should().Be(true);
        result.OutputData["JudgmentResult"].Should().Be("OK");
    }

    [Fact]
    public async Task ExecuteAsync_WithLowConfidence_ShouldReturnConfidenceGateNg()
    {
        var op = new Operator("test", OperatorType.ResultJudgment, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinConfidence", 0.8, "double"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Value"] = 1,
            ["Confidence"] = 0.5
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsOk"].Should().Be(false);
        result.OutputData["Condition"].Should().Be("MinConfidenceGate");
        result.OutputData["Details"].Should().Be("Confidence below MinConfidence");
    }

    [Fact]
    public void ValidateParameters_WithInvalidMinConfidence_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.ResultJudgment, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinConfidence", 1.5, "double"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }
}
