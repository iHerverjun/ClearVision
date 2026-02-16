using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;

namespace Acme.Product.Application.Services;

/// <summary>
/// 用户管理服务 - 仅Admin可调用
/// </summary>
public class UserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserManagementService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// 获取所有用户（包括已禁用）
    /// </summary>
    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToDto);
    }

    /// <summary>
    /// 获取所有启用的用户
    /// </summary>
    public async Task<IEnumerable<UserDto>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllActiveUsersAsync(cancellationToken);
        return users.Select(MapToDto);
    }

    /// <summary>
    /// 根据ID获取用户
    /// </summary>
    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        if (!Guid.TryParse(id, out var userId))
            return null;

        var user = await _userRepository.GetByIdAsync(userId);
        return user == null ? null : MapToDto(user);
    }

    /// <summary>
    /// 创建用户
    /// </summary>
    public async Task<UserResult> CreateUserAsync(CreateUserRequest request)
    {
        // 验证请求
        if (string.IsNullOrWhiteSpace(request.Username))
            return UserResult.Fail("用户名不能为空");

        if (request.Username.Length < 3)
            return UserResult.Fail("用户名长度至少为3位");

        if (string.IsNullOrWhiteSpace(request.Password))
            return UserResult.Fail("密码不能为空");

        if (request.Password.Length < 6)
            return UserResult.Fail("密码长度至少为6位");

        // 检查用户名是否已存在
        if (await _userRepository.IsUsernameExistsAsync(request.Username))
            return UserResult.Fail($"用户名 '{request.Username}' 已存在");

        // 哈希密码
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // 创建用户
        var user = User.Create(
            request.Username.Trim(),
            passwordHash,
            request.DisplayName?.Trim() ?? request.Username.Trim(),
            request.Role
        );

        await _userRepository.AddAsync(user);

        return UserResult.Ok(MapToDto(user));
    }

    /// <summary>
    /// 更新用户
    /// </summary>
    public async Task<UserResult> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        if (!Guid.TryParse(id, out var userId))
            return UserResult.Fail("无效的用户ID");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return UserResult.Fail("用户不存在");

        // 更新显示名称
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.UpdateDisplayName(request.DisplayName.Trim());
        }

        // 更新角色
        user.ChangeRole(request.Role);

        // 更新启用状态
        if (request.IsActive != user.IsActive)
        {
            if (request.IsActive)
                user.Activate();
            else
                user.Deactivate();
        }

        await _userRepository.UpdateAsync(user);

        return UserResult.Ok(MapToDto(user));
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    public async Task<UserResult> DeleteUserAsync(string id)
    {
        if (!Guid.TryParse(id, out var userId))
            return UserResult.Fail("无效的用户ID");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return UserResult.Fail("用户不存在");

        await _userRepository.DeleteAsync(user);

        return UserResult.Ok(MapToDto(user));
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    public async Task<UserResult> ResetPasswordAsync(string id, string newPassword)
    {
        if (!Guid.TryParse(id, out var userId))
            return UserResult.Fail("无效的用户ID");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return UserResult.Fail("密码长度至少为6位");

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return UserResult.Fail("用户不存在");

        var newHash = _passwordHasher.HashPassword(newPassword);
        user.ChangePassword(newHash);
        await _userRepository.UpdateAsync(user);

        return UserResult.Ok(MapToDto(user));
    }

    /// <summary>
    /// 检查用户名是否可用
    /// </summary>
    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        return !await _userRepository.IsUsernameExistsAsync(username);
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

/// <summary>
/// 用户操作结果
/// </summary>
public class UserResult
{
    public bool Success { get; set; }
    public UserDto? User { get; set; }
    public string? ErrorMessage { get; set; }

    public static UserResult Ok(UserDto user) => new() { Success = true, User = user };
    public static UserResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// 创建用户请求
/// </summary>
public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.Operator;
}

/// <summary>
/// 更新用户请求
/// </summary>
public class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
