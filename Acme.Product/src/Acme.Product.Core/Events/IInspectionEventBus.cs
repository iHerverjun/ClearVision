// IInspectionEventBus.cs
// 事件总线接口
// 作者：架构修复方案 v2

namespace Acme.Product.Core.Events;

/// <summary>
/// 检测事件总线
/// 职责：发布和订阅检测相关事件
/// </summary>
public interface IInspectionEventBus
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="ct">取消令牌</param>
    Task PublishAsync<T>(T eventData, CancellationToken ct = default) where T : IInspectionEvent;

    /// <summary>
    /// 订阅精确类型事件
    /// </summary>
    /// <typeparam name="T">事件类型</typeparam>
    /// <param name="handler">处理器</param>
    /// <returns>订阅句柄（Dispose 取消订阅）</returns>
    IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IInspectionEvent;

    /// <summary>
    /// 订阅接口类型事件（可接收所有实现类型）
    /// </summary>
    /// <typeparam name="TInterface">接口类型</typeparam>
    /// <param name="handler">处理器</param>
    /// <returns>订阅句柄（Dispose 取消订阅）</returns>
    IDisposable SubscribeInterface<TInterface>(Func<TInterface, CancellationToken, Task> handler) 
        where TInterface : class, IInspectionEvent;
}
