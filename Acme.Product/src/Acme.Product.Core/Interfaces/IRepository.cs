// IRepository.cs
// 是否存在
// 作者：蘅芜君

using System.Linq.Expressions;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 仓储接口基类
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// 根据ID获取实体
    /// </summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// 根据条件查询
    /// </summary>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// 添加实体
    /// </summary>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// 更新实体
    /// </summary>
    Task UpdateAsync(T entity);

    /// <summary>
    /// 删除实体
    /// </summary>
    Task DeleteAsync(T entity);

    /// <summary>
    /// 根据ID删除
    /// </summary>
    Task DeleteByIdAsync(Guid id);

    /// <summary>
    /// 是否存在
    /// </summary>
    Task<bool> ExistsAsync(Guid id);
}
