// SseHeartbeatService.cs
// SSE 心跳服务
// 职责：定期发布心跳事件，保持 SSE 连接活跃
// 作者：架构修复方案 v2

using Acme.Product.Core.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Services;

/// <summary>
/// SSE 心跳后台服务
/// 每 30 秒发布一次心跳事件，防止 NAT/代理断开连接
/// </summary>
public class SseHeartbeatService : BackgroundService
{
    private readonly IInspectionEventBus _eventBus;
    private readonly ILogger<SseHeartbeatService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    public SseHeartbeatService(
        IInspectionEventBus eventBus,
        ILogger<SseHeartbeatService> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[HeartbeatService] 心跳服务已启动，间隔: {Interval}s", _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                
                if (stoppingToken.IsCancellationRequested) break;

                // 发布心跳事件
                await _eventBus.PublishAsync(new HeartbeatEvent(), stoppingToken);
                
                _logger.LogDebug("[HeartbeatService] 心跳已发送");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HeartbeatService] 心跳发送失败");
            }
        }

        _logger.LogInformation("[HeartbeatService] 心跳服务已停止");
    }
}
