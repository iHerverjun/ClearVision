// InspectionResultBackgroundService.cs
// 异步处理检测结果保存的后台服务
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// 检测结果异步通道写入器接口
/// </summary>
public interface IInspectionResultChannelWriter
{
    /// <summary>
    /// 尝试将检测结果写入后台队列
    /// </summary>
    bool TryWrite(InspectionResult result);
}

/// <summary>
/// 基于 Channel 的检测结果缓冲服务与后台保存任务
/// 解决了检测结果实时保存阻塞前端核心检测线程的问题
/// </summary>
public class InspectionResultBackgroundService : BackgroundService, IInspectionResultChannelWriter
{
    private readonly Channel<InspectionResult> _channel;
    private readonly ILogger<InspectionResultBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public InspectionResultBackgroundService(
        ILogger<InspectionResultBackgroundService> logger, 
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // 创建一个有界容量的通道，防止内存无限胀大。当超过容量时，丢弃最旧的数据（可选）或阻塞/拒绝。
        // 由于有界通道满时直接 Drop 最旧/最新会导致数据丢失记录，这里我们选择配置为 DropOldest
        // 以确保系统能继续运行（尽管极限情况下丢失落盘记录，但保障了UI与实时引擎不卡死）。
        _channel = Channel.CreateBounded<InspectionResult>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true, // 本类唯一消耗
            SingleWriter = false // 多个检测流可能同时写入
        });
    }

    public bool TryWrite(InspectionResult result)
    {
        var written = _channel.Writer.TryWrite(result);
        if (!written)
        {
            _logger.LogWarning("检测结果后台保存队列已满，可能有记录被丢弃或未能成功入队: {Id}", result.Id);
        }
        return written;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("检测结果异步落盘后台服务已启动。");

        // 批处理缓冲
        var batch = new List<InspectionResult>(50);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 等待有数据可用
                if (await _channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    // 尽最大努力读取出批量，最多合并 50 个结果
                    while (batch.Count < 50 && _channel.Reader.TryRead(out var result))
                    {
                        batch.Add(result);
                    }

                    if (batch.Count > 0)
                    {
                        // 落盘
                        await SaveBatchAsync(batch, stoppingToken);
                        batch.Clear();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 停止请求
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理检测异步保存时发生异常。");
            }
        }
        
        // 优雅退出前清空队列
        if (_channel.Reader.TryRead(out var lastResult))
        {
            batch.Add(lastResult);
             while (batch.Count < 50 && _channel.Reader.TryRead(out var result))
             {
                 batch.Add(result);
             }
             if (batch.Count > 0)
             {
                 await SaveBatchAsync(batch, CancellationToken.None);
             }
        }
    }

    private async Task SaveBatchAsync(List<InspectionResult> results, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IInspectionResultRepository>();

            foreach (var result in results)
            {
                await repo.AddAsync(result);
            }
            
            // 注意：因为我们使用了 DbContext 的 SaveChangesAsync 或者类似的底层接口
            // 我们最好做一次性能优化的 bulk insert，但 IRepository.AddAsync 通常会执行 Add + SaveChanges
            // 如果底层仓储对每一次 AddAsync 都做 SaveChangesAsync，批处理就不怎么管用了。
            // 目前不修改仓储接口，直接轮询写入，已解除主线程阻塞。未来可引入 AddRangeAsync()。
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "异步落盘时出现错误，批次大小: {Count}", results.Count);
        }
    }
}
