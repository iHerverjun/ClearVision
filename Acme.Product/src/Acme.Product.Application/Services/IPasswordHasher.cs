// IPasswordHasher.cs
// 验证密码
// 作者：蘅芜君

namespace Acme.Product.Application.Services;

/// <summary>
/// 密码哈希服务接口
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 哈希密码
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// 验证密码
    /// </summary>
    bool VerifyPassword(string password, string hash);
}
