using System.Diagnostics;
using System.Reflection;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;

namespace Acme.Product.Application.Services;

public interface ISystemStatsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    Task<HardwareStatusDto> GetHardwareStatusAsync(CancellationToken cancellationToken = default);
    Task<List<ActivityLogDto>> GetRecentActivitiesAsync(int count = 10, CancellationToken cancellationToken = default);
    Task<string> GetAppVersionAsync(CancellationToken cancellationToken = default);
}

public sealed class DashboardStatsDto
{
    public double AverageYield { get; set; }
    public double AverageCycleTimeMs { get; set; }
    public double StorageUsedGb { get; set; }
    public int TotalInspections { get; set; }
    public int OkCount { get; set; }
    public int NgCount { get; set; }
}

public sealed class HardwareStatusDto
{
    public double CpuUsage { get; set; }
    public double MemoryUsedGb { get; set; }
    public double MemoryTotalGb { get; set; }
    public bool IsBridgeConnected { get; set; }
    public string CameraStatus { get; set; } = "unknown";
    public int RunningInspections { get; set; }
}

public sealed class ActivityLogDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "Unknown";
    public string? UserName { get; set; }
}

public sealed class SystemStatsService : ISystemStatsService
{
    private readonly IInspectionResultRepository _inspectionResultRepository;
    private readonly object _cpuLock = new();
    private TimeSpan _lastCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
    private DateTime _lastCpuSampleAt = DateTime.UtcNow;

    public SystemStatsService(IInspectionResultRepository inspectionResultRepository)
    {
        _inspectionResultRepository = inspectionResultRepository;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddHours(-24);

        var results = await _inspectionResultRepository.FindAsync(result =>
            !result.IsDeleted &&
            result.InspectionTime >= windowStart &&
            result.InspectionTime <= windowEnd);

        var resultList = results.ToList();
        var total = resultList.Count;
        var okCount = resultList.Count(result => result.Status == InspectionStatus.OK);
        var ngCount = resultList.Count(result => result.Status == InspectionStatus.NG);
        var averageCycleTime = total > 0 ? resultList.Average(result => (double)result.ProcessingTimeMs) : 0;
        var yieldRate = total > 0 ? (double)okCount / total * 100 : 0;

        return new DashboardStatsDto
        {
            AverageYield = Math.Round(yieldRate, 1),
            AverageCycleTimeMs = Math.Round(averageCycleTime, 1),
            StorageUsedGb = Math.Round(GetDatabaseSizeInGb(), 2),
            TotalInspections = total,
            OkCount = okCount,
            NgCount = ngCount
        };
    }

    public Task<HardwareStatusDto> GetHardwareStatusAsync(CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var memoryUsedGb = process.WorkingSet64 / 1024d / 1024d / 1024d;

        double memoryTotalGb;
        try
        {
            memoryTotalGb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024d / 1024d / 1024d;
        }
        catch
        {
            memoryTotalGb = 0;
        }

        var status = new HardwareStatusDto
        {
            CpuUsage = Math.Round(ReadCpuUsage(process), 1),
            MemoryUsedGb = Math.Round(memoryUsedGb, 2),
            MemoryTotalGb = Math.Round(memoryTotalGb, 2),
            IsBridgeConnected = true,
            CameraStatus = "connected",
            RunningInspections = 0
        };

        return Task.FromResult(status);
    }

    public async Task<List<ActivityLogDto>> GetRecentActivitiesAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var sampleSize = Math.Max(count, 50);
        var recentResults = await _inspectionResultRepository.FindAsync(result => !result.IsDeleted);

        return recentResults
            .OrderByDescending(result => result.InspectionTime)
            .Take(sampleSize)
            .Take(count)
            .Select(result => new ActivityLogDto
            {
                Id = result.Id.ToString("N"),
                Title = $"Inspection {result.Status}",
                Description = $"Result {result.Id}",
                Timestamp = result.InspectionTime,
                Type = "InspectionCompleted",
                UserName = "system"
            })
            .ToList();
    }

    public Task<string> GetAppVersionAsync(CancellationToken cancellationToken = default)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? typeof(SystemStatsService).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        return Task.FromResult(version);
    }

    private double GetDatabaseSizeInGb()
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDirectory, "vision.db"),
                Path.Combine(baseDirectory, "..", "..", "..", "vision.db"),
            };

            var dbFile = candidates
                .Select(Path.GetFullPath)
                .FirstOrDefault(File.Exists);

            if (dbFile == null)
            {
                return 0;
            }

            var fileSize = new FileInfo(dbFile).Length;
            return fileSize / 1024d / 1024d / 1024d;
        }
        catch
        {
            return 0;
        }
    }

    private double ReadCpuUsage(Process process)
    {
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var currentCpu = process.TotalProcessorTime;
            var elapsedMs = (now - _lastCpuSampleAt).TotalMilliseconds;

            if (elapsedMs <= 0)
            {
                return 0;
            }

            var cpuMs = (currentCpu - _lastCpuTime).TotalMilliseconds;
            _lastCpuTime = currentCpu;
            _lastCpuSampleAt = now;

            var usage = cpuMs / (Environment.ProcessorCount * elapsedMs) * 100;
            return Math.Clamp(usage, 0, 100);
        }
    }
}
