// IUserRepository.cs
// 检查用户名是否已存在
// 作者：蘅芜君

using System.Linq.Expressions;
using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 用户仓储接口
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用户实体，不存在则返回null</returns>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有启用的用户
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>启用状态的用户列表</returns>
    Task<IEnumerable<User>> GetAllActiveUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查用户名是否已存在
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> IsUsernameExistsAsync(string username, CancellationToken cancellationToken = default);
}
