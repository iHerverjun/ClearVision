// IProjectFlowStorage.cs
// 加载流程 JSON 字符串
// 作者：蘅芜君

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 负责存储项目流程数据的接口
/// </summary>
public interface IProjectFlowStorage
{
    /// <summary>
    /// 保存流程 JSON 字符串
    /// </summary>
    Task SaveFlowJsonAsync(Guid projectId, string flowJson);

    /// <summary>
    /// 加载流程 JSON 字符串
    /// </summary>
    Task<string?> LoadFlowJsonAsync(Guid projectId);
}
