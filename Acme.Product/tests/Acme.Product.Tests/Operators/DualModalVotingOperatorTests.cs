using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Operators;

public class DualModalVotingOperatorTests
{
    private readonly ILogger<DualModalVotingOperator> _loggerMock;
    private readonly DualModalVotingOperator _operator;
    private readonly Operator _operatorEntity;

    public DualModalVotingOperatorTests()
    {
        _loggerMock = Substitute.For<ILogger<DualModalVotingOperator>>();
        _operator = new DualModalVotingOperator(_loggerMock);
        _operatorEntity = new Operator("DualModalVoting", OperatorType.DualModalVoting, 0, 0);
    }

    [Fact]
    public async Task Execute_WithDetectionResultObjects_ShouldVoteUsingOkProbability()
    {
        var dlResult = DetectionResult.Success(true, 0.9);
        var traditionalResult = DetectionResult.Success(false, 0.4);

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlResult },
            { "TraditionalResult", traditionalResult }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsOk"].Should().Be(true);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.78, 0.001);
        result.OutputData["JudgmentValue"].Should().Be("1");
    }

    [Fact]
    public async Task Execute_WithDictionaryInputs_ShouldExtractAndVote()
    {
        var dlDict = new Dictionary<string, object>
        {
            { "IsOk", true },
            { "Confidence", 0.8 }
        };

        var traditionalDict = new Dictionary<string, object>
        {
            { "IsOk", true },
            { "Confidence", 0.7 }
        };

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlDict },
            { "TraditionalResult", traditionalDict }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsOk"].Should().Be(true);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.76, 0.001);
    }

    [Fact]
    public async Task Execute_WithDeepLearningDefectCountFormat_ShouldInferLabelAndProbability()
    {
        var dlDict = new Dictionary<string, object>
        {
            { "DefectCount", 0 },
            { "Defects", new List<object>() }
        };

        var traditionalDict = new Dictionary<string, object>
        {
            {
                "DefectCount",
                1
            },
            {
                "Defects",
                new List<object>
                {
                    new Dictionary<string, object> { { "Confidence", 0.95 } }
                }
            }
        };

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlDict },
            { "TraditionalResult", traditionalDict }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsOk"].Should().Be(true);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.62, 0.001);
    }

    [Fact]
    public async Task Execute_WithWeightedAverage_ShouldNotAverageHighConfidenceNgIntoOk()
    {
        var dlResult = DetectionResult.Success(true, 0.51);
        var traditionalResult = DetectionResult.Success(false, 0.95);

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlResult },
            { "TraditionalResult", traditionalResult }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsOk"].Should().Be(false);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.674, 0.001);
        result.OutputData["JudgmentValue"].Should().Be("0");
    }

    [Fact]
    public async Task Execute_WithWeightedAverage_ShouldReturnConfidenceForFinalNgDecision()
    {
        var dlResult = DetectionResult.Success(true, 0.1);
        var traditionalResult = DetectionResult.Success(false, 0.9);

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlResult },
            { "TraditionalResult", traditionalResult }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsOk"].Should().Be(false);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.9, 0.001);
    }

    [Fact]
    public async Task Execute_WithUnanimousStrategy_BothMustBeOk()
    {
        _operatorEntity.AddParameter(TestHelpers.CreateParameter(
            "VotingStrategy",
            "Unanimous",
            "string"));

        var dlResult = DetectionResult.Success(true, 0.9);
        var traditionalResult = DetectionResult.Success(false, 0.4);

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlResult },
            { "TraditionalResult", traditionalResult }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().NotBeNull();
        result.OutputData!["IsOk"].Should().Be(false);
        result.OutputData["JudgmentValue"].Should().Be("0");
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public async Task Execute_WithWeightedAverageAndZeroWeights_ShouldFail()
    {
        _operatorEntity.AddParameter(TestHelpers.CreateParameter("VotingStrategy", "WeightedAverage", "string"));
        _operatorEntity.AddParameter(TestHelpers.CreateParameter("DLWeight", 0.0, "double"));
        _operatorEntity.AddParameter(TestHelpers.CreateParameter("TraditionalWeight", 0.0, "double"));

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", DetectionResult.Success(true, 0.9) },
            { "TraditionalResult", DetectionResult.Success(false, 0.1) }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DLWeight + TraditionalWeight > 0");
    }
}
