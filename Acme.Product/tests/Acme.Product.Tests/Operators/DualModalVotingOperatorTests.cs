using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
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
    public async Task Execute_WithDetectionResultObjects_ShouldVoteCorrectly()
    {
        // Arrange
        var dlResult = Acme.Product.Core.Services.DetectionResult.Success(true, 0.9);
        var traditionalResult = Acme.Product.Core.Services.DetectionResult.Success(false, 0.4);

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlResult },
            { "TraditionalResult", traditionalResult }
        };

        // Strategy: WeightedAverage (Default)
        // DL(0.9)*0.6 + Trad(0.4)*0.4 = 0.54 + 0.16 = 0.70
        // Threshold: 0.5 -> OK

        // Act
        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputData["IsOk"].Should().Be(true);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.70, 0.001);
        result.OutputData["JudgmentValue"].Should().Be("1");
    }

    [Fact]
    public async Task Execute_WithDictionaryInputs_ShouldExtractAndVote()
    {
        // Arrange
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

        // Act
        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputData["IsOk"].Should().Be(true);
        // 0.8*0.6 + 0.7*0.4 = 0.48 + 0.28 = 0.76
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.76, 0.001);
    }

    [Fact]
    public async Task Execute_WithDeepLearningDefectCountFormat_ShouldInferIsOk()
    {
        // Arrange
        // DL Output: DefectCount=0 (OK), Defects=[]
        var dlDict = new Dictionary<string, object>
        {
            { "DefectCount", 0 },
            { "Defects", new List<object>() }
        };

        // Traditional Output: DefectCount=1 (NG), DefectsWithConf
        var tradDefects = new List<object>
        {
            new Dictionary<string, object> { { "Confidence", 0.95 } }
        };
        var traditionalDict = new Dictionary<string, object>
        {
            { "DefectCount", 1 },
            { "Defects", tradDefects }
        };

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlDict },
            { "TraditionalResult", traditionalDict }
        };

        // DL inferred: IsOk=true, Confidence=1.0
        // Trad inferred: IsOk=false, Confidence=0.95

        // Strategy: WeightedAverage
        // 1.0*0.6 + 0.95*0.4 = 0.6 + 0.38 = 0.98 -> OK

        // Act
        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputData["IsOk"].Should().Be(true);
        ((double)result.OutputData["Confidence"]).Should().BeApproximately(0.98, 0.001);
    }

    [Fact]
    public async Task Execute_WithUnanimousStrategy_BothMustBeOk()
    {
        // Arrange
        _operatorEntity.AddParameter(TestHelpers.CreateParameter(
            "VotingStrategy",
            "Unanimous",
            "string"));

        var dlResult = Acme.Product.Core.Services.DetectionResult.Success(true, 0.9);
        var traditionalResult = Acme.Product.Core.Services.DetectionResult.Success(false, 0.4);

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", dlResult },
            { "TraditionalResult", traditionalResult }
        };

        // Act
        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.OutputData["IsOk"].Should().Be(false); // One is NG
        result.OutputData["JudgmentValue"].Should().Be("0");
    }

    [Fact]
    public async Task Execute_WithWeightedAverageAndZeroWeights_ShouldFail()
    {
        _operatorEntity.AddParameter(TestHelpers.CreateParameter("VotingStrategy", "WeightedAverage", "string"));
        _operatorEntity.AddParameter(TestHelpers.CreateParameter("DLWeight", 0.0, "double"));
        _operatorEntity.AddParameter(TestHelpers.CreateParameter("TraditionalWeight", 0.0, "double"));

        var inputs = new Dictionary<string, object>
        {
            { "DLResult", Acme.Product.Core.Services.DetectionResult.Success(true, 0.9) },
            { "TraditionalResult", Acme.Product.Core.Services.DetectionResult.Success(false, 0.1) }
        };

        var result = await _operator.ExecuteAsync(_operatorEntity, inputs, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("DLWeight + TraditionalWeight > 0");
    }
}
