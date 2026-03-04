// IAiConnectorFactory.cs
// AI 连接器工厂接口
// 定义连接器创建与解析的统一工厂契约
// 作者：蘅芜君
namespace Acme.Product.Infrastructure.AI.Runtime;

/// <summary>
/// Creates a connector instance for a specific model profile.
/// </summary>
public interface IAiConnectorFactory
{
    IAiConnector CreateConnector(AiModelConfig modelConfig);
}
