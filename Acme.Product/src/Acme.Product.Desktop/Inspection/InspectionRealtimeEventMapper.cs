using Acme.Product.Core.Events;
using Acme.Product.Core.Services;

namespace Acme.Product.Desktop.Inspection;

public sealed record InspectionRealtimeMessage(string EventType, object Payload);

public static class InspectionRealtimeEventMapper
{
    public static IReadOnlyList<InspectionRealtimeMessage> CreateSnapshot(RuntimeState state)
    {
        var messages = new List<InspectionRealtimeMessage>
        {
            new(
                "stateChanged",
                BuildStatePayload(
                    state.ProjectId,
                    state.SessionId,
                    oldState: null,
                    newState: state.Status.ToString(),
                    errorMessage: state.ErrorMessage,
                    timestamp: DateTimeOffset.UtcNow,
                    isSnapshot: true,
                    startedAt: state.StartedAt,
                    stoppedAt: state.StoppedAt))
        };

        if (state.Status == RuntimeStatus.Faulted)
        {
            messages.Add(new(
                "faulted",
                BuildFaultPayload(
                    state.ProjectId,
                    state.SessionId,
                    state.ErrorMessage,
                    DateTimeOffset.UtcNow)));
        }

        return messages;
    }

    public static IReadOnlyList<InspectionRealtimeMessage> Map(IInspectionEvent evt)
    {
        return evt switch
        {
            InspectionStateChangedEvent stateChanged => MapStateChanged(stateChanged),
            InspectionResultEvent result => new[]
            {
                new InspectionRealtimeMessage(
                    "resultProduced",
                    new
                    {
                        projectId = result.ProjectId,
                        sessionId = result.SessionId,
                        resultId = result.ResultId,
                        imageId = result.ImageId,
                        status = result.Status,
                        defectCount = result.DefectCount,
                        processingTimeMs = result.ProcessingTimeMs,
                        errorMessage = result.ErrorMessage,
                        outputImageBase64 = result.OutputImageBase64,
                        outputData = result.OutputData,
                        analysisData = result.AnalysisData,
                        timestamp = result.Timestamp
                    })
            },
            InspectionProgressEvent progress => new[]
            {
                new InspectionRealtimeMessage(
                    "progressChanged",
                    new
                    {
                        projectId = progress.ProjectId,
                        sessionId = progress.SessionId,
                        processedCount = progress.ProcessedCount,
                        totalCount = progress.TotalCount,
                        progressPercentage = progress.ProgressPercentage,
                        currentOperator = progress.CurrentOperator,
                        timestamp = progress.Timestamp
                    })
            },
            HeartbeatEvent heartbeat => new[]
            {
                new InspectionRealtimeMessage(
                    "heartbeat",
                    new
                    {
                        projectId = heartbeat.ProjectId,
                        timestamp = heartbeat.Timestamp
                    })
            },
            _ => new[]
            {
                new InspectionRealtimeMessage(evt.GetType().Name, evt)
            }
        };
    }

    private static IReadOnlyList<InspectionRealtimeMessage> MapStateChanged(InspectionStateChangedEvent evt)
    {
        var messages = new List<InspectionRealtimeMessage>
        {
            new(
                "stateChanged",
                BuildStatePayload(
                    evt.ProjectId,
                    evt.SessionId,
                    evt.OldState,
                    evt.NewState,
                    evt.ErrorMessage,
                    evt.Timestamp,
                    isSnapshot: false,
                    startedAt: null,
                    stoppedAt: null))
        };

        if (string.Equals(evt.NewState, "Faulted", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add(new(
                "faulted",
                BuildFaultPayload(
                    evt.ProjectId,
                    evt.SessionId,
                    evt.ErrorMessage,
                    evt.Timestamp)));
        }

        return messages;
    }

    private static object BuildStatePayload(
        Guid projectId,
        Guid sessionId,
        string? oldState,
        string newState,
        string? errorMessage,
        DateTimeOffset timestamp,
        bool isSnapshot,
        DateTime? startedAt,
        DateTime? stoppedAt)
    {
        return new
        {
            projectId,
            sessionId,
            oldState,
            newState,
            errorMessage,
            timestamp,
            isSnapshot,
            startedAt,
            stoppedAt
        };
    }

    private static object BuildFaultPayload(
        Guid projectId,
        Guid sessionId,
        string? errorMessage,
        DateTimeOffset timestamp)
    {
        return new
        {
            projectId,
            sessionId,
            errorMessage,
            timestamp
        };
    }
}
