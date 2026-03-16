using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class MqttPublishOperatorTests
{
    private readonly MqttPublishOperator _operator;

    public MqttPublishOperatorTests()
    {
        _operator = new MqttPublishOperator(Substitute.For<ILogger<MqttPublishOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeMqttPublish()
    {
        _operator.OperatorType.Should().Be(OperatorType.MqttPublish);
    }

    [Fact]
    public async Task ExecuteAsync_WithPayloadInput_ShouldFailFastInsteadOfPretendingSuccess()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Broker", "localhost" },
            { "Topic", "cv/results" },
            { "Qos", 1 },
            { "TimeoutMs", 3000 }
        });

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            { "Payload", new Dictionary<string, object> { { "status", "NG" } } }
        });

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未启用");
        result.OutputData.Should().BeNull();
    }

    [Fact]
    public void ValidateParameters_WithLegacyQoSCasing_ShouldStillBeValid()
    {
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Broker", "localhost" },
            { "Topic", "cv/results" },
            { "QoS", 2 }
        });

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("MqttPublish", OperatorType.MqttPublish, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }
}
