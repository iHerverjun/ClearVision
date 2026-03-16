using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Acme.Product.Infrastructure.Diagnostics;

/// <summary>
/// 性能分析结果实体
/// 记录单个操作的统计数据
/// </summary>
public class ProfilerResult
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalMs { get; set; }
    public double MinMs { get; set; } = double.MaxValue;
    public double MaxMs { get; set; } = double.MinValue;
    public double AverageMs => Count > 0 ? TotalMs / Count : 0;
    
    // 用于计算标准差 (简单的平方和记录)
    public double SumOfSquaresMs { get; set; }
    
    /// <summary>
    /// 标准差 (Standard Deviation)
    /// </summary>
    public double StdDevMs
    {
        get
        {
            if (Count <= 1) return 0;
            double variance = (SumOfSquaresMs - (TotalMs * TotalMs / Count)) / Count;
            return variance > 0 ? Math.Sqrt(variance) : 0;
        }
    }
}

/// <summary>
/// 性能分析工具
/// 实现了 IDisposable 接口，配合 using 语句可以自动记录代码块耗时
/// </summary>
public class PerformanceProfiler : IDisposable
{
    private static readonly ConcurrentDictionary<string, ProfilerResult> _results = new(StringComparer.OrdinalIgnoreCase);
    
    private readonly string _name;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    /// <summary>
    /// 初始化新的性能分析器实例
    /// </summary>
    /// <param name="name">被分析的操作名称 (如算子名)</param>
    public PerformanceProfiler(string name)
    {
        _name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _stopwatch.Stop();
            double elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
            RecordResult(_name, elapsedMs);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 记录单次执行的时间
    /// </summary>
    public static void RecordResult(string name, double elapsedMs)
    {
        _results.AddOrUpdate(name,
            key => new ProfilerResult
            {
                Name = key,
                Count = 1,
                TotalMs = elapsedMs,
                MinMs = elapsedMs,
                MaxMs = elapsedMs,
                SumOfSquaresMs = elapsedMs * elapsedMs
            },
            (key, existing) =>
            {
                lock (existing)
                {
                    existing.Count++;
                    existing.TotalMs += elapsedMs;
                    existing.SumOfSquaresMs += elapsedMs * elapsedMs;
                    if (elapsedMs < existing.MinMs) existing.MinMs = elapsedMs;
                    if (elapsedMs > existing.MaxMs) existing.MaxMs = elapsedMs;
                }
                return existing;
            });
    }

    /// <summary>
    /// 获取当前的性能分析报告数据
    /// </summary>
    public static IEnumerable<ProfilerResult> GetResults()
    {
        return _results.Values.OrderByDescending(r => r.TotalMs);
    }

    /// <summary>
    /// 重置统计数据
    /// </summary>
    public static void Reset()
    {
        _results.Clear();
    }

    /// <summary>
    /// 生成 JSON 格式的性能报告
    /// </summary>
    public static string GenerateReportJson()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(GetResults(), options);
    }

    /// <summary>
    /// 生成 CSV 格式的性能报告
    /// </summary>
    public static string GenerateReportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("OperatorName,Count,TotalMs,AverageMs,MinMs,MaxMs,StdDevMs");
        
        foreach (var r in GetResults())
        {
            sb.AppendLine($"{r.Name},{r.Count},{r.TotalMs:F3},{r.AverageMs:F3},{r.MinMs:F3},{r.MaxMs:F3},{r.StdDevMs:F3}");
        }
        
        return sb.ToString();
    }
}
