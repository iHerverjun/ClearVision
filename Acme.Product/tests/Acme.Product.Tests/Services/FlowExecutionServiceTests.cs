// FlowExecutionServiceTests.cs
// FlowExecutionServiceTests测试
// 作者：蘅芜君

using System;
using System.Collections.Generic;
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

    public FlowExecutionServiceTests()
    {
        _executor = Substitute.For<IOperatorExecutor>();
        // Fix: Configure mock property BEFORE initializing service so dictionary key is correct
        _executor.OperatorType.Returns(OperatorType.Thresholding);

        _logger = Substitute.For<ILogger<FlowExecutionService>>();

        var executors = new List<IOperatorExecutor> { _executor };
        _sut = new FlowExecutionService(executors, _logger);
    }

    [Fact]
    public async Task ExecuteFlowAsync_ShouldRespectCancellation()
    {
        // Arrange
        var flow = new OperatorFlow("TestFlow");
        var op = new Operator(Guid.NewGuid(), "LongRunningOp", OperatorType.Thresholding, 0, 0);
        flow.AddOperator(op);

        // Note: Executor properties are configured in constructor
        _executor.ExecuteAsync(Arg.Any<Operator>(), Arg.Any<Dictionary<string, object>>())
            .Returns(async x =>
            {
                await Task.Delay(500); // Simulate work longer than cancellation delay
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
        result.ErrorMessage.Should().Contain("流程被取消", "Error message should indicate cancellation");
    }
}
