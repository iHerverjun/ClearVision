using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Acme.Product.Tests.Operators;

public class SerialCommunicationOperatorTests
{
    private readonly SerialCommunicationOperator _operator;

    public SerialCommunicationOperatorTests()
    {
        _operator = new SerialCommunicationOperator(Substitute.For<ILogger<SerialCommunicationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeSerialCommunication()
    {
        _operator.OperatorType.Should().Be(OperatorType.SerialCommunication);
    }

    [Fact]
    public void ValidateParameters_WithInvalidBaudRate_ShouldReturnInvalid()
    {
        var op = new Operator("test", OperatorType.SerialCommunication, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("PortName", "COM1", "string"));
        op.AddParameter(TestHelpers.CreateParameter("BaudRate", "invalid", "string"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithDataBitsOutOfRange_ShouldFailBeforeOpeningPort()
    {
        var op = new Operator("test", OperatorType.SerialCommunication, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("PortName", "COM_DO_NOT_OPEN", "string"));
        op.AddParameter(TestHelpers.CreateParameter("DataBits", 4, "int"));

        var result = await _operator.ExecuteAsync(op);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("数据位必须在 5-8 之间");
    }
}
