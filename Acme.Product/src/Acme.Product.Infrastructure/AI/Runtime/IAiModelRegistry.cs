namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Read-only registry abstraction for model profiles used by the unified runtime pipeline.
/// </summary>
public interface IAiModelRegistry
{
    AiModelConfig GetActiveModel();
}

