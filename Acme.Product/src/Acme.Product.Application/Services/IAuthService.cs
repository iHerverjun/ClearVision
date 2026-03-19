// IAuthService.cs
// 是否已过期
// 作者：蘅芜君

using Acme.Product.Application.DTOs;

namespace Acme.Product.Application.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 登录
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// 登出
    /// </summary>
    Task LogoutAsync(string token);

    /// <summary>
    /// 验证Token是否有效
    /// </summary>
    Task<bool> ValidateTokenAsync(string token);

    /// <summary>
    /// 获取当前会话用户
    /// </summary>
    Task<UserSession?> GetSessionAsync(string token);

    /// <summary>
    /// 修改密码
    /// </summary>
    Task<AuthResult> ChangePasswordAsync(string userId, string oldPassword, string newPassword);
}

/// <summary>
/// 认证结果
/// </summary>
public class AuthResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 访问令牌
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// 用户信息
    /// </summary>
    public UserDto? User { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static AuthResult Ok(string token, UserDto user) => new()
    {
        Success = true,
        Token = token,
        User = user
    };

    public static AuthResult Fail(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// 用户会话信息
/// </summary>
public class UserSession
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 用户角色
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool IsExpired => IsExpiredAt(DateTime.UtcNow);

    public bool IsExpiredAt(DateTime utcNow) => utcNow > ExpiresAt;
}
