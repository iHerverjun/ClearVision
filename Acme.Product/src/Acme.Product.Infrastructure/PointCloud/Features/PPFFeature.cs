using System.Numerics;

namespace Acme.Product.Infrastructure.PointCloud.Features;

/// <summary>
/// Point Pair Feature (PPF) as a 4D descriptor:
/// - Distance: ||p2 - p1||
/// - Angle1: angle between n1 and (p2 - p1)
/// - Angle2: angle between n2 and (p2 - p1)
/// - AngleNormals: angle between n1 and n2
/// </summary>
public readonly record struct PPFFeature(float Distance, float Angle1, float Angle2, float AngleNormals)
{
    public static PPFFeature Compute(Vector3 p1, Vector3 n1, Vector3 p2, Vector3 n2)
    {
        var d = p2 - p1;
        var dist = d.Length();
        if (dist <= 1e-20f || !float.IsFinite(dist))
        {
            return default;
        }

        d /= dist;

        n1 = SafeNormalize(n1);
        n2 = SafeNormalize(n2);

        // Normal direction is often ambiguous (can flip). Using abs(dot) makes angles stable under normal flips.
        var a1 = Acos01(MathF.Abs(Vector3.Dot(n1, d)));
        var a2 = Acos01(MathF.Abs(Vector3.Dot(n2, d)));
        var an = Acos01(MathF.Abs(Vector3.Dot(n1, n2)));

        return new PPFFeature(dist, a1, a2, an);
    }

    private static Vector3 SafeNormalize(Vector3 v)
    {
        var len2 = v.LengthSquared();
        if (len2 <= 1e-20f || !float.IsFinite(len2))
        {
            return Vector3.UnitZ;
        }
        return Vector3.Normalize(v);
    }

    private static float Acos01(float x)
    {
        // x should be within [0,1], but numerical drift happens.
        x = Math.Clamp(x, 0f, 1f);
        return MathF.Acos(x);
    }
}

