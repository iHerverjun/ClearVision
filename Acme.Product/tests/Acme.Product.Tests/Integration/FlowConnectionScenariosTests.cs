// FlowConnectionScenariosTests.cs
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Integration;

public class FlowConnectionScenariosTests
{
    [Fact]
    public async Task ExecuteFlowAsync_ImageToCommentToResult_WithPortRemap_ShouldReturnImageBytes()
    {
        var service = CreateFlowService(
            new ImageAcquisitionOperator(Substitute.For<ILogger<ImageAcquisitionOperator>>(), Substitute.For<ICameraManager>()),
            new CommentOperator(Substitute.For<ILogger<CommentOperator>>()),
            new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>()));

        var flow = new OperatorFlow();
        var acquisition = CreateOperator("acquisition", OperatorType.ImageAcquisition, outputPorts: [("Image", PortDataType.Image)]);
        var comment = CreateOperator("comment", OperatorType.Comment, inputPorts: [("Input", PortDataType.Any)], outputPorts: [("Output", PortDataType.Any)]);
        var result = CreateOperator("result", OperatorType.ResultOutput, inputPorts: [("Image", PortDataType.Image)], outputPorts: [("Output", PortDataType.Any)]);

        flow.AddOperator(acquisition);
        flow.AddOperator(comment);
        flow.AddOperator(result);
        flow.AddConnection(CreateConnection(acquisition, "Image", comment, "Input"));
        flow.AddConnection(CreateConnection(comment, "Output", result, "Image"));

        var output = await service.ExecuteFlowAsync(flow, new Dictionary<string, object>
        {
            { "Image", CreateTestImageBytes() }
        });

        output.IsSuccess.Should().BeTrue(output.ErrorMessage);
        output.OutputData.Should().ContainKey("Image");
        output.OutputData!["Image"].Should().BeAssignableTo<byte[]>();
        ((byte[])output.OutputData["Image"]).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteFlowAsync_ImageToResultPort_ShouldPreserveImageWrapperLifetime()
    {
        var service = CreateFlowService(
            new ImageAcquisitionOperator(Substitute.For<ILogger<ImageAcquisitionOperator>>(), Substitute.For<ICameraManager>()),
            new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>()));

        var flow = new OperatorFlow();
        var acquisition = CreateOperator("acquisition", OperatorType.ImageAcquisition, outputPorts: [("Image", PortDataType.Image)]);
        var result = CreateOperator(
            "result",
            OperatorType.ResultOutput,
            inputPorts: [("Result", PortDataType.Any)],
            outputPorts: [("Output", PortDataType.Any)]);

        flow.AddOperator(acquisition);
        flow.AddOperator(result);
        flow.AddConnection(CreateConnection(acquisition, "Image", result, "Result"));

        var output = await service.ExecuteFlowAsync(flow, new Dictionary<string, object>
        {
            { "Image", CreateTestImageBytes() }
        });

        output.IsSuccess.Should().BeTrue(output.ErrorMessage);
        output.OutputData.Should().ContainKey("Result");
        output.OutputData!["Result"].Should().BeAssignableTo<byte[]>();
        ((byte[])output.OutputData["Result"]).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteFlowAsync_ConditionalTrueBranchToResult_ShouldRouteImageSuccessfully()
    {
        var service = CreateFlowService(
            new ImageAcquisitionOperator(Substitute.For<ILogger<ImageAcquisitionOperator>>(), Substitute.For<ICameraManager>()),
            new ConditionalBranchOperator(Substitute.For<ILogger<ConditionalBranchOperator>>()),
            new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>()));

        var flow = new OperatorFlow();
        var acquisition = CreateOperator("acquisition", OperatorType.ImageAcquisition, outputPorts: [("Image", PortDataType.Image)]);
        var branch = CreateOperator(
            "branch",
            OperatorType.ConditionalBranch,
            inputPorts: [("Value", PortDataType.Any)],
            outputPorts: [("True", PortDataType.Any), ("False", PortDataType.Any)]);
        var result = CreateOperator("result", OperatorType.ResultOutput, inputPorts: [("Image", PortDataType.Image)], outputPorts: [("Output", PortDataType.Any)]);

        branch.AddParameter(new Parameter(Guid.NewGuid(), "Condition", "Condition", string.Empty, "string", "Contains", null, null, true));
        branch.AddParameter(new Parameter(Guid.NewGuid(), "CompareValue", "CompareValue", string.Empty, "string", "ImageWrapper", null, null, true));

        flow.AddOperator(acquisition);
        flow.AddOperator(branch);
        flow.AddOperator(result);
        flow.AddConnection(CreateConnection(acquisition, "Image", branch, "Value"));
        flow.AddConnection(CreateConnection(branch, "True", result, "Image"));

        var output = await service.ExecuteFlowAsync(flow, new Dictionary<string, object>
        {
            { "Image", CreateTestImageBytes() }
        });

