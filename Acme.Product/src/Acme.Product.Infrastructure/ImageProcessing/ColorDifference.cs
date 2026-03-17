namespace Acme.Product.Infrastructure.ImageProcessing;

public static class ColorDifference
{
    public static double DeltaE76(CieLab c1, CieLab c2)
    {
        var dl = c2.L - c1.L;
        var da = c2.A - c1.A;
        var db = c2.B - c1.B;
        return Math.Sqrt((dl * dl) + (da * da) + (db * db));
    }

    // CIEDE2000 implementation based on:
    // Sharma, W., Wu, E., & Dalal, E. N. (2005).
    // The CIEDE2000 Color-Difference Formula: Implementation Notes, Supplementary Test Data, and Mathematical Observations.
    public static double DeltaE00(CieLab c1, CieLab c2, double kL = 1.0, double kC = 1.0, double kH = 1.0)
    {
        // Step 1: Compute C' and h' for each color
        var L1 = c1.L;
        var a1 = c1.A;
        var b1 = c1.B;
        var L2 = c2.L;
        var a2 = c2.A;
        var b2 = c2.B;

        var c1ab = Math.Sqrt((a1 * a1) + (b1 * b1));
        var c2ab = Math.Sqrt((a2 * a2) + (b2 * b2));

        var cBar = (c1ab + c2ab) * 0.5;
        var cBar7 = Math.Pow(cBar, 7.0);
        var g = 0.5 * (1.0 - Math.Sqrt(cBar7 / (cBar7 + Math.Pow(25.0, 7.0))));

        var a1p = (1.0 + g) * a1;
        var a2p = (1.0 + g) * a2;

        var c1p = Math.Sqrt((a1p * a1p) + (b1 * b1));
        var c2p = Math.Sqrt((a2p * a2p) + (b2 * b2));

        var h1p = Hp(b1, a1p);
        var h2p = Hp(b2, a2p);

        // Step 2: ΔL', ΔC', ΔH'
        var dLp = L2 - L1;
        var dCp = c2p - c1p;

        var dhp = Dhp(h1p, h2p, c1p, c2p);
        var dHp = 2.0 * Math.Sqrt(c1p * c2p) * Math.Sin(DegToRad(dhp) * 0.5);

        // Step 3: Compute averages L', C', h'
        var LpBar = (L1 + L2) * 0.5;
        var CpBar = (c1p + c2p) * 0.5;

        var hpBar = HBar(h1p, h2p, c1p, c2p);

        // Step 4: Compute weighting functions
        var t =
            1.0
            - 0.17 * Math.Cos(DegToRad(hpBar - 30.0))
            + 0.24 * Math.Cos(DegToRad(2.0 * hpBar))
            + 0.32 * Math.Cos(DegToRad((3.0 * hpBar) + 6.0))
            - 0.20 * Math.Cos(DegToRad((4.0 * hpBar) - 63.0));

        var dTheta = 30.0 * Math.Exp(-Math.Pow((hpBar - 275.0) / 25.0, 2.0));
        var rC = 2.0 * Math.Sqrt(Math.Pow(CpBar, 7.0) / (Math.Pow(CpBar, 7.0) + Math.Pow(25.0, 7.0)));
        var sL = 1.0 + (0.015 * Math.Pow(LpBar - 50.0, 2.0)) / Math.Sqrt(20.0 + Math.Pow(LpBar - 50.0, 2.0));
        var sC = 1.0 + 0.045 * CpBar;
        var sH = 1.0 + 0.015 * CpBar * t;
        var rT = -Math.Sin(DegToRad(2.0 * dTheta)) * rC;

        // Step 5: ΔE00
        var dL = dLp / (kL * sL);
        var dC = dCp / (kC * sC);
        var dH = dHp / (kH * sH);

        return Math.Sqrt((dL * dL) + (dC * dC) + (dH * dH) + (rT * dC * dH));
    }

    private static double Hp(double b, double ap)
    {
        if (Math.Abs(ap) < 1e-15 && Math.Abs(b) < 1e-15) return 0.0;

        var h = RadToDeg(Math.Atan2(b, ap));
        if (h < 0) h += 360.0;
        return h;
    }

    private static double Dhp(double h1p, double h2p, double c1p, double c2p)
    {
        if (c1p * c2p < 1e-15) return 0.0;
        var d = h2p - h1p;
        if (d > 180.0) d -= 360.0;
        if (d < -180.0) d += 360.0;
        return d;
    }

    private static double HBar(double h1p, double h2p, double c1p, double c2p)
    {
        if (c1p * c2p < 1e-15) return h1p + h2p;

        var sum = h1p + h2p;
        var diff = Math.Abs(h1p - h2p);

        if (diff > 180.0)
        {
            // Wrap across 0/360.
            if (sum < 360.0)
            {
                return (sum + 360.0) * 0.5;
            }

            return (sum - 360.0) * 0.5;
        }

        return sum * 0.5;
    }

    private static double DegToRad(double deg) => deg * (Math.PI / 180.0);
    private static double RadToDeg(double rad) => rad * (180.0 / Math.PI);
}

