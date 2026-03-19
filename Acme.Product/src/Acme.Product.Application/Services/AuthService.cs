// AuthService.cs
// 映射到DTO
// 作者：蘅芜君
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
    private readonly IConfigurationService _configurationService;

    // 内存Token存储 - 应用重启后需重新登录
    private static readonly Dictionary<string, UserSession> _sessions = new();
    private static readonly Dictionary<string, LoginFailureState> _loginFailures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    // 当前配置模型未提供单独的锁定时长，暂用固定窗口实现“临时锁定”。
    private static readonly TimeSpan _lockoutDuration = TimeSpan.FromMinutes(15);

    public Func<DateTime> UtcNowProvider { get; set; } = static () => DateTime.UtcNow;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IConfigurationService configurationService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configurationService = configurationService;
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

        var normalizedUsername = username.Trim();
        var utcNow = UtcNowProvider();

        // 查找用户
        var user = await _userRepository.GetByUsernameAsync(normalizedUsername);
        if (user == null)
        {
            return AuthResult.Fail("用户名或密码错误");
        }

        // 检查是否启用
        if (!user.IsActive)
        {
            return AuthResult.Fail("账户已被禁用");
        }

        if (TryGetActiveLockout(normalizedUsername, utcNow, out var lockedUntilUtc))
        {
            return AuthResult.Fail(BuildLockoutMessage(lockedUntilUtc));
        }

        // 验证密码
        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            var threshold = ResolveLoginFailureLockoutCount();
            var failureState = RegisterFailedAttempt(normalizedUsername, utcNow, threshold);
            if (failureState.IsLockedAt(utcNow))
            {
                return AuthResult.Fail(BuildLockoutMessage(failureState.LockedUntilUtc));
            }

            return AuthResult.Fail("用户名或密码错误");
        }

        ClearFailedAttempts(normalizedUsername);

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
            ExpiresAt = utcNow.Add(ResolveSessionTimeout())
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

        var utcNow = UtcNowProvider();

        lock (_lock)
        {
            if (_sessions.TryGetValue(token, out var session))
            {
                if (session.IsExpiredAt(utcNow))
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

        var utcNow = UtcNowProvider();

        lock (_lock)
        {
            if (_sessions.TryGetValue(token, out var session))
            {
                if (session.IsExpiredAt(utcNow))
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

    public static void ResetInMemoryStateForTests()
    {
        lock (_lock)
        {
            _sessions.Clear();
            _loginFailures.Clear();
        }
    }

    /// <summary>
    /// 生成Token
    /// </summary>
    private static string GenerateToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    private TimeSpan ResolveSessionTimeout()
    {
        var minutes = Math.Max(1, _configurationService.GetCurrent()?.Security?.SessionTimeoutMinutes ?? 30);
        return TimeSpan.FromMinutes(minutes);
    }

    private int ResolveLoginFailureLockoutCount()
    {
        return Math.Max(1, _configurationService.GetCurrent()?.Security?.LoginFailureLockoutCount ?? 5);
    }

    private static LoginFailureState RegisterFailedAttempt(string username, DateTime utcNow, int threshold)
    {
        lock (_lock)
        {
            if (!_loginFailures.TryGetValue(username, out var state))
            {
                state = new LoginFailureState();
            }

            if (state.LockedUntilUtc > utcNow)
            {
                _loginFailures[username] = state;
                return state;
            }

            if (state.LockedUntilUtc != DateTime.MinValue && state.LockedUntilUtc <= utcNow)
            {
                state = new LoginFailureState();
            }

            state.FailureCount += 1;
            if (state.FailureCount >= threshold)
            {
                state.LockedUntilUtc = utcNow.Add(_lockoutDuration);
                state.FailureCount = 0;
            }

            _loginFailures[username] = state;
            return state;
        }
    }

    private static void ClearFailedAttempts(string username)
    {
        lock (_lock)
        {
            _loginFailures.Remove(username);
        }
    }

    private static bool TryGetActiveLockout(string username, DateTime utcNow, out DateTime lockedUntilUtc)
    {
        lock (_lock)
        {
            if (_loginFailures.TryGetValue(username, out var state))
            {
                if (state.IsLockedAt(utcNow))
                {
                    lockedUntilUtc = state.LockedUntilUtc;
                    return true;
                }

                if (state.LockedUntilUtc != DateTime.MinValue && state.LockedUntilUtc <= utcNow)
                {
                    _loginFailures.Remove(username);
                }
            }
        }

        lockedUntilUtc = DateTime.MinValue;
        return false;
    }

    private static string BuildLockoutMessage(DateTime lockedUntilUtc)
    {
        return $"账号已被临时锁定，请在 {lockedUntilUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} 后重试";
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

    private sealed class LoginFailureState
    {
        public int FailureCount { get; set; }

        public DateTime LockedUntilUtc { get; set; }

        public bool IsLockedAt(DateTime utcNow) => LockedUntilUtc > utcNow;
    }
}
