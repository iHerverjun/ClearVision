// IDomainEvent.cs
// 领域事件基类，提供默认实现。
// 作者：蘅芜君

namespace Acme.Product.Core.Events;

/// <summary>
/// 领域事件接口定义。
/// 基于《代码实践指导》中的领域事件设计模式。
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// 事件唯一标识。
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// 事件发生时间。
    /// </summary>
    DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// 触发事件的聚合根 ID。
    /// </summary>
    Guid AggregateId { get; }
}

/// <summary>
/// 领域事件基类，提供默认实现。
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public abstract Guid AggregateId { get; }
}
