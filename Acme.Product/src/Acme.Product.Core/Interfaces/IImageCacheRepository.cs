// IImageCacheRepository.cs
// 清理过期缓存
// 作者：蘅芜君

namespace Acme.Product.Core.Interfaces;

/// <summary>
/// 图像缓存仓储接口
/// </summary>
public interface IImageCacheRepository
{
    /// <summary>
    /// 添加图像到缓存
    /// </summary>
    Task<Guid> AddAsync(byte[] imageData, string format);

    /// <summary>
    /// 获取图像
    /// </summary>
    Task<byte[]?> GetAsync(Guid id);

    /// <summary>
    /// 删除图像
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// 清理过期缓存
    /// </summary>
    Task CleanExpiredAsync(TimeSpan expiration);
}
