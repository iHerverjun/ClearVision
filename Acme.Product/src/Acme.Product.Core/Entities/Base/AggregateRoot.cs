// AggregateRoot.cs
// 聚合根基类 - 继承自 Entity 并实现 IAggregateRoot
// 作者：蘅芜君

using Acme.Product.Core.Entities.Base;

namespace Acme.Product.Core.Entities.Base;

/// <summary>
/// 聚合根基类 - 继承自 Entity 并实现 IAggregateRoot
/// </summary>
public abstract class AggregateRoot : Entity, IAggregateRoot
{
    private readonly List<Events.IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<Events.IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(Events.IDomainEvent eventItem)
    {
        _domainEvents.Add(eventItem);
    }

    public void RemoveDomainEvent(Events.IDomainEvent eventItem)
    {
        _domainEvents.Remove(eventItem);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
