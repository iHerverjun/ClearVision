using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

public readonly record struct CieLab(double L, double A, double B);

/// <summary>
/// Convert sRGB (D65) to CIE L*a*b* (1976).
/// </summary>
public static class CieLabConverter
{
    // D65 reference white for sRGB.
    private const double Xn = 0.95047;
    private const double Yn = 1.00000;
    private const double Zn = 1.08883;

    // (6/29)^3
    private const double Epsilon = 0.008856451679035631;
    // (29/3)^3 / 116
    private const double Kappa = 903.2962962962963;

    private static readonly double[] SrgbToLinear = BuildSrgbToLinearTable();

    public static CieLab RgbToLab(byte r, byte g, byte b)
    {
        var rl = SrgbToLinear[r];
        var gl = SrgbToLinear[g];
        var bl = SrgbToLinear[b];

        // sRGB -> XYZ (D65)
        // Values are in 0..1 range, reference white is Xn,Yn,Zn.
        var x = (0.4124564 * rl) + (0.3575761 * gl) + (0.1804375 * bl);
        var y = (0.2126729 * rl) + (0.7151522 * gl) + (0.0721750 * bl);
        var z = (0.0193339 * rl) + (0.1191920 * gl) + (0.9503041 * bl);

        return XyzToLab(x, y, z);
    }

    public static CieLab BgrToLab(byte b, byte g, byte r)
    {
        return RgbToLab(r, g, b);
    }

    public static CieLab XyzToLab(double x, double y, double z)
    {
        var fx = F(x / Xn);
        var fy = F(y / Yn);
        var fz = F(z / Zn);

        var l = (116.0 * fy) - 16.0;
        var a = 500.0 * (fx - fy);
        var b = 200.0 * (fy - fz);
        return new CieLab(l, a, b);
    }

    public static CieLab ComputeMeanLabBgr8U(Mat bgr8U, Rect roi)
    {
        if (bgr8U == null) throw new ArgumentNullException(nameof(bgr8U));
        if (bgr8U.Empty()) throw new ArgumentException("Input image is empty.", nameof(bgr8U));
        if (bgr8U.Type() != MatType.CV_8UC3) throw new ArgumentException("Expected CV_8UC3 BGR image.", nameof(bgr8U));

        var r = ClampRoi(roi, bgr8U.Width, bgr8U.Height);
        if (r.Width <= 0 || r.Height <= 0) return default;

        var idx = bgr8U.GetGenericIndexer<Vec3b>();
        double sumL = 0, sumA = 0, sumB = 0;
        long n = 0;

        for (var y = r.Top; y < r.Bottom; y++)
        {
            for (var x = r.Left; x < r.Right; x++)
            {
                var v = idx[y, x];
                var lab = BgrToLab(v.Item0, v.Item1, v.Item2);
                sumL += lab.L;
                sumA += lab.A;
                sumB += lab.B;
                n++;
            }
        }

        if (n <= 0) return default;
        var inv = 1.0 / n;
        return new CieLab(sumL * inv, sumA * inv, sumB * inv);
    }

    private static Rect ClampRoi(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        var w = Math.Clamp(rect.Width, 0, width - x);
        var h = Math.Clamp(rect.Height, 0, height - y);
        return new Rect(x, y, w, h);
    }

    private static double F(double t)
    {
        // CIE f(t) piecewise function.
        if (t > Epsilon)
        {
            return Math.Cbrt(t);
        }

        return ((Kappa * t) + 16.0) / 116.0;
    }

    private static double[] BuildSrgbToLinearTable()
    {
        var table = new double[256];
        for (var i = 0; i < 256; i++)
        {
            var c = i / 255.0;
            table[i] = c <= 0.04045 ? (c / 12.92) : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return table;
    }
}

