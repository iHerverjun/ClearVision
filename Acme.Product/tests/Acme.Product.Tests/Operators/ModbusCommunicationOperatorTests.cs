// ModbusCommunicationOperatorTests.cs
// ModbusCommunicationOperatorTests测试
// 作者：蘅芜君

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
        var op = new Operator("测试", OperatorType.ModbusCommunication, 0, 0);
        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateParameters_WithInvalidPort_ShouldReturnInvalid()
    {
        var op = new Operator("测试", OperatorType.ModbusCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "Port", "端口", "", "int", 70000, 0, 65535, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidSlaveId_ShouldReturnInvalid()
    {
        var op = new Operator("测试", OperatorType.ModbusCommunication, 0, 0);
        op.AddParameter(new(Guid.NewGuid(), "SlaveId", "从机ID", "", "int", 256, 0, 255, true));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }
}
