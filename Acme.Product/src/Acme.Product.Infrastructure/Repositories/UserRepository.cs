// UserRepository.cs
// 检查用户名是否已存在
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Acme.Product.Infrastructure.Repositories;

/// <summary>
/// 用户仓储实现
/// </summary>
public class UserRepository : RepositoryBase<User>, IUserRepository
{
    public UserRepository(Data.VisionDbContext context) : base(context)
    {
    }

    /// <summary>
    /// 根据用户名获取用户
    /// </summary>
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// 获取所有启用的用户
    /// </summary>
    public async Task<IEnumerable<User>> GetAllActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(u => u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 检查用户名是否已存在
    /// </summary>
    public async Task<bool> IsUsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(u => u.Username == username && !u.IsDeleted, cancellationToken);
    }
}
