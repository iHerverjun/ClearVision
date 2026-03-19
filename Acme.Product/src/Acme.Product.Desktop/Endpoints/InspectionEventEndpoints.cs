using System.Text.Json;
using System.Threading.Channels;
using Acme.Product.Core.Events;
using Acme.Product.Core.Services;
using Acme.Product.Desktop.Inspection;
using Acme.Product.Infrastructure.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Acme.Product.Desktop.Endpoints;

public static class InspectionEventEndpoints
{
    public static IEndpointRouteBuilder MapInspectionEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/inspection/realtime/{projectId:guid}/events", HandleSseEventsAsync);
        return app;
    }

    private static async Task HandleSseEventsAsync(
        Guid projectId,
        HttpContext context,
        IInspectionEventBus eventBus,
        IEventStore eventStore,
        IInspectionRuntimeCoordinator coordinator,
        CancellationToken ct)
    {
        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        await context.Response.StartAsync(ct);

        var lastSequenceId = ParseLastEventId(context.Request);
        var currentState = coordinator.GetState(projectId);

        if (currentState is not null)
        {
            foreach (var snapshot in InspectionRealtimeEventMapper.CreateSnapshot(currentState))
            {
                await context.Response.WriteSseMessageAsync(
                    new SseMessage(
                        null,
                        snapshot.EventType,
                        snapshot.Payload),
                    ct);
            }
        }

        if (lastSequenceId > 0)
        {
            foreach (var storedEvent in eventStore.GetEventsAfter(projectId, lastSequenceId))
            {
                foreach (var mappedEvent in InspectionRealtimeEventMapper.Map(storedEvent.Event))
                {
                    await context.Response.WriteSseMessageAsync(
                        new SseMessage(
                            storedEvent.SequenceId,
                            mappedEvent.EventType,
                            mappedEvent.Payload),
                        ct);
                }
            }
        }

        var channel = Channel.CreateUnbounded<SseMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        using var subscription = eventBus.SubscribeInterface<IInspectionEvent>((evt, _) =>
        {
            if (evt.ProjectId != projectId)
            {
                return Task.CompletedTask;
            }

            var sequenceId = eventStore.Append(projectId, evt);
            foreach (var mappedEvent in InspectionRealtimeEventMapper.Map(evt))
            {
                channel.Writer.TryWrite(new SseMessage(sequenceId, mappedEvent.EventType, mappedEvent.Payload));
            }
            return Task.CompletedTask;
        });

        using var channelRegistration = ct.Register(() => channel.Writer.TryComplete());
        var heartbeatTask = SendHeartbeatsAsync(channel.Writer, ct);

        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                await context.Response.WriteSseMessageAsync(message, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected.
        }
        finally
        {
            channel.Writer.TryComplete();

            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation during shutdown.
            }
        }
    }

    private static long ParseLastEventId(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Last-Event-ID", out var lastEventIdHeader) &&
            long.TryParse(lastEventIdHeader.FirstOrDefault(), out var parsedId))
        {
            return parsedId;
        }

        return 0;
    }

    private static async Task SendHeartbeatsAsync(ChannelWriter<SseMessage> writer, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                writer.TryWrite(new SseMessage(null, "heartbeat", new { timestamp = DateTime.UtcNow }));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public static async Task WriteSseMessageAsync(this HttpResponse response, SseMessage message, CancellationToken ct)
    {
        if (message.EventType == "heartbeat")
        {
            await response.WriteAsync(":keepalive\n\n", ct);
            await response.Body.FlushAsync(ct);
            return;
        }

        var json = JsonSerializer.Serialize(
            message.Data,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (message.SequenceId.HasValue)
        {
            await response.WriteAsync($"id: {message.SequenceId.Value}\n", ct);
        }

        await response.WriteAsync($"event: {message.EventType}\n", ct);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}

public sealed record SseMessage(long? SequenceId, string EventType, object Data);
