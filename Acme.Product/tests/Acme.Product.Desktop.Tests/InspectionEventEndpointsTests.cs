using System.Text;
using Acme.Product.Application.Services;
using Acme.Product.Core.Events;
using Acme.Product.Core.Services;
using Acme.Product.Desktop.Endpoints;
using Acme.Product.Desktop.Middleware;
using Acme.Product.Infrastructure.Events;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class InspectionEventEndpointsTests
{
    [Fact]
    public async Task EventsEndpoint_StreamsLiveEvents_AsSseFrames()
    {
        await using var host = await InspectionEventTestHost.CreateAsync();
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await host.Coordinator.TryStartAsync(projectId, sessionId, CancellationToken.None);
        host.Coordinator.UpdateSessionStatus(projectId, RuntimeStatus.Running);

        using var response = await host.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"/api/inspection/realtime/{projectId}/events"),
            HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();

        var initialChunk = await ReadUntilContainsAsync(stream, "event: stateChanged", TimeSpan.FromSeconds(2));
        initialChunk.Should().Contain("event: stateChanged");
        initialChunk.Should().NotContain("id:");
        initialChunk.Should().Contain("\"newState\":\"Running\"");
        initialChunk.Should().Contain("\"isSnapshot\":true");

        await host.EventBus.PublishAsync(new InspectionProgressEvent
        {
            ProjectId = projectId,
            SessionId = sessionId,
            ProcessedCount = 1
        });

        var eventChunk = await ReadUntilContainsAsync(stream, "event: progressChanged", TimeSpan.FromSeconds(2));
        eventChunk.Should().Contain("id: 1");
        eventChunk.Should().Contain("event: progressChanged");
        eventChunk.Should().Contain("\"processedCount\":1");
    }

    [Fact]
    public async Task EventsEndpoint_ReplaysStoredEvents_UsingStableSequenceIds()
    {
        await using var host = await InspectionEventTestHost.CreateAsync();
        var projectId = Guid.NewGuid();

        var firstSequence = host.EventStore.Append(projectId, new InspectionProgressEvent
        {
            ProjectId = projectId,
            SessionId = Guid.NewGuid(),
            ProcessedCount = 1
        });
        var secondSequence = host.EventStore.Append(projectId, new InspectionProgressEvent
        {
            ProjectId = projectId,
            SessionId = Guid.NewGuid(),
            ProcessedCount = 2
        });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/inspection/realtime/{projectId}/events");
        request.Headers.Add("Last-Event-ID", firstSequence.ToString());

        using var response = await host.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();

        var replayChunk = await ReadUntilContainsAsync(stream, "event: progressChanged", TimeSpan.FromSeconds(2));
        replayChunk.Should().Contain($"id: {secondSequence}");
        replayChunk.Should().Contain("event: progressChanged");
        replayChunk.Should().Contain("\"processedCount\":2");
    }

    [Fact]
    public async Task EventsEndpoint_AllowsAuthenticatedSseRequests_UsingQueryToken()
    {
        await using var host = await InspectionEventTestHost.CreateAsync(requireAuth: true);
        var projectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await host.Coordinator.TryStartAsync(projectId, sessionId, CancellationToken.None);
        host.Coordinator.UpdateSessionStatus(projectId, RuntimeStatus.Running);

        using var response = await host.Client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/inspection/realtime/{projectId}/events?token=test-token"),
            HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        var chunk = await ReadUntilContainsAsync(stream, "event: stateChanged", TimeSpan.FromSeconds(2));
        chunk.Should().Contain("\"newState\":\"Running\"");
    }

    private static async Task<string> ReadUntilContainsAsync(Stream stream, string marker, TimeSpan timeout)
    {
        var buffer = new byte[512];
        var builder = new StringBuilder();
        using var cts = new CancellationTokenSource(timeout);

        while (!builder.ToString().Contains(marker, StringComparison.Ordinal))
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
            bytesRead.Should().BeGreaterThan(0);
            builder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        return builder.ToString();
    }

    private sealed class InspectionEventTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private InspectionEventTestHost(
            WebApplication app,
            IInspectionEventBus eventBus,
            IEventStore eventStore,
            IInspectionRuntimeCoordinator coordinator)
        {
            _app = app;
            EventBus = eventBus;
            EventStore = eventStore;
            Coordinator = coordinator;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }
        public IInspectionEventBus EventBus { get; }
        public IEventStore EventStore { get; }
        public IInspectionRuntimeCoordinator Coordinator { get; }

        public static async Task<InspectionEventTestHost> CreateAsync(bool requireAuth = false)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();

            var eventStore = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
            var eventBus = new InMemoryInspectionEventBus(
                NullLogger<InMemoryInspectionEventBus>.Instance,
                eventStore);
            var coordinator = new InspectionRuntimeCoordinator(NullLogger<InspectionRuntimeCoordinator>.Instance);

            builder.Services.AddSingleton<IEventStore>(eventStore);
            builder.Services.AddSingleton<IInspectionEventBus>(eventBus);
            builder.Services.AddSingleton<IInspectionRuntimeCoordinator>(coordinator);

            if (requireAuth)
            {
                var authService = Substitute.For<IAuthService>();
                authService.GetSessionAsync("test-token").Returns(
                    Task.FromResult<Acme.Product.Application.Services.UserSession?>(new Acme.Product.Application.Services.UserSession
                {
                    UserId = "user-1",
                    Username = "tester",
                    Role = "Admin",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30)
                }));
                builder.Services.AddSingleton(authService);
            }

            var app = builder.Build();
            if (requireAuth)
            {
                app.UseMiddleware<AuthMiddleware>();
            }
            app.MapInspectionEventEndpoints();
            await app.StartAsync();

            return new InspectionEventTestHost(app, eventBus, eventStore, coordinator);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
