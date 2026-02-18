// User.cs
// 更新显示名称
// 作者：蘅芜君

using Acme.Product.Core.Entities.Base;
using Acme.Product.Core.Enums;

namespace Acme.Product.Core.Entities;

/// <summary>
/// 用户实体 - 系统用户管理
/// </summary>
public class User : Entity
{
    /// <summary>
    /// 登录用户名
    /// </summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// 密码哈希（BCrypt）
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// 用户角色
    /// </summary>
    public UserRole Role { get; private set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    // 私有构造函数，用于EF Core
    private User()
    {
    }

    /// <summary>
    /// 创建新用户
    /// </summary>
    public static User Create(string username, string passwordHash, string displayName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("用户名不能为空", nameof(username));
        
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("密码哈希不能为空", nameof(passwordHash));

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = username;

        return new User
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            PasswordHash = passwordHash,
            DisplayName = displayName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 更新最后登录时间
    /// </summary>
    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        MarkAsModified();
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("新密码哈希不能为空", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        MarkAsModified();
    }

    /// <summary>
    /// 禁用账户
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        MarkAsModified();
    }

    /// <summary>
    /// 启用账户
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        MarkAsModified();
    }

    /// <summary>
    /// 修改角色
    /// </summary>
    public void ChangeRole(UserRole newRole)
    {
        Role = newRole;
        MarkAsModified();
    }

    /// <summary>
    /// 更新显示名称
    /// </summary>
    public void UpdateDisplayName(string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            throw new ArgumentException("显示名称不能为空", nameof(newDisplayName));

        DisplayName = newDisplayName.Trim();
        MarkAsModified();
    }
}
