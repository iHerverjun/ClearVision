// Entity.cs
// 恢复删除
// 作者：蘅芜君

namespace Acme.Product.Core.Entities.Base;

/// <summary>
/// 实体基类 - 所有领域实体的基础
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// 实体唯一标识
    /// </summary>
    public Guid Id { get; protected set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime? ModifiedAt { get; protected set; }

    /// <summary>
    /// 是否已删除（软删除）
    /// </summary>
    public bool IsDeleted { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// 更新修改时间
    /// </summary>
    public void MarkAsModified()
    {
        ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 软删除
    /// </summary>
    public void MarkAsDeleted()
    {
        IsDeleted = true;
        MarkAsModified();
    }

    /// <summary>
    /// 恢复删除
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        MarkAsModified();
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
