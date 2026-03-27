using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Services;

public class FlowNodePreviewServiceTests
{
    [Fact]
    public async Task PreviewWithMetricsAsync_ShouldNotInjectExternalImage_WhenImageAcquisitionExistsUpstream()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        Dictionary<string, object>? capturedInput = null;
        var acquisition = new Operator("Acquire", OperatorType.ImageAcquisition, 0, 0);
        var target = new Operator("Resize", OperatorType.ImageResize, 0, 0);
        var flow = new OperatorFlow("preview-flow");
        flow.AddOperator(acquisition);
        flow.AddOperator(target);
        flow.Connections.Add(new OperatorConnection(acquisition.Id, Guid.NewGuid(), target.Id, Guid.NewGuid()));

        flowExecution.ExecuteFlowDebugAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<DebugOptions>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedInput = callInfo.ArgAt<Dictionary<string, object>?>(2);
                return Task.FromResult(new FlowDebugExecutionResult
                {
                    IsSuccess = true,
                    DebugSessionId = Guid.NewGuid(),
                    IntermediateResults = new Dictionary<Guid, Dictionary<string, object>>
                    {
                        [target.Id] = new()
                        {
                            ["Image"] = CreatePreviewImageBytes()
                        }
                    }
                });
            });

        var service = new FlowNodePreviewService(
            NullLogger<FlowNodePreviewService>.Instance,
            flowExecution,
            Substitute.For<IPreviewMetricsAnalyzer>());

        var result = await service.PreviewWithMetricsAsync(flow, target.Id, new byte[] { 9, 9, 9 });

        result.Success.Should().BeTrue();
        capturedInput.Should().BeNull();
    }

    [Fact]
    public async Task PreviewWithMetricsAsync_ShouldUseBundledLabels_WhenLabelsPathIsBlank()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var target = new Operator("DeepLearning", OperatorType.DeepLearning, 0, 0);
        target.AddParameter(new Parameter(Guid.NewGuid(), "ModelPath", "ModelPath", string.Empty, "string", Path.GetTempFileName()));
        target.AddParameter(new Parameter(Guid.NewGuid(), "LabelsPath", "LabelsPath", string.Empty, "string", string.Empty));
        target.AddParameter(new Parameter(Guid.NewGuid(), "TargetClasses", "TargetClasses", string.Empty, "string", "Wire_Black,Wire_Blue"));

        var flow = new OperatorFlow("wire-sequence-flow");
        flow.AddOperator(target);

        flowExecution.ExecuteFlowDebugAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<DebugOptions>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowDebugExecutionResult
            {
                IsSuccess = true,
                DebugSessionId = Guid.NewGuid(),
                IntermediateResults = new Dictionary<Guid, Dictionary<string, object>>
                {
                    [target.Id] = new()
                    {
                        ["Image"] = CreatePreviewImageBytes()
                    }
                }
            }));

        var service = new FlowNodePreviewService(
            NullLogger<FlowNodePreviewService>.Instance,
            flowExecution,
            Substitute.For<IPreviewMetricsAnalyzer>());

        try
        {
            var result = await service.PreviewWithMetricsAsync(flow, target.Id, null);

            result.Success.Should().BeTrue();
            result.MissingResources.Should().NotContain(item => item.ResourceKey == "DeepLearning.LabelsPath");
        }
        finally
        {
            var modelPath = target.Parameters.Single(item => item.Name == "ModelPath").GetValue()?.ToString();
            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                File.Delete(modelPath);
            }
        }
    }

    [Fact]
    public async Task PreviewWithMetricsAsync_ShouldExcludeOriginalImageFromOutputs()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var target = new Operator("Resize", OperatorType.ImageResize, 0, 0);
        var flow = new OperatorFlow("preview-flow");
        flow.AddOperator(target);
        var previewImage = new byte[] { 1, 2, 3, 4 };
        var originalImage = new byte[] { 9, 8, 7, 6 };

        flowExecution.ExecuteFlowDebugAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<DebugOptions>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowDebugExecutionResult
            {
                IsSuccess = true,
                DebugSessionId = Guid.NewGuid(),
                IntermediateResults = new Dictionary<Guid, Dictionary<string, object>>
                {
                    [target.Id] = new()
                    {
                        ["Image"] = previewImage,
                        ["OriginalImage"] = originalImage,
                        ["ObjectCount"] = 2
                    }
                }
            }));

        var service = new FlowNodePreviewService(
            NullLogger<FlowNodePreviewService>.Instance,
            flowExecution,
            Substitute.For<IPreviewMetricsAnalyzer>());

        var result = await service.PreviewWithMetricsAsync(flow, target.Id, null);

        result.Success.Should().BeTrue();
        result.PreviewImage.Should().Equal(previewImage);
        result.PreviewImage.Should().NotEqual(originalImage);
        result.Outputs.Should().ContainKey("ObjectCount");
        result.Outputs.Should().NotContainKey("Image");
        result.Outputs.Should().NotContainKey("OriginalImage");
    }

    private static byte[] CreatePreviewImageBytes()
    {
        using var image = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(255));
        Cv2.ImEncode(".png", image, out var encoded);
        return encoded;
    }
}
