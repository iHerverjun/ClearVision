using Acme.Product.Application.Analysis;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Events;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Events;
using Acme.Product.Infrastructure.Metrics;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Collections.Concurrent;

namespace Acme.Product.Tests.Services;

public class InspectionWorkerTests
{
    [Fact]
    public async Task StopAsync_CancelsRunningTask_AndPublishesStoppedState()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        flowExecution.ExecuteFlowAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => WaitForCancellationAsync(callInfo.ArgAt<CancellationToken>(3)));

        var imageAcquisition = Substitute.For<IImageAcquisitionService>();
        var resultRepository = Substitute.For<IInspectionResultRepository>();
        var projectRepository = Substitute.For<IProjectRepository>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        using var serviceProvider = BuildScopedServices(
            flowExecution,
            imageAcquisition,
            resultRepository,
            projectRepository);

        var store = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
        var bus = new InMemoryInspectionEventBus(NullLogger<InMemoryInspectionEventBus>.Instance, store);
        var coordinator = new InspectionRuntimeCoordinator(NullLogger<InspectionRuntimeCoordinator>.Instance);
        var analysisDataBuilder = new AnalysisDataBuilder();
        var worker = new InspectionWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            coordinator,
            bus,
            NullLogger<InspectionWorker>.Instance,
            lifetime,
            new InspectionMetrics(),
            analysisDataBuilder);

        var stateChanges = new ConcurrentQueue<InspectionStateChangedEvent>();
        using var subscription = bus.Subscribe<InspectionStateChangedEvent>((evt, _) =>
        {
            stateChanges.Enqueue(evt);
            return Task.CompletedTask;
        });

        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        (await coordinator.TryStartAsync(projectId, sessionId, CancellationToken.None)).Should().Be(StartResult.Success);
        (await worker.TryStartRunAsync(projectId, sessionId, new OperatorFlow("Test"), null)).Should().BeTrue();

        await WaitUntilAsync(
            () => stateChanges.Any(evt => evt.NewState == "Running"),
            TimeSpan.FromSeconds(2));

        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        await WaitUntilAsync(
            () => stateChanges.Any(evt => evt.NewState == "Stopped"),
            TimeSpan.FromSeconds(2));
    }

    private static async Task<FlowExecutionResult> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new FlowExecutionResult { IsSuccess = true };
    }

    private static ServiceProvider BuildScopedServices(
        IFlowExecutionService flowExecution,
        IImageAcquisitionService imageAcquisition,
        IInspectionResultRepository resultRepository,
        IProjectRepository projectRepository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => flowExecution);
        services.AddScoped(_ => imageAcquisition);
        services.AddScoped(_ => resultRepository);
        services.AddScoped(_ => projectRepository);

        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - started > timeout)
            {
                throw new TimeoutException("Condition was not met within the expected timeout.");
            }

            await Task.Delay(25);
        }
    }
}
