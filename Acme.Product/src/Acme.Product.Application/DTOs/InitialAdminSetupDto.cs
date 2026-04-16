namespace Acme.Product.Application.DTOs;

/// <summary>
/// First-run admin setup status response.
/// </summary>
public class InitialAdminSetupStatusResponse
{
    public bool RequiresInitialAdminSetup { get; set; }

    public int UsernameMinLength { get; set; } = 3;

    public int PasswordMinLength { get; set; }

    public bool RequiresUppercase { get; set; }

    public bool RequiresLowercase { get; set; }

    public bool RequiresDigit { get; set; }
}

/// <summary>
/// First-run admin setup request.
/// </summary>
public class InitialAdminSetupRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
