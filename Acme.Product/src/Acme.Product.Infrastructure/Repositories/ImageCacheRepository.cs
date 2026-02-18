// ImageCacheRepository.cs
// 图像缓存仓储实现（内存缓存）
// 作者：蘅芜君

using Acme.Product.Core.Interfaces;

namespace Acme.Product.Infrastructure.Repositories;

/// <summary>
/// 图像缓存仓储实现（内存缓存）
/// </summary>
public class ImageCacheRepository : IImageCacheRepository
{
    private readonly Dictionary<Guid, CacheEntry> _cache = new();
    private readonly object _lock = new();

    private class CacheEntry
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string Format { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public Task<Guid> AddAsync(byte[] imageData, string format)
    {
        var id = Guid.NewGuid();

        lock (_lock)
        {
            _cache[id] = new CacheEntry
            {
                Data = imageData,
                Format = format,
                CreatedAt = DateTime.UtcNow
            };
        }

        return Task.FromResult(id);
    }

    public Task<byte[]?> GetAsync(Guid id)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(id, out var entry))
            {
                return Task.FromResult<byte[]?>(entry.Data);
            }
            return Task.FromResult<byte[]?>(null);
        }
    }

    public Task DeleteAsync(Guid id)
    {
        lock (_lock)
        {
            _cache.Remove(id);
        }
        return Task.CompletedTask;
    }

    public Task CleanExpiredAsync(TimeSpan expiration)
    {
        var cutoff = DateTime.UtcNow - expiration;

        lock (_lock)
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.CreatedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }
        }

        return Task.CompletedTask;
    }
}
