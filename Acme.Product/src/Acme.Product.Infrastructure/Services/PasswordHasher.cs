// PasswordHasher.cs
// 验证密码
// 作者：蘅芜君

using Acme.Product.Application.Services;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 密码哈希服务 - 使用 BCrypt
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// 哈希密码
    /// </summary>    
    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("密码不能为空", nameof(password));

        // 使用 BCrypt 哈希，工作因子 12
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (string.IsNullOrWhiteSpace(hash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
