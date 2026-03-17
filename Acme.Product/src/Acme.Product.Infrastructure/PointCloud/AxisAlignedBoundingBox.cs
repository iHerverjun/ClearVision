using System.Numerics;

namespace Acme.Product.Infrastructure.PointCloud;

public readonly struct AxisAlignedBoundingBox
{
    public Vector3 Min { get; init; }
    public Vector3 Max { get; init; }

    public Vector3 Center => (Min + Max) / 2f;
    public Vector3 Extent => Max - Min;

    public override string ToString() => $"AABB(Min={Min}, Max={Max})";
}

