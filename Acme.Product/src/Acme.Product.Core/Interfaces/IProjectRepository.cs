// IProjectRepository.cs
// 更新工程流程
// 作者：蘅芜君

using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 工程仓储接口
/// </summary>
public interface IProjectRepository : IRepository<Project>
{
    /// <summary>
    /// 根据名称查找工程
    /// </summary>
    Task<Project?> GetByNameAsync(string name);

    /// <summary>
    /// 获取最近打开的工程列表
    /// </summary>
    Task<IEnumerable<Project>> GetRecentlyOpenedAsync(int count = 10);

    /// <summary>
    /// 搜索工程
    /// </summary>
    Task<IEnumerable<Project>> SearchAsync(string keyword);

    /// <summary>
    /// 获取工程及其流程
    /// </summary>
    Task<Project?> GetWithFlowAsync(Guid id);

    /// <summary>
    /// 更新工程流程
    /// </summary>
    Task UpdateFlowAsync(Project project);
}
