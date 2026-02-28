namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Selects model profiles for different runtime intents.
/// Stage A keeps selection strategy simple: active model only.
/// </summary>
public interface IAiModelSelector
{
    AiModelConfig SelectGenerationModel();
}

