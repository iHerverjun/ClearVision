using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.Product.Contracts.Messages;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Handles GenerateFlow requests from the desktop bridge.
/// </summary>
public class GenerateFlowMessageHandler
{
    private readonly IAiFlowGenerationService _generationService;
    private readonly Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public GenerateFlowMessageHandler(
        IAiFlowGenerationService generationService,
        Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler> logger)
    {
        _generationService = generationService;
        _logger = logger;
    }

    public async Task<string> HandleAsync(
        string description,
        string? sessionId = null,
        string? existingFlowJson = null,
        string? hint = null,
        GenerateFlowMode mode = GenerateFlowMode.Auto,
        bool debugPrompt = false,
        string? requestId = null,
        IReadOnlyList<string>? attachments = null,
        Action<string, string>? onMessage = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received AI generate-flow request. Description={Description}", description);

        try
        {
            onMessage?.Invoke(
                "GenerateFlowProgress",
                JsonSerializer.Serialize(new
                {
                    message = "正在连接 AI 服务...",
                    phase = "connecting",
                    requestId
                }, _jsonOptions));

            var result = await _generationService.GenerateFlowAsync(
                new AiFlowGenerationRequest(
                    Description: description,
                    AdditionalContext: hint,
                    SessionId: sessionId,
                    ExistingFlowJson: existingFlowJson,
                    Attachments: attachments,
                    Mode: mode,
                    DebugPrompt: debugPrompt),
                progressMsg => onMessage?.Invoke(
                    "GenerateFlowProgress",
                    JsonSerializer.Serialize(new
                    {
                        message = progressMsg,
                        requestId
                    }, _jsonOptions)),
                chunk => onMessage?.Invoke(
                    "GenerateFlowStreamChunk",
                    JsonSerializer.Serialize(new GenerateFlowStreamChunk
                    {
                        ChunkType = chunk.ChunkType,
                        Content = chunk.Content,
                        RequestId = requestId
                    }, _jsonOptions)),
                cancellationToken,
                attachmentReport => onMessage?.Invoke(
                    "GenerateFlowAttachmentReport",
                    JsonSerializer.Serialize(attachmentReport with { RequestId = requestId }, _jsonOptions)));

            var response = new GenerateFlowResponse
            {
                Success = result.Success,
                Status = NormalizeStatus(result.CompletionStatus, result.Success),
                Flow = result.Flow,
                ErrorMessage = result.ErrorMessage,
                FailureSummary = BuildFailureSummaryText(result.FailureSummary, result.ErrorMessage),
                LastAttemptDiagnostics = result.LastAttemptDiagnostics,
                AiExplanation = result.AiExplanation,
                Reasoning = result.Reasoning,
                ParametersNeedingReview = result.ParametersNeedingReview,
                SessionId = result.SessionId ?? sessionId,
                RequestId = requestId,
                DetectedIntent = result.DetectedIntent,
                DryRunResult = result.DryRunResult,
                RecommendedTemplate = MapRecommendedTemplate(result.RecommendedTemplate),
                PendingParameters = MapPendingParameters(result.PendingParameters),
                MissingResources = MapMissingResources(result.MissingResources),
                ManualRetry = MapManualRetry(result.ManualRetry),
                PromptTrace = result.PromptTrace
            };

            return SerializeResponse(response, result.FailureType);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("AI generation request was cancelled by the user. SessionId={SessionId}", sessionId);

            var cancelledResponse = new GenerateFlowResponse
            {
                Success = false,
                Status = AiFlowGenerationResult.CompletionStatusCancelled,
                ErrorMessage = "用户已取消本次生成。",
                FailureSummary = "用户已取消本次生成。",
                LastAttemptDiagnostics = Array.Empty<AiAttemptDiagnostic>(),
                SessionId = sessionId,
                RequestId = requestId
            };

            return SerializeResponse(cancelledResponse, AiFlowGenerationResult.FailureTypeUserCancelled);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                "AI generation request timed out. SessionId={SessionId}. Error={Error}",
                sessionId ?? string.Empty,
                ex.Message);

            var timeoutResponse = new GenerateFlowResponse
            {
                Success = false,
                Status = AiFlowGenerationResult.CompletionStatusTimedOut,
                ErrorMessage = "AI generation timed out. Please retry.",
                FailureSummary = "AI generation timed out. Please retry.",
                LastAttemptDiagnostics = Array.Empty<AiAttemptDiagnostic>(),
                SessionId = sessionId,
                RequestId = requestId
            };

            return SerializeResponse(timeoutResponse, AiFlowGenerationResult.FailureTypeTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while handling AI generate-flow request");

            var errorResponse = new GenerateFlowResponse
            {
                Success = false,
                Status = AiFlowGenerationResult.CompletionStatusFailed,
                ErrorMessage = $"服务内部错误：{ex.Message}",
                FailureSummary = $"服务内部错误：{ex.Message}",
                LastAttemptDiagnostics = Array.Empty<AiAttemptDiagnostic>(),
                SessionId = sessionId,
                RequestId = requestId
            };

            return SerializeResponse(errorResponse, AiFlowGenerationResult.FailureTypeSystemError);
        }
    }

