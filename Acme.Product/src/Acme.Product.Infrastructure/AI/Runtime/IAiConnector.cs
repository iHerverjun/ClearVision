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

