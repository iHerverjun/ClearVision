using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;

namespace Acme.Product.Application.Services;

/// <summary>
/// 认证服务实现 - 内存Token管理
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    
    // 内存Token存储 - 应用重启后需重新登录
    private static readonly Dictionary<string, UserSession> _sessions = new();
    private static readonly object _lock = new();

    // Token 有效期：8小时
    private static readonly TimeSpan _tokenExpiration = TimeSpan.FromHours(8);

    public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// 登录
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Fail("用户名和密码不能为空");
        }

        // 查找用户
        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
        {
            return AuthResult.Fail("用户名或密码错误");
        }

        // 检查是否启用
        if (!user.IsActive)
        {
            return AuthResult.Fail("账户已被禁用");
        }

        // 验证密码
        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return AuthResult.Fail("用户名或密码错误");
        }

        // 更新最后登录时间
        user.UpdateLastLogin();
        await _userRepository.UpdateAsync(user);

        // 生成Token
        var token = GenerateToken();
        var session = new UserSession
        {
            UserId = user.Id.ToString(),
            Username = user.Username,
            Role = user.Role.ToString(),
            ExpiresAt = DateTime.UtcNow.Add(_tokenExpiration)
        };

        lock (_lock)
        {
            _sessions[token] = session;
        }

        // 返回结果
        var userDto = MapToDto(user);
        return AuthResult.Ok(token, userDto);
    }

    /// <summary>
    /// 登出
    /// </summary>
    public Task LogoutAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            return Task.CompletedTask;

        lock (_lock)
        {
            _sessions.Remove(token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 验证Token是否有效
    /// </summary>
    public Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            return Task.FromResult(false);

        lock (_lock)
        {
            if (_sessions.TryGetValue(token, out var session))
            {
                // 检查是否过期
                if (session.IsExpired)
                {
                    _sessions.Remove(token);
                    return Task.FromResult(false);
                }
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 获取当前会话用户
    /// </summary>
    public Task<UserSession?> GetSessionAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            return Task.FromResult<UserSession?>(null);

        lock (_lock)
        {
            if (_sessions.TryGetValue(token, out var session))
            {
                if (session.IsExpired)
                {
                    _sessions.Remove(token);
                    return Task.FromResult<UserSession?>(null);
                }
                return Task.FromResult<UserSession?>(session);
            }
            return Task.FromResult<UserSession?>(null);
        }
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    public async Task<AuthResult> ChangePasswordAsync(string userId, string oldPassword, string newPassword)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return AuthResult.Fail("无效的用户ID");
        }

        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return AuthResult.Fail("密码不能为空");
        }

        if (newPassword.Length < 6)
        {
            return AuthResult.Fail("新密码长度至少为6位");
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return AuthResult.Fail("用户不存在");
        }

        // 验证旧密码
        if (!_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
        {
            return AuthResult.Fail("当前密码错误");
        }

        // 更新密码
        var newHash = _passwordHasher.HashPassword(newPassword);
        user.ChangePassword(newHash);
        await _userRepository.UpdateAsync(user);

        return AuthResult.Ok(string.Empty, MapToDto(user));
    }

    /// <summary>
    /// 生成Token
    /// </summary>
    private static string GenerateToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 映射到DTO
    /// </summary>
    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id.ToString(),
            Username = user.Username,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt
        };
    }
}
