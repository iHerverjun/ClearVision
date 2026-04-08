// FlowExecutionServiceTests.cs
// FlowExecutionServiceTests测试
// 作者：蘅芜君

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Acme.Product.Tests.Services;

public class FlowExecutionServiceTests
{
    private readonly FlowExecutionService _sut;
    private readonly IOperatorExecutor _executor;
    private readonly ILogger<FlowExecutionService> _logger;
    private readonly IVariableContext _variableContext;

    public FlowExecutionServiceTests()
    {
        _executor = Substitute.For<IOperatorExecutor>();
        // Fix: Configure mock property BEFORE initializing service so dictionary key is correct
        _executor.OperatorType.Returns(OperatorType.Thresholding);

        _logger = Substitute.For<ILogger<FlowExecutionService>>();
        _variableContext = Substitute.For<IVariableContext>();

        var executors = new List<IOperatorExecutor> { _executor };
        _sut = new FlowExecutionService(executors, _logger, _variableContext);
    }

    [Fact]
    public async Task ExecuteFlowAsync_ShouldRespectCancellation()
    {
        // Arrange
        var flow = new OperatorFlow("TestFlow");
        var op = new Operator(Guid.NewGuid(), "LongRunningOp", OperatorType.Thresholding, 0, 0);
        flow.AddOperator(op);

        // Note: Executor properties are configured in constructor
        _executor.ExecuteAsync(
                Arg.Any<Operator>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(async x =>
            {
                var ct = x.Arg<CancellationToken>();
                await Task.Delay(500, ct); // Simulate work longer than cancellation delay
                return OperatorExecutionOutput.Success(new Dictionary<string, object>(), 500);
            });

        var cts = new CancellationTokenSource();

        // Act
        // Start the task but don't await it immediately
        var task = _sut.ExecuteFlowAsync(flow, cancellationToken: cts.Token);

        // Cancel quickly (before the 500ms delay finishes)
        await Task.Delay(100);
        cts.Cancel();

        // Await the task, expecting it to complete (possibly with failure)
        var result = await task;

        // Assert
        result.IsSuccess.Should().BeFalse("Flow execution should be cancelled");
    }

    [Fact]
    public async Task ExecuteFlowDebugAsync_ReusesCachedUpstreamOutputs_WhenOnlyTargetParametersChange()
    {
        var callCounts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        _executor.ValidateParameters(Arg.Any<Operator>()).Returns(new ValidationResult { IsValid = true });
        _executor.ExecuteAsync(
                Arg.Any<Operator>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var op = callInfo.Arg<Operator>();
                callCounts.AddOrUpdate(op.Name, 1, (_, current) => current + 1);

                return Task.FromResult(OperatorExecutionOutput.Success(
                    new Dictionary<string, object>
                    {
                        ["Image"] = new byte[] { (byte)callCounts[op.Name] },
                        ["BlobCount"] = op.Parameters.FirstOrDefault(p => p.Name == "Threshold")?.GetValue() ?? 0
                    },
                    executionTimeMs: 5));
            });

        var flow = new OperatorFlow("DebugFlow");
        var upstream = CreateOperatorWithPorts("Upstream", OperatorType.Thresholding);
        var target = CreateOperatorWithPorts("Target", OperatorType.Thresholding);
        target.AddParameter(new Parameter(Guid.NewGuid(), "Threshold", "Threshold", string.Empty, "int", 128, 0, 255, true));

        flow.AddOperator(upstream);
        flow.AddOperator(target);
        flow.AddConnection(CreateConnection(upstream, target));

        var debugSessionId = Guid.NewGuid();
        var inputData = new Dictionary<string, object> { ["Image"] = new byte[] { 1, 2, 3 } };

        var firstResult = await _sut.ExecuteFlowDebugAsync(
            flow,
            new DebugOptions
            {
                DebugSessionId = debugSessionId,
                EnableIntermediateCache = true,
                BreakAtOperatorId = target.Id
            },
            inputData);

        target.Parameters.Single(parameter => parameter.Name == "Threshold").SetValue(180);

        var secondResult = await _sut.ExecuteFlowDebugAsync(
            flow,
            new DebugOptions
            {
                DebugSessionId = debugSessionId,
                EnableIntermediateCache = true,
                BreakAtOperatorId = target.Id
            },
            inputData);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        callCounts["Upstream"].Should().Be(1, "upstream outputs should be reused from the debug cache");
        callCounts["Target"].Should().Be(2, "the edited target node still needs to run again");
    }

    [Fact]
    public async Task ExecuteFlowAsync_WhenParallelLayerFails_CancelsSiblingOperators()
    {
        var slowStartedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _executor.ValidateParameters(Arg.Any<Operator>()).Returns(new ValidationResult { IsValid = true });
        _executor.ExecuteAsync(
                Arg.Any<Operator>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var op = callInfo.Arg<Operator>();
                var ct = callInfo.ArgAt<CancellationToken>(2);
                if (string.Equals(op.Name, "Fail", StringComparison.Ordinal))
                {
                    return WaitForSiblingToStartThenFailAsync(slowStartedTcs);
                }

                slowStartedTcs.TrySetResult(true);
                return WaitForOperatorCancellationAsync(ct, canceledTcs);
            });

        var flow = new OperatorFlow("ParallelFailureFlow");
        flow.AddOperator(CreateOperatorWithPorts("Fail", OperatorType.Thresholding));
        flow.AddOperator(CreateOperatorWithPorts("Slow", OperatorType.Thresholding));

        var result = await _sut.ExecuteFlowAsync(flow, enableParallel: true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Fail");
        var slowResult = result.OperatorResults.Single(r => r.OperatorName == "Slow");
        slowResult.IsSuccess.Should().BeFalse();
        slowResult.ErrorMessage.Should().Contain("canceled");
        result.OperatorResults.Should().HaveCount(2);
    }

    private static Operator CreateOperatorWithPorts(string name, OperatorType type)
    {
        var op = new Operator(name, type, 0, 0);
        op.AddInputPort("Input", PortDataType.Image, true);
        op.AddOutputPort("Output", PortDataType.Image);
        return op;
    }

    private static OperatorConnection CreateConnection(Operator source, Operator target)
    {
        return new OperatorConnection(
            source.Id,
            source.OutputPorts.First().Id,
            target.Id,
            target.InputPorts.First().Id);
    }

    private static async Task<OperatorExecutionOutput> WaitForOperatorCancellationAsync(
        CancellationToken cancellationToken,
        TaskCompletionSource<bool> canceledTcs)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return OperatorExecutionOutput.Success(new Dictionary<string, object>());
        }
        catch (OperationCanceledException)
        {
            canceledTcs.TrySetResult(true);
            throw;
        }
    }

    private static async Task<OperatorExecutionOutput> WaitForSiblingToStartThenFailAsync(
        TaskCompletionSource<bool> slowStartedTcs)
    {
        await slowStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        return OperatorExecutionOutput.Failure("boom");
    }
}
