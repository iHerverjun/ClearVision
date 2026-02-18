// TcpCommunicationOperatorTests.cs
// TcpCommunicationOperatorTests测试
// 作者：蘅芜君

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
        var op = new Operator("测试", OperatorType.TcpCommunication, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPort_ShouldReturnInvalid()
    {
        var op = new Operator("测试", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "Port", "端口", "", "int", 70000, 0, 65535, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithEmptyHost_ShouldReturnInvalid()
    {
        var op = new Operator("测试", OperatorType.TcpCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "Host", "主机地址", "", "string", "", "", "", true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }
}
