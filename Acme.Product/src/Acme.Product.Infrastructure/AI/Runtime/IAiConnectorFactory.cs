namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Creates a connector instance for a specific model profile.
/// </summary>
public interface IAiConnectorFactory
{
    IAiConnector CreateConnector(AiModelConfig modelConfig);
}

