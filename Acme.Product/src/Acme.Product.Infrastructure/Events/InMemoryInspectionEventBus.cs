// InMemoryInspectionEventBus.cs
// 内存事件总线实现
// 作者：架构修复方案 v2

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Acme.Product.Core.Events;
using Microsoft.Extensions.Logging;

namespace Acme.Product.Infrastructure.Events;

/// <summary>
/// 内存事件总线实现
/// 特性：
/// 1. 线程安全：ConcurrentDictionary + ImmutableList
/// 2. 支持精确类型订阅
/// 3. 支持接口订阅（可接收所有实现类型）
/// 4. 异常隔离：单个 handler 失败不影响其他 handler
/// </summary>
public class InMemoryInspectionEventBus : IInspectionEventBus, IDisposable
{
    private readonly ILogger<InMemoryInspectionEventBus> _logger;
    private readonly IEventStore _eventStore;
    
    // 精确类型订阅者存储
    private readonly ConcurrentDictionary<Type, ImmutableList<Delegate>> _handlers = new();
    
    // 接口订阅者存储（使用包装器避免类型转换问题）
    private readonly ConcurrentDictionary<Type, ImmutableList<Func<object, CancellationToken, Task>>> 
        _interfaceHandlers = new();

    public InMemoryInspectionEventBus(
        ILogger<InMemoryInspectionEventBus> logger,
        IEventStore eventStore)
    {
        _logger = logger;
        _eventStore = eventStore;
    }

    public async Task PublishAsync<T>(T eventData, CancellationToken ct = default) where T : IInspectionEvent
    {
        var type = typeof(T);
        var exceptions = new List<Exception>();
        var sequenceId = _eventStore.Append(eventData.ProjectId, eventData);
        
        _logger.LogDebug("[EventBus] 发布事件: {EventType}, Project: {ProjectId}", 
            type.Name, eventData.ProjectId);
        _logger.LogTrace("[EventBus] 事件序列号: {SequenceId}", sequenceId);

        // 1. 精确类型订阅者（异常隔离）
        if (_handlers.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    var typedHandler = (Func<T, CancellationToken, Task>)handler;
                    await typedHandler(eventData, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[EventBus] 事件处理器失败: {HandlerType}", handler.GetType().Name);
                    exceptions.Add(ex);
                    // 继续执行后续 handler
                }
            }
        }

        // 2. 接口订阅者（异常隔离）
        foreach (var (ifaceType, ifaceHandlers) in _interfaceHandlers)
        {
            if (ifaceType.IsAssignableFrom(type))
            {
                foreach (var handler in ifaceHandlers)
                {
                    try
                    {
                        await handler(eventData, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[EventBus] 接口事件处理器失败: {InterfaceType}", ifaceType.Name);
                        exceptions.Add(ex);
                    }
                }
            }
        }

        // 如有异常，抛 AggregateException
        if (exceptions.Count > 0)
        {
            throw new AggregateException("一个或多个事件处理器失败", exceptions);
        }
    }

    public IDisposable Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : IInspectionEvent
    {
        var type = typeof(T);
        
        _handlers.AddOrUpdate(
            type,
            _ => ImmutableList.Create<Delegate>(handler),
            (_, list) => list.Add(handler));

        _logger.LogDebug("[EventBus] 订阅事件: {EventType}", type.Name);

        return new Subscription(() =>
        {
            _handlers.AddOrUpdate(
                type,
                _ => ImmutableList<Delegate>.Empty,
                (_, list) => list.Remove(handler));
            _logger.LogDebug("[EventBus] 取消订阅: {EventType}", type.Name);
        });
    }

    public IDisposable SubscribeInterface<TInterface>(Func<TInterface, CancellationToken, Task> handler) 
        where TInterface : class, IInspectionEvent
    {
        var type = typeof(TInterface);
        
        // 关键修正：创建包装器委托，避免运行时类型转换失败
        Func<object, CancellationToken, Task> wrapper = async (obj, ct) =>
        {
            if (obj is TInterface typedObj)
            {
                await handler(typedObj, ct);
            }
            else
            {
                _logger.LogWarning("[EventBus] 事件类型不匹配，期望 {Expected}，实际 {Actual}", 
                    typeof(TInterface).Name, obj.GetType().Name);
            }
        };

        _interfaceHandlers.AddOrUpdate(
            type,
            _ => ImmutableList.Create(wrapper),
            (_, list) => list.Add(wrapper));

        _logger.LogDebug("[EventBus] 订阅接口事件: {InterfaceType}", type.Name);

        return new Subscription(() =>
        {
            _interfaceHandlers.AddOrUpdate(
                type,
                _ => ImmutableList<Func<object, CancellationToken, Task>>.Empty,
                (_, list) => list.Remove(wrapper));
            _logger.LogDebug("[EventBus] 取消接口订阅: {InterfaceType}", type.Name);
        });
    }

    public void Dispose()
    {
        _handlers.Clear();
        _interfaceHandlers.Clear();
    }

    /// <summary>
    /// 订阅句柄实现
    /// </summary>
    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _unsubscribe();
                _disposed = true;
            }
        }
    }
}
