// LruImageCacheRepository.cs
// 缓存统计信息
// 作者：蘅芜君

using Acme.Product.Core.Interfaces;

namespace Acme.Product.Infrastructure.Repositories;

/// <summary>
/// LRU (Least Recently Used) 图像缓存仓储实现
/// Sprint 5: S5-006 实现
/// </summary>
public class LruImageCacheRepository : IImageCacheRepository
{
    private readonly Dictionary<Guid, CacheEntry> _cache = new();
    private readonly LinkedList<Guid> _accessOrder = new();
    private readonly object _lock = new();
    private readonly long _maxSizeInBytes;
    private long _currentSizeInBytes;
    
    private long _hitCount;
    private long _missCount;

    public LruImageCacheRepository(long maxSizeInBytes = 100 * 1024 * 1024) // 默认100MB
    {
        _maxSizeInBytes = maxSizeInBytes;
    }

    /// <summary>
    /// 添加图像到缓存
    /// </summary>
    public Task<Guid> AddAsync(byte[] imageData, string format)
    {
        var id = Guid.NewGuid();
        var size = imageData.Length;

        // 检查单体大小是否超过最大缓存限制
        if (size > _maxSizeInBytes)
        {
            throw new ArgumentException($"图像大小 {size} 超过最大缓存限制 {_maxSizeInBytes}");
        }

        lock (_lock)
        {
            // 检查是否需要淘汰
            while (_currentSizeInBytes + size > _maxSizeInBytes && _accessOrder.Count > 0)
            {
                EvictLeastRecentlyUsed();
            }

            var entry = new CacheEntry
            {
                Id = id,
                Data = imageData,
                Format = format,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                SizeInBytes = size
            };

            _cache[id] = entry;
            _accessOrder.AddFirst(id);
            _currentSizeInBytes += size;
        }

        return Task.FromResult(id);
    }

    /// <summary>
    /// 从缓存获取图像
    /// </summary>
    public Task<byte[]?> GetAsync(Guid id)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(id, out var entry))
            {
                // 更新访问时间
                entry.LastAccessedAt = DateTime.UtcNow;
                
                // 移动到链表头部（最近使用）
                _accessOrder.Remove(id);
                _accessOrder.AddFirst(id);
                
                Interlocked.Increment(ref _hitCount);
                return Task.FromResult<byte[]?>(entry.Data);
            }
            
            Interlocked.Increment(ref _missCount);
            return Task.FromResult<byte[]?>(null);
        }
    }

    /// <summary>
    /// 删除缓存项
    /// </summary>
    public Task DeleteAsync(Guid id)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(id, out var entry))
            {
                _cache.Remove(id);
                _accessOrder.Remove(id);
                _currentSizeInBytes -= entry.SizeInBytes;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 清理过期缓存项
    /// </summary>
    public Task CleanExpiredAsync(TimeSpan expiration)
    {
        var cutoff = DateTime.UtcNow - expiration;

        lock (_lock)
        {
            var expiredIds = _cache
                .Where(kvp => kvp.Value.LastAccessedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in expiredIds)
            {
                if (_cache.TryGetValue(id, out var entry))
                {
                    _cache.Remove(id);
                    _accessOrder.Remove(id);
                    _currentSizeInBytes -= entry.SizeInBytes;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            var hits = Interlocked.Read(ref _hitCount);
            var misses = Interlocked.Read(ref _missCount);
            var total = hits + misses;
            
            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                CurrentSizeInBytes = _currentSizeInBytes,
                MaxSizeInBytes = _maxSizeInBytes,
                HitCount = hits,
                MissCount = misses,
                HitRate = total > 0 ? (double)hits / total : 0
            };
        }
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _accessOrder.Clear();
            _currentSizeInBytes = 0;
        }
    }

    /// <summary>
    /// 淘汰最近最少使用的项
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        if (_accessOrder.Count == 0) return;
        
        var lruId = _accessOrder.Last!.Value;
        if (_cache.TryGetValue(lruId, out var entry))
        {
            _cache.Remove(lruId);
            _accessOrder.RemoveLast();
            _currentSizeInBytes -= entry.SizeInBytes;
        }
    }

    /// <summary>
    /// 缓存项
    /// </summary>
    private class CacheEntry
    {
        public Guid Id { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string Format { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public long SizeInBytes { get; set; }
    }
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public long CurrentSizeInBytes { get; set; }
    public long MaxSizeInBytes { get; set; }
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public double HitRate { get; set; }
    
    public double CurrentSizeInMB => CurrentSizeInBytes / (1024.0 * 1024.0);
    public double MaxSizeInMB => MaxSizeInBytes / (1024.0 * 1024.0);
}
