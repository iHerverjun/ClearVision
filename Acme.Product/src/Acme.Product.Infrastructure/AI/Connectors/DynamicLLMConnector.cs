// DynamicLLMConnector.cs
// 动态 LLM 连接器
// 按配置动态路由并调用不同大模型提供方
// 作者：蘅芜君
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Acme.Product.Infrastructure.AI.Connectors;

public class DynamicLLMConnector : ILLMConnector
{
    private readonly ILLMConfigurationStore _configStore;
    private readonly LLMConnectorFactory _factory;

    public DynamicLLMConnector(ILLMConfigurationStore configStore, LLMConnectorFactory factory)
    {
        _configStore = configStore;
        _factory = factory;
    }

    public async Task<LLMResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetActiveProfileAsync();
        if (config == null)
            throw new InvalidOperationException("未配置激活的 LLM 供应商。请配置后重试。");

        var connector = _factory.Create(config);

        return await connector.GenerateAsync(prompt, cancellationToken);
    }

    public async IAsyncEnumerable<LLMStreamChunk> GenerateStreamAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetActiveProfileAsync();
        if (config == null)
            throw new InvalidOperationException("未配置激活的 LLM 供应商。请配置后重试。");

        var connector = _factory.Create(config);
        await foreach (var chunk in connector.GenerateStreamAsync(prompt, cancellationToken))
        {
            yield return chunk;
        }
    }
}
