using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class ModbusCommunicationOperatorTests
{
    private readonly ModbusCommunicationOperator _operator;

    public ModbusCommunicationOperatorTests()
    {
        _operator = new ModbusCommunicationOperator(Substitute.For<ILogger<ModbusCommunicationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeModbusCommunication()
    {
        _operator.OperatorType.Should().Be(OperatorType.ModbusCommunication);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.ModbusCommunication, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPort_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.ModbusCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "Port", "Port", "", "int", 70000, 0, 65535, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidSlaveId_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.ModbusCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "SlaveId", "SlaveId", "", "int", 256, 0, 255, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedRtuMode_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.ModbusCommunication, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Protocol", "RTU", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
