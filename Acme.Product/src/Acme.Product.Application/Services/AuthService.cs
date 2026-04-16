using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Product.Application.Services;

/// <summary>
/// Auth service backed by in-memory sessions.
/// </summary>
public class AuthService : IAuthService
{
    private const int UsernameMinLength = 3;
    public const string InitialAdminSetupAlreadyCompletedMessage = "系统已完成初始化，请直接登录";
    private const string InitialAdminPasswordMismatchMessage = "两次输入的密码不一致";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<AuthService> _logger;

    private static readonly Dictionary<string, UserSession> _sessions = new();
    private static readonly Dictionary<string, LoginFailureState> _loginFailures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();
    private static readonly SemaphoreSlim _initialAdminSetupGate = new(1, 1);
    private static readonly TimeSpan _lockoutDuration = TimeSpan.FromMinutes(15);

    public Func<DateTime> UtcNowProvider { get; set; } = static () => DateTime.UtcNow;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IConfigurationService configurationService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configurationService = configurationService;
        _logger = logger;
    }

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IConfigurationService configurationService)
        : this(userRepository, passwordHasher, configurationService, NullLogger<AuthService>.Instance)
    {
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Fail("用户名和密码不能为空");
        }

        var normalizedUsername = username.Trim();
        var utcNow = UtcNowProvider();

        var user = await _userRepository.GetByUsernameAsync(normalizedUsername);
        if (user == null)
        {
            return AuthResult.Fail("用户名或密码错误");
        }

        if (!user.IsActive)
        {
            return AuthResult.Fail("账户已被禁用");
        }

        if (TryGetActiveLockout(normalizedUsername, utcNow, out var lockedUntilUtc))
        {
            _logger.LogWarning("[AuthService] Login rejected because account is locked: {Username}", normalizedUsername);
            return AuthResult.Fail(BuildLockoutMessage(lockedUntilUtc));
        }

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            var threshold = ResolveLoginFailureLockoutCount();
            var failureState = RegisterFailedAttempt(normalizedUsername, utcNow, threshold);
            if (failureState.IsLockedAt(utcNow))
            {
                _logger.LogWarning("[AuthService] Login locked after failures: {Username}", normalizedUsername);
                return AuthResult.Fail(BuildLockoutMessage(failureState.LockedUntilUtc));
            }

            _logger.LogInformation("[AuthService] Invalid login credentials: {Username}", normalizedUsername);
            return AuthResult.Fail("用户名或密码错误");
        }

        ClearFailedAttempts(normalizedUsername);

        user.UpdateLastLogin();
        await _userRepository.UpdateAsync(user);

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

        _logger.LogInformation("[AuthService] Login success: {Username}", normalizedUsername);
        return AuthResult.Ok(token, MapToDto(user));
    }

    public Task LogoutAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogInformation("[AuthService] Logout requested without token.");
            return Task.CompletedTask;
        }

        var removed = false;
        lock (_lock)
        {
            removed = _sessions.Remove(token);
        }

        _logger.LogInformation("[AuthService] Logout complete: {Result}", removed ? "removed" : "not-found");
        return Task.CompletedTask;
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult(false);
        }

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
        }

        return Task.FromResult(false);
    }

    public Task<UserSession?> GetSessionAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return Task.FromResult<UserSession?>(null);
        }

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
        }

        return Task.FromResult<UserSession?>(null);
    }

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

        var passwordPolicyError = ValidatePasswordPolicy(newPassword, oldPassword);
        if (!string.IsNullOrEmpty(passwordPolicyError))
        {
            return AuthResult.Fail(passwordPolicyError);
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            return AuthResult.Fail("用户不存在");
        }

        if (!_passwordHasher.VerifyPassword(oldPassword, user.PasswordHash))
        {
            _logger.LogInformation("[AuthService] Change password rejected due to invalid current password: {UserId}", userId);
            return AuthResult.Fail("当前密码错误");
        }

        var newHash = _passwordHasher.HashPassword(newPassword);
        user.ChangePassword(newHash);
        await _userRepository.UpdateAsync(user);
        _logger.LogInformation("[AuthService] Password changed: {UserId}", userId);

        return AuthResult.Ok(string.Empty, MapToDto(user));
    }

    public async Task<InitialAdminSetupStatusResponse> GetInitialAdminSetupStatusAsync()
    {
        return new InitialAdminSetupStatusResponse
        {
            RequiresInitialAdminSetup = !await _userRepository.HasAnyUsersAsync(),
            UsernameMinLength = UsernameMinLength,
            PasswordMinLength = ResolvePasswordMinLength(),
            RequiresUppercase = false,
            RequiresLowercase = false,
            RequiresDigit = false
        };
    }

    public async Task<AuthResult> SetupInitialAdminAsync(InitialAdminSetupRequest request)
    {
        if (request == null)
        {
            return AuthResult.Fail("初始化请求不能为空");
        }

        var usernameError = ValidateUsername(request.Username);
        if (!string.IsNullOrEmpty(usernameError))
        {
            return AuthResult.Fail(usernameError);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return AuthResult.Fail("密码不能为空");
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return AuthResult.Fail(InitialAdminPasswordMismatchMessage);
        }

        var passwordPolicyError = ValidatePasswordLength(request.Password);
        if (!string.IsNullOrEmpty(passwordPolicyError))
        {
            return AuthResult.Fail(passwordPolicyError);
        }

        var normalizedUsername = request.Username.Trim();

        await _initialAdminSetupGate.WaitAsync();
        try
        {
            if (await _userRepository.HasAnyUsersAsync())
            {
                return AuthResult.Fail(InitialAdminSetupAlreadyCompletedMessage);
            }

            if (await _userRepository.IsUsernameExistsAsync(normalizedUsername))
            {
                return AuthResult.Fail($"用户名 '{normalizedUsername}' 已存在");
            }

            var adminUser = User.Create(
                normalizedUsername,
                _passwordHasher.HashPassword(request.Password),
                normalizedUsername,
                UserRole.Admin);

            await _userRepository.AddAsync(adminUser);
            _logger.LogInformation("[AuthService] Initial admin created: {Username}", normalizedUsername);
        }
        finally
        {
            _initialAdminSetupGate.Release();
        }

        return await LoginAsync(normalizedUsername, request.Password);
    }

    public static void ResetInMemoryStateForTests()
    {
        lock (_lock)
        {
            _sessions.Clear();
            _loginFailures.Clear();
        }
    }

    private static string GenerateToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    private TimeSpan ResolveSessionTimeout()
    {
        var minutes = Math.Max(1, _configurationService.GetCurrent()?.Security?.SessionTimeoutMinutes ?? 30);
        return TimeSpan.FromMinutes(minutes);
    }

    private int ResolvePasswordMinLength()
    {
        return Math.Max(6, _configurationService.GetCurrent()?.Security?.PasswordMinLength ?? 6);
    }

    private int ResolveLoginFailureLockoutCount()
    {
        return Math.Max(1, _configurationService.GetCurrent()?.Security?.LoginFailureLockoutCount ?? 5);
    }

    private string? ValidatePasswordPolicy(string newPassword, string oldPassword)
    {
        var lengthError = ValidatePasswordLength(newPassword);
        if (!string.IsNullOrEmpty(lengthError))
        {
            return lengthError;
        }

        if (string.Equals(newPassword, oldPassword, StringComparison.Ordinal))
        {
            return "新密码不能与当前密码相同";
        }

        return null;
    }

    private string? ValidatePasswordLength(string password)
    {
        var minPasswordLength = ResolvePasswordMinLength();
        if (password.Trim().Length < minPasswordLength)
        {
            return $"新密码长度不能少于 {minPasswordLength} 位";
        }

        return null;
    }

    private static string? ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "用户名不能为空";
        }

        if (username.Trim().Length < UsernameMinLength)
        {
            return $"用户名长度至少为 {UsernameMinLength} 位";
        }

        return null;
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
