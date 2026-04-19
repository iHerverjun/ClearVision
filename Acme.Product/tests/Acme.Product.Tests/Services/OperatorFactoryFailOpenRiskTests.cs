using System.Diagnostics;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;

namespace Acme.Product.Tests.Services;

public class OperatorFactoryFailOpenRiskTests
{
    [Fact]
    public void Constructor_ShouldFailFast_WhenMetadataScanThrows_InStrictMode()
    {
        Action act = () => _ = new OperatorFactory(
            scanMetadata: () => throw new InvalidOperationException("scan-boom"),
            strictMetadataScan: true);

        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Metadata initialization failed");
        exception.InnerException.Should().NotBeNull();
        exception.InnerException!.Message.Should().Contain("scan-boom");
    }

    [Fact]
    public void Constructor_ShouldEmitTraceError_WhenMetadataScanThrows_InNonStrictMode()
    {
        using var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            _ = new OperatorFactory(
                scanMetadata: () => throw new InvalidOperationException("scan-boom"),
                strictMetadataScan: false);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        listener.Messages.Should().Contain(message =>
            message.Contains("Attribute metadata scan failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateOperator_ShouldThrow_WhenMetadataIsUnavailable_InsteadOfFailOpenAnyPorts()
    {
        var factory = new OperatorFactory(
            scanMetadata: () => [],
            strictMetadataScan: false);

        Action act = () => factory.CreateOperator(OperatorType.Comment, "comment", 0, 0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Metadata missing for operator type*")
            .WithMessage("*fail-open Any-port fallback*");
    }

    [Fact]
    public void CreateOperator_ShouldUseMetadataPorts_WhenMetadataExists()
    {
        var factory = new OperatorFactory(
            scanMetadata: () =>
            [
                new OperatorMetadata
                {
                    Type = OperatorType.Comment,
                    DisplayName = "Comment",
                    Description = "Comment metadata",
                    Category = "Utility",
                    InputPorts = [new PortDefinition { Name = "ImageIn", DisplayName = "ImageIn", DataType = PortDataType.Image, IsRequired = true }],
                    OutputPorts = [new PortDefinition { Name = "ImageOut", DisplayName = "ImageOut", DataType = PortDataType.Image }],
                    Parameters = [new ParameterDefinition { Name = "Note", DisplayName = "Note", DataType = "string", DefaultValue = "v", IsRequired = false }]
                }
            ],
            strictMetadataScan: true);

        var op = factory.CreateOperator(OperatorType.Comment, "comment", 1, 2);

        op.InputPorts.Should().ContainSingle(port => port.Name == "ImageIn" && port.DataType == PortDataType.Image);
        op.OutputPorts.Should().ContainSingle(port => port.Name == "ImageOut" && port.DataType == PortDataType.Image);
        op.Parameters.Should().ContainSingle(parameter => parameter.Name == "Note");
    }

    private sealed class CapturingTraceListener : TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Messages.Add(message);
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Messages.Add(message);
            }
        }
    }
}
