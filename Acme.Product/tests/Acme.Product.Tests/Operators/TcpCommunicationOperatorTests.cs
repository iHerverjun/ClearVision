using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class TcpCommunicationOperatorTests
{
    private readonly TcpCommunicationOperator _operator;

    public TcpCommunicationOperatorTests()
    {
        _operator = new TcpCommunicationOperator(Substitute.For<ILogger<TcpCommunicationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeTcpCommunication()
    {
        _operator.OperatorType.Should().Be(OperatorType.TcpCommunication);
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPort_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "Port", "Port", "", "int", 70000, 0, 65535, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithEmptyHost_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "IpAddress", "Host", "", "string", "", "", "", true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedServerMode_ShouldReturnFailure()
    {
        var op = new Operator("test", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Mode", "Server", "string"));

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}
