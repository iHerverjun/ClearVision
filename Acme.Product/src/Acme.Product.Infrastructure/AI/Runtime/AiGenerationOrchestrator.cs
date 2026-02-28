using Acme.Product.Contracts.Messages;

namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Unified runtime orchestrator used by generation services.
/// Stage A keeps policy minimal and forwards calls to the selected connector.
/// </summary>
public sealed class AiGenerationOrchestrator
{
    private readonly IAiModelSelector _modelSelector;
    private readonly IAiConnectorFactory _connectorFactory;

    public AiGenerationOrchestrator(
        IAiModelSelector modelSelector,
        IAiConnectorFactory connectorFactory)
    {
        _modelSelector = modelSelector;
        _connectorFactory = connectorFactory;
    }

    public AiModelConfig ResolveGenerationModel()
    {
        return _modelSelector.SelectGenerationModel();
    }

    public AiModelCapabilities ResolveCapabilities(AiModelConfig? modelConfig = null)
    {
        var model = modelConfig ?? ResolveGenerationModel();
        return model.GetEffectiveCapabilities();
    }

    public bool SupportsVisionInput(AiModelConfig? modelConfig = null)
    {
        return ResolveCapabilities(modelConfig).SupportsVisionInput;
    }

    public Task<AiCompletionResult> CompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        AiModelConfig? modelConfig = null,
        CancellationToken cancellationToken = default)
    {
        var model = modelConfig ?? ResolveGenerationModel();
        var connector = _connectorFactory.CreateConnector(model);
        return connector.CompleteAsync(systemPrompt, messages, cancellationToken);
    }

    public Task<AiCompletionResult> StreamCompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        Action<AiStreamChunk> onChunk,
        AiModelConfig? modelConfig = null,
        CancellationToken cancellationToken = default)
    {
        var model = modelConfig ?? ResolveGenerationModel();
        var connector = _connectorFactory.CreateConnector(model);
        return connector.StreamCompleteAsync(systemPrompt, messages, onChunk, cancellationToken);
    }
}
