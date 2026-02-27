using System.Text.Json;
using Acme.Product.Contracts.Messages;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// 处理前端发来的 GenerateFlow 消息
/// </summary>
public class GenerateFlowMessageHandler
{
    private readonly IAiFlowGenerationService _generationService;
    private readonly Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
        IReadOnlyList<string>? attachments = null,
        Action<string, string>? onMessage = null, // "GenerateFlowProgress", "GenerateFlowStreamChunk", "GenerateFlowAttachmentReport"
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("收到 AI 生成请求：{Description}", description);

        try
        {
            // 推送开始消息
            onMessage?.Invoke("GenerateFlowProgress",
                JsonSerializer.Serialize(new { message = "正在连接 AI 服务...", phase = "connecting" }, _jsonOptions));

            var result = await _generationService.GenerateFlowAsync(
                new AiFlowGenerationRequest(description, hint, sessionId, existingFlowJson, attachments),
                progressMsg => onMessage?.Invoke("GenerateFlowProgress",
                    JsonSerializer.Serialize(new { message = progressMsg }, _jsonOptions)),
                chunk => onMessage?.Invoke("GenerateFlowStreamChunk",
                    JsonSerializer.Serialize(new GenerateFlowStreamChunk { ChunkType = chunk.ChunkType, Content = chunk.Content }, _jsonOptions)),
                cancellationToken,
                attachmentReport => onMessage?.Invoke("GenerateFlowAttachmentReport",
                    JsonSerializer.Serialize(attachmentReport, _jsonOptions)));

            var response = new GenerateFlowResponse
            {
                Success = result.Success,
                Flow = result.Flow,
                ErrorMessage = result.ErrorMessage,
                AiExplanation = result.AiExplanation,
                Reasoning = result.Reasoning,
                ParametersNeedingReview = result.ParametersNeedingReview,
                SessionId = result.SessionId,
                DetectedIntent = result.DetectedIntent
            };

            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 AI 生成请求时发生未预期错误");

            var errorResponse = new GenerateFlowResponse
            {
                Success = false,
                ErrorMessage = $"服务内部错误：{ex.Message}"
            };

            return JsonSerializer.Serialize(errorResponse, _jsonOptions);
        }
    }
}
