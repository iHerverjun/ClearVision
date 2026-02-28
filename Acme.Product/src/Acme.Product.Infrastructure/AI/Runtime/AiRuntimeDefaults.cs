using Acme.Product.Contracts.Messages;

namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Stage A connector adapter that routes requests to the existing AiApiClient.
/// This keeps behavior unchanged while switching the call site to the unified abstraction.
/// </summary>
public sealed class AiApiClientAdapterConnector : IAiConnector
{
    private readonly AiApiClient _apiClient;
    private readonly AiGenerationOptions _options;

    public AiApiClientAdapterConnector(AiApiClient apiClient, AiModelConfig modelConfig)
    {
        _apiClient = apiClient;
        _options = modelConfig.ToGenerationOptions();
    }

    public Task<AiCompletionResult> CompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.CompleteAsync(systemPrompt, messages, _options, cancellationToken);
    }

    public Task<AiCompletionResult> StreamCompleteAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        Action<AiStreamChunk> onChunk,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.StreamCompleteAsync(systemPrompt, messages, onChunk, _options, cancellationToken);
    }
}

/// <summary>
/// Stage A default connector factory.
/// </summary>
public sealed class AiConnectorFactory : IAiConnectorFactory
{
    private readonly AiApiClient _apiClient;

    public AiConnectorFactory(AiApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public IAiConnector CreateConnector(AiModelConfig modelConfig)
    {
        return new AiApiClientAdapterConnector(_apiClient, modelConfig);
    }
}

/// <summary>
/// Stage A model registry adapter on top of AiConfigStore.
/// </summary>
public sealed class AiModelRegistry : IAiModelRegistry
{
    private readonly AiConfigStore _configStore;

    public AiModelRegistry(AiConfigStore configStore)
    {
        _configStore = configStore;
    }

    public AiModelConfig GetActiveModel()
    {
        var allModels = _configStore.GetAll();
        var active = allModels.FirstOrDefault(x => x.IsActive) ?? allModels.FirstOrDefault();
        if (active == null)
            throw new InvalidOperationException("No available AI model configuration.");

        return active;
    }
}

/// <summary>
/// Stage A selector: always use active profile.
/// </summary>
public sealed class ActiveAiModelSelector : IAiModelSelector
{
    private readonly IAiModelRegistry _registry;

    public ActiveAiModelSelector(IAiModelRegistry registry)
    {
        _registry = registry;
    }

    public AiModelConfig SelectGenerationModel()
    {
        return _registry.GetActiveModel();
    }
}

