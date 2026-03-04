// IAiConnector.cs
// AI 连接器接口
// 定义 AI 模型连接器统一调用契约
// 作者：蘅芜君
using Acme.Product.Contracts.Messages;

namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Unified connector contract for the AI generation pipeline.
/// </summary>
public interface IAiConnector
{
    Task<AiCompletionResult> CompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        CancellationToken cancellationToken = default);

    Task<AiCompletionResult> StreamCompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        Action<AiStreamChunk> onChunk,
        CancellationToken cancellationToken = default);
}