    private static string SerializeResponse(GenerateFlowResponse response, string? failureType)
    {
        return JsonSerializer.Serialize(new
            {
                response.Type,
                response.Success,
                response.Status,
            response.Flow,
            response.ErrorMessage,
            response.FailureSummary,
            response.LastAttemptDiagnostics,
            response.AiExplanation,
            response.Reasoning,
            response.ParametersNeedingReview,
            response.SessionId,
            response.RequestId,
            response.DetectedIntent,
            response.DryRunResult,
                response.RecommendedTemplate,
                response.PendingParameters,
                response.MissingResources,
                response.ManualRetry,
                response.PromptTrace,
                FailureType = failureType
            }, _jsonOptions);
    }

    private static string NormalizeStatus(string? completionStatus, bool success)
    {
        if (!string.IsNullOrWhiteSpace(completionStatus))
        {
            return completionStatus;
        }

        return success
            ? AiFlowGenerationResult.CompletionStatusCompleted
            : AiFlowGenerationResult.CompletionStatusFailed;
    }

    private static string? BuildFailureSummaryText(AiFailureSummary? failureSummary, string? fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(failureSummary?.Message))
        {
            return failureSummary.Message;
        }

        return string.IsNullOrWhiteSpace(fallbackMessage)
            ? null
            : fallbackMessage;
    }

    private static GenerateFlowTemplateRecommendation? MapRecommendedTemplate(AiRecommendedTemplateInfo? template)
    {
        if (template == null)
        {
            return null;
        }

        return new GenerateFlowTemplateRecommendation
        {
            TemplateId = template.TemplateId,
            TemplateName = template.TemplateName,
            MatchReason = template.MatchReason,
            MatchMode = template.MatchMode,
            Confidence = template.Confidence
        };
    }

    private static List<GenerateFlowPendingParameter> MapPendingParameters(IReadOnlyCollection<AiPendingParameterInfo>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return new List<GenerateFlowPendingParameter>();
        }

        return parameters.Select(item => new GenerateFlowPendingParameter
        {
            OperatorId = item.OperatorId,
            ActualOperatorId = item.ActualOperatorId,
            ParameterNames = item.ParameterNames?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>()
        }).ToList();
    }

    private static List<GenerateFlowMissingResource> MapMissingResources(IReadOnlyCollection<AiMissingResourceInfo>? resources)
    {
        if (resources == null || resources.Count == 0)
        {
            return new List<GenerateFlowMissingResource>();
        }

        return resources.Select(item => new GenerateFlowMissingResource
        {
            ResourceType = item.ResourceType,
            ResourceKey = item.ResourceKey,
            Description = item.Description
        }).ToList();
    }

    private static GenerateFlowManualRetry? MapManualRetry(AiManualRetryInfo? manualRetry)
    {
        if (manualRetry == null)
        {
            return null;
        }

        return new GenerateFlowManualRetry
        {
            Required = manualRetry.Required,
            Stage = manualRetry.Stage,
            Draft = manualRetry.Draft,
            Summary = manualRetry.Summary,
            RepairTarget = manualRetry.RepairTarget,
            LastOutputSummary = manualRetry.LastOutputSummary,
            Diagnostics = manualRetry.Diagnostics.Cast<object>().ToList()
        };
    }
}
