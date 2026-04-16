using System.Linq.Expressions;
using Acme.Product.Core.Entities;

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// User repository contract.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Gets a user by username.
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active users.
    /// </summary>
    Task<IEnumerable<User>> GetAllActiveUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the username already exists.
    /// </summary>
    Task<bool> IsUsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the system already has any non-deleted user.
    /// </summary>
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default);
}
