using Acme.Product.Application.DTOs;

namespace Acme.Product.Application.Services;

/// <summary>
/// Authentication service contract.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);

    Task LogoutAsync(string token);

    Task<bool> ValidateTokenAsync(string token);

    Task<UserSession?> GetSessionAsync(string token);

    Task<AuthResult> ChangePasswordAsync(string userId, string oldPassword, string newPassword);

    Task<InitialAdminSetupStatusResponse> GetInitialAdminSetupStatusAsync();

    Task<AuthResult> SetupInitialAdminAsync(InitialAdminSetupRequest request);
}

/// <summary>
/// Authentication result.
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }

    public string? Token { get; set; }

    public UserDto? User { get; set; }

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
/// User session information.
/// </summary>
public class UserSession
{
    public string UserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsExpired => IsExpiredAt(DateTime.UtcNow);

    public bool IsExpiredAt(DateTime utcNow) => utcNow > ExpiresAt;
}
