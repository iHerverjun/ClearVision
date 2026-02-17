using System.Collections.Concurrent;

namespace Acme.Product.Core.Services;

/// <summary>
/// 全局变量上下文 - 支持算子间共享变量，跨周期累计
/// 【第三优先级】变量表/全局上下文功能
/// </summary>
public interface IVariableContext
{
    /// <summary>
    /// 获取变量值
    /// </summary>
    T? GetValue<T>(string variableName, T? defaultValue = default);
    
    /// <summary>
    /// 设置变量值
    /// </summary>
    void SetValue<T>(string variableName, T value);
    
    /// <summary>
    /// 递增变量（用于计数器）
    /// </summary>
    long Increment(string variableName, long delta = 1);
    
    /// <summary>
    /// 删除变量
    /// </summary>
    bool Remove(string variableName);
    
    /// <summary>
    /// 检查变量是否存在
    /// </summary>
    bool Contains(string variableName);
    
    /// <summary>
    /// 获取所有变量名称
    /// </summary>
    IEnumerable<string> GetVariableNames();
    
    /// <summary>
    /// 清空所有变量
    /// </summary>
    void Clear();
    
    /// <summary>
    /// 获取循环计数器
    /// </summary>
    long CycleCount { get; }
    
    /// <summary>
    /// 递增循环计数器
    /// </summary>
    void IncrementCycleCount();
    
    /// <summary>
    /// 重置循环计数器
    /// </summary>
    void ResetCycleCount();
}

/// <summary>
/// 变量上下文实现 - 线程安全的全局变量存储
/// </summary>
public class VariableContext : IVariableContext
{
    private readonly ConcurrentDictionary<string, object> _variables = new();
    private long _cycleCount = 0;
    
    public long CycleCount => Interlocked.Read(ref _cycleCount);
    
    public T? GetValue<T>(string variableName, T? defaultValue = default)
    {
        if (_variables.TryGetValue(variableName, out var value))
        {
            try
            {
                if (value is T t) return t;
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
    
    public void SetValue<T>(string variableName, T value)
    {
        _variables[variableName] = value!;
    }
    
    public long Increment(string variableName, long delta = 1)
    {
        return _variables.AddOrUpdate(variableName, delta, (_, existingValue) =>
        {
            var current = existingValue switch
            {
                long l => l,
                int i => i,
                double d => (long)d,
                _ => 0L
            };
            return current + delta;
        }) switch
        {
            long l => l,
            int i => i,
            _ => 0L
        };
    }
    
    public bool Remove(string variableName)
    {
        return _variables.TryRemove(variableName, out _);
    }
    
    public bool Contains(string variableName)
    {
        return _variables.ContainsKey(variableName);
    }
    
    public IEnumerable<string> GetVariableNames()
    {
        return _variables.Keys.ToList();
    }
    
    public void Clear()
    {
        _variables.Clear();
        Interlocked.Exchange(ref _cycleCount, 0);
    }
    
    public void IncrementCycleCount()
    {
        Interlocked.Increment(ref _cycleCount);
    }
    
    public void ResetCycleCount()
    {
        Interlocked.Exchange(ref _cycleCount, 0);
    }
}