        output.IsSuccess.Should().BeTrue(output.ErrorMessage);
        output.OutputData.Should().ContainKey("Image");
        output.OutputData!["Image"].Should().BeAssignableTo<byte[]>();
        ((byte[])output.OutputData["Image"]).Length.Should().BeGreaterThan(0);
        output.OutputData.Should().ContainKey("ConditionResult");
        output.OutputData["ConditionResult"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteFlowAsync_DualInputSameConsumer_ShouldNotOverRetainSourceImage()
    {
        var sourceExecutor = new TestImageSourceOperator(NullLogger<TestImageSourceOperator>.Instance);
        var consumerExecutor = new TestDualInputConsumerOperator(NullLogger<TestDualInputConsumerOperator>.Instance);
        var service = CreateFlowService(sourceExecutor, consumerExecutor);

        var flow = new OperatorFlow();
        var source = CreateOperator("source", OperatorType.ImageAcquisition, outputPorts: [("Image", PortDataType.Image)]);
        var consumer = CreateOperator(
            "consumer",
            OperatorType.ImageDiff,
            inputPorts: [("InputA", PortDataType.Image), ("InputB", PortDataType.Image)],
            outputPorts: [("Output", PortDataType.Any)]);

        flow.AddOperator(source);
        flow.AddOperator(consumer);
        flow.AddConnection(CreateConnection(source, "Image", consumer, "InputA"));
        flow.AddConnection(CreateConnection(source, "Image", consumer, "InputB"));

        var output = await service.ExecuteFlowAsync(flow);

        output.IsSuccess.Should().BeTrue(output.ErrorMessage);
        output.OutputData.Should().ContainKey("SameRef");
        output.OutputData!["SameRef"].Should().Be(true);
        sourceExecutor.LastOutputImage.Should().NotBeNull();
        sourceExecutor.LastOutputImage!.RefCount.Should().Be(0);
    }

    private static IFlowExecutionService CreateFlowService(params IOperatorExecutor[] executors)
    {
        return new FlowExecutionService(
            executors,
            Substitute.For<ILogger<FlowExecutionService>>(),
            Substitute.For<IVariableContext>());
    }

    private static Operator CreateOperator(
        string name,
        OperatorType type,
        IEnumerable<(string Name, PortDataType Type)>? inputPorts = null,
        IEnumerable<(string Name, PortDataType Type)>? outputPorts = null)
    {
        var op = new Operator(name, type, 0, 0);

        if (inputPorts != null)
        {
            foreach (var (portName, portType) in inputPorts)
            {
                op.AddInputPort(portName, portType, isRequired: false);
            }
        }

        if (outputPorts != null)
        {
            foreach (var (portName, portType) in outputPorts)
            {
                op.AddOutputPort(portName, portType);
            }
        }

        return op;
    }

    private static OperatorConnection CreateConnection(Operator source, string sourcePortName, Operator target, string targetPortName)
    {
        var sourcePort = source.OutputPorts.Single(p => p.Name == sourcePortName);
        var targetPort = target.InputPorts.Single(p => p.Name == targetPortName);
        return new OperatorConnection(source.Id, sourcePort.Id, target.Id, targetPort.Id);
    }

    private static byte[] CreateTestImageBytes()
    {
        const string base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
        return Convert.FromBase64String(base64Png);
    }

    private sealed class TestImageSourceOperator : OperatorBase
    {
        public override OperatorType OperatorType => OperatorType.ImageAcquisition;
        public ImageWrapper? LastOutputImage { get; private set; }

        public TestImageSourceOperator(ILogger<TestImageSourceOperator> logger) : base(logger)
        {
        }

        protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
            Operator @operator,
            Dictionary<string, object>? inputs,
            CancellationToken cancellationToken)
        {
            var mat = new Mat(8, 8, MatType.CV_8UC1, Scalar.All(255));
            var output = CreateImageOutput(mat);
            LastOutputImage = (ImageWrapper)output["Image"];
            return Task.FromResult(OperatorExecutionOutput.Success(output));
        }

        public override ValidationResult ValidateParameters(Operator @operator)
        {
            return ValidationResult.Valid();
        }
    }

    private sealed class TestDualInputConsumerOperator : OperatorBase
    {
        public override OperatorType OperatorType => OperatorType.ImageDiff;

        public TestDualInputConsumerOperator(ILogger<TestDualInputConsumerOperator> logger) : base(logger)
        {
        }

        protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
            Operator @operator,
            Dictionary<string, object>? inputs,
            CancellationToken cancellationToken)
        {
            if (!TryGetInputImage(inputs, "InputA", out var inputA) || inputA == null)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("missing InputA"));
            }

            if (!TryGetInputImage(inputs, "InputB", out var inputB) || inputB == null)
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("missing InputB"));
            }

            return Task.FromResult(OperatorExecutionOutput.Success(new Dictionary<string, object>
            {
                { "SameRef", ReferenceEquals(inputA, inputB) }
            }));
        }

        public override ValidationResult ValidateParameters(Operator @operator)
        {
            return ValidationResult.Valid();
        }
    }
}
