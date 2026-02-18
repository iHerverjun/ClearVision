// IAggregateRoot.cs
// 清空所有领域事件
// 作者：蘅芜君

namespace Acme.Product.Core.Entities.Base;

/// <summary>
/// 聚合根标记接口 - 标识领域聚合根
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// 领域事件集合
    /// </summary>
    IReadOnlyCollection<Events.IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// 添加领域事件
    /// </summary>
    void AddDomainEvent(Events.IDomainEvent eventItem);

    /// <summary>
    /// 移除领域事件
    /// </summary>
    void RemoveDomainEvent(Events.IDomainEvent eventItem);

    /// <summary>
    /// 清空所有领域事件
    /// </summary>
    void ClearDomainEvents();
}
