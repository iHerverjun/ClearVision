// FlowExecutionServiceTests.cs
// FlowExecutionServiceTests测试
// 作者：蘅芜君

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
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
    public async Task ExecuteFlowDebugAsync_CachedIntermediateResult_ShouldStayIsolatedFromExternalMutation()
    {
        var executeCount = 0;
        _executor.ValidateParameters(Arg.Any<Operator>()).Returns(new ValidationResult { IsValid = true });
        _executor.ExecuteAsync(
                Arg.Any<Operator>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                executeCount++;
                return Task.FromResult(OperatorExecutionOutput.Success(
                    new Dictionary<string, object>
                    {
                        ["Image"] = new byte[] { 10, 20, 30 },
                        ["Score"] = 7
                    },
                    executionTimeMs: 3));
            });

        var flow = new OperatorFlow("CacheIsolationFlow");
        var op = CreateOperatorWithPorts("Single", OperatorType.Thresholding);
        flow.AddOperator(op);

        var debugSessionId = Guid.NewGuid();
        var debugOptions = new DebugOptions
        {
            DebugSessionId = debugSessionId,
            EnableIntermediateCache = true
        };
        var inputData = new Dictionary<string, object> { ["Image"] = new byte[] { 1, 2, 3 } };

        var firstResult = await _sut.ExecuteFlowDebugAsync(flow, debugOptions, inputData);
        var firstSnapshotBytes = (byte[])firstResult.DebugOperatorResults.Single().OutputSnapshot!["Image"];
        firstSnapshotBytes[0] = 99;

        var firstIntermediateBytes = (byte[])firstResult.IntermediateResults[op.Id]["Image"];
        firstIntermediateBytes[1] = 88;

        var externalCacheRead = _sut.GetDebugIntermediateResult(debugSessionId, op.Id)!;
        ((byte[])externalCacheRead["Image"])[2] = 77;

        var secondResult = await _sut.ExecuteFlowDebugAsync(flow, debugOptions, inputData);

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        executeCount.Should().Be(1, "second debug run should hit intermediate cache");

        var secondSnapshotBytes = (byte[])secondResult.DebugOperatorResults.Single().OutputSnapshot!["Image"];
        secondSnapshotBytes.Should().Equal(10, 20, 30);

        var secondCacheRead = _sut.GetDebugIntermediateResult(debugSessionId, op.Id)!;
        ((byte[])secondCacheRead["Image"]).Should().Equal(10, 20, 30);
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
        flow.AddOperator(CreateOperatorWithPorts("Slow", OperatorType.Thresholding));
        flow.AddOperator(CreateOperatorWithPorts("Fail", OperatorType.Thresholding));

        var result = await _sut.ExecuteFlowAsync(flow, enableParallel: true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Fail");
        await canceledTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var slowResult = result.OperatorResults.Single(r => r.OperatorName == "Slow");
        slowResult.IsSuccess.Should().BeFalse();
        slowResult.ErrorMessage.Should().Contain("canceled");
        result.OperatorResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteFlowAsync_WhenParallelFailuresRace_UsesFirstSignaledFailureForErrorMessage()
    {
        var slowReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFailuresTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _executor.ValidateParameters(Arg.Any<Operator>()).Returns(new ValidationResult { IsValid = true });
        _executor.ExecuteAsync(
                Arg.Any<Operator>(),
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var op = callInfo.Arg<Operator>();
                if (string.Equals(op.Name, "Fail", StringComparison.Ordinal))
                {
                    return ReleasePrimaryFailureAsync(slowReadyTcs, releaseFailuresTcs);
                }

                return ReleaseSecondaryFailureAsync(releaseFailuresTcs, slowReadyTcs);
            });

        var flow = new OperatorFlow("ParallelFailureRaceFlow");
        flow.AddOperator(CreateOperatorWithPorts("Slow", OperatorType.Thresholding));
        flow.AddOperator(CreateOperatorWithPorts("Fail", OperatorType.Thresholding));

        var result = await _sut.ExecuteFlowAsync(flow, enableParallel: true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Fail");
    }

    [Fact]
    public void PrepareOperatorInputs_LargeGraph_ShouldPreserveSemantics_AndHitIndexLookups()
    {
        // Arrange: build a large graph with many unrelated nodes/connections.
        var flow = new OperatorFlow("LargeGraph");

        var source = new Operator("Source", OperatorType.Thresholding, 0, 0);
        source.AddOutputPort("Image", PortDataType.Image);

        var branch = new Operator("Branch", OperatorType.ConditionalBranch, 0, 0);
        branch.AddOutputPort("True", PortDataType.Image);
        branch.AddOutputPort("False", PortDataType.Image);

        var target = new Operator("Target", OperatorType.Thresholding, 0, 0);
        target.AddInputPort("Foreground", PortDataType.Image, true);
        target.AddInputPort("DecisionInput", PortDataType.Image, true);

        flow.AddOperator(source);
        flow.AddOperator(branch);
        flow.AddOperator(target);

        var previousNoise = new Operator("Noise-0", OperatorType.Thresholding, 0, 0);
        previousNoise.AddInputPort("Input", PortDataType.Image, true);
        previousNoise.AddOutputPort("Output", PortDataType.Image);
        flow.AddOperator(previousNoise);
        flow.AddConnection(CreateConnection(source, previousNoise));

        for (var i = 1; i < 180; i++)
        {
            var currentNoise = new Operator($"Noise-{i}", OperatorType.Thresholding, 0, 0);
            currentNoise.AddInputPort("Input", PortDataType.Image, true);
            currentNoise.AddOutputPort("Output", PortDataType.Image);
            flow.AddOperator(currentNoise);
            flow.AddConnection(CreateConnection(previousNoise, currentNoise));
            previousNoise = currentNoise;
        }

        flow.AddConnection(new OperatorConnection(
            source.Id,
            source.OutputPorts.Single(p => p.Name == "Image").Id,
            target.Id,
            target.InputPorts.Single(p => p.Name == "Foreground").Id));

        flow.AddConnection(new OperatorConnection(
            branch.Id,
            branch.OutputPorts.Single(p => p.Name == "True").Id,
            target.Id,
            target.InputPorts.Single(p => p.Name == "DecisionInput").Id));

        var operatorOutputs = new Dictionary<Guid, Dictionary<string, object>>
        {
            [source.Id] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Image"] = "image-from-source",
                ["Metadata"] = "meta-from-source"
            },
            [branch.Id] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["True"] = "true-branch-image",
                ["False"] = null!,
                ["Result"] = true,
                ["Condition"] = "Score > 0.5",
                ["ActualValue"] = 0.82d
            }
        };

        var buildIndexMethod = typeof(FlowExecutionService)
            .GetMethod("BuildFlowInputPreparationIndex", BindingFlags.Static | BindingFlags.NonPublic)!;
        var index = buildIndexMethod.Invoke(null, [flow]);

        var prepareMethod = typeof(FlowExecutionService)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "PrepareOperatorInputs" && method.GetParameters().Length == 4);

        // Act
        var inputs = (Dictionary<string, object>)prepareMethod.Invoke(_sut, [flow, target, operatorOutputs, index])!;

        // Assert: mapping and branch routing semantics remain unchanged.
        inputs["Foreground"].Should().Be("image-from-source");
        inputs["DecisionInput"].Should().Be("true-branch-image");
        inputs["True"].Should().Be("true-branch-image");
        inputs["ConditionResult"].Should().Be(true);
        inputs["Condition"].Should().Be("Score > 0.5");
        inputs["ActualValue"].Should().Be(0.82d);
        inputs["Metadata"].Should().Be("meta-from-source");
        inputs.ContainsKey("False").Should().BeFalse("null branch payload should not be propagated");

        // Assert: indexed lookups are exercised in large graph path.
        ReadIndexLookupCount(index!, "IncomingConnectionLookupCount").Should().BeGreaterThan(0);
        ReadIndexLookupCount(index!, "SourceOperatorLookupCount").Should().BeGreaterThan(0);
        ReadIndexLookupCount(index!, "SourcePortLookupCount").Should().BeGreaterThan(0);
        ReadIndexLookupCount(index!, "TargetPortLookupCount").Should().BeGreaterThan(0);
    }

    [Fact]
    public void CreateDebugCacheFingerprint_SameTextDifferentScalarTypes_ShouldNotCollide()
    {
        var method = typeof(FlowExecutionService).GetMethod(
            "CreateDebugCacheFingerprint",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();

        var op = CreateOperatorWithPorts("FingerprintTarget", OperatorType.Thresholding);
        var intInputs = new Dictionary<string, object> { ["Value"] = 1 };
        var stringInputs = new Dictionary<string, object> { ["Value"] = "1" };

        var intFingerprint = method!.Invoke(null, new object?[] { op, intInputs }) as string;
        var stringFingerprint = method.Invoke(null, new object?[] { op, stringInputs }) as string;

        intFingerprint.Should().NotBeNullOrWhiteSpace();
        stringFingerprint.Should().NotBeNullOrWhiteSpace();
        intFingerprint.Should().NotBe(stringFingerprint);
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

    private static async Task<OperatorExecutionOutput> ReleasePrimaryFailureAsync(
        TaskCompletionSource<bool> slowReadyTcs,
        TaskCompletionSource<bool> releaseFailuresTcs)
    {
        await slowReadyTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        releaseFailuresTcs.TrySetResult(true);
        return OperatorExecutionOutput.Failure("primary boom");
    }

    private static async Task<OperatorExecutionOutput> ReleaseSecondaryFailureAsync(
        TaskCompletionSource<bool> releaseFailuresTcs,
        TaskCompletionSource<bool> slowReadyTcs)
    {
        slowReadyTcs.TrySetResult(true);
        await releaseFailuresTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        return OperatorExecutionOutput.Failure("secondary boom");
    }

    private static int ReadIndexLookupCount(object index, string propertyName)
    {
        var property = index.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!;
        return (int)property.GetValue(index)!;
    }
}
