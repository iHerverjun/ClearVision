using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

public readonly record struct GlcmFeatures(
    double Contrast,
    double Correlation,
    double Energy,
    double Homogeneity,
    double Entropy);

public readonly record struct GlcmDirection(int Dx, int Dy, int Degrees)
{
    public override string ToString() => $"{Degrees}deg({Dx},{Dy})";
}

/// <summary>
/// GLCM (Gray-Level Co-occurrence Matrix) texture features.
/// </summary>
public static class GlcmTexture
{
    public static IReadOnlyList<GlcmDirection> GetDefaultDirections()
    {
        // 0,45,90,135 degrees.
        return new[]
        {
            new GlcmDirection(1, 0, 0),
            new GlcmDirection(1, -1, 45),
            new GlcmDirection(0, -1, 90),
            new GlcmDirection(-1, -1, 135)
        };
    }

    public static (GlcmFeatures Mean, IReadOnlyDictionary<GlcmDirection, GlcmFeatures> PerDirection) Compute(
        Mat src,
        int levels = 16,
        int distance = 1,
        IReadOnlyList<GlcmDirection>? directions = null,
        bool symmetric = true,
        bool normalize = true)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (src.Empty()) throw new ArgumentException("Input image is empty.", nameof(src));
        if (levels < 2 || levels > 256) throw new ArgumentOutOfRangeException(nameof(levels));
        if (distance < 1) throw new ArgumentOutOfRangeException(nameof(distance));

        directions ??= GetDefaultDirections();
        if (directions.Count == 0) throw new ArgumentException("At least one direction is required.", nameof(directions));

        using var gray8U = ToGray8U(src);
        using var quant = Quantize(gray8U, levels);

        var per = new Dictionary<GlcmDirection, GlcmFeatures>();
        foreach (var dir in directions)
        {
            var f = ComputeForDirection(quant, levels, distance, dir, symmetric, normalize);
            per[dir] = f;
        }

        var mean = Average(per.Values);
        return (mean, per);
    }

    private static GlcmFeatures Average(IEnumerable<GlcmFeatures> features)
    {
        double c = 0, corr = 0, e = 0, h = 0, ent = 0;
        int n = 0;
        foreach (var f in features)
        {
            c += f.Contrast;
            corr += f.Correlation;
            e += f.Energy;
            h += f.Homogeneity;
            ent += f.Entropy;
            n++;
        }

        if (n == 0) return default;
        var inv = 1.0 / n;
        return new GlcmFeatures(c * inv, corr * inv, e * inv, h * inv, ent * inv);
    }

    private static GlcmFeatures ComputeForDirection(
        Mat quant8U,
        int levels,
        int distance,
        GlcmDirection dir,
        bool symmetric,
        bool normalize)
    {
        var width = quant8U.Width;
        var height = quant8U.Height;
        var dx = dir.Dx * distance;
        var dy = dir.Dy * distance;

        var counts = new double[levels, levels];
        double sum = 0;

        var idx = quant8U.GetGenericIndexer<byte>();
        for (var y = 0; y < height; y++)
        {
            var ny = y + dy;
            if ((uint)ny >= (uint)height) continue;

            for (var x = 0; x < width; x++)
            {
                var nx = x + dx;
                if ((uint)nx >= (uint)width) continue;

                var i = idx[y, x];
                var j = idx[ny, nx];

                counts[i, j] += 1.0;
                sum += 1.0;
                if (symmetric)
                {
                    counts[j, i] += 1.0;
                    sum += 1.0;
                }
            }
        }

        if (sum <= 0)
        {
            return default;
        }

        if (normalize)
        {
            var inv = 1.0 / sum;
            for (var i = 0; i < levels; i++)
            {
                for (var j = 0; j < levels; j++)
                {
                    counts[i, j] *= inv;
                }
            }
        }

        // Marginals
        var px = new double[levels];
        var py = new double[levels];
        for (var i = 0; i < levels; i++)
        {
            double row = 0;
            for (var j = 0; j < levels; j++)
            {
                row += counts[i, j];
            }
            px[i] = row;
        }

        for (var j = 0; j < levels; j++)
        {
            double col = 0;
            for (var i = 0; i < levels; i++)
            {
                col += counts[i, j];
            }
            py[j] = col;
        }

        double mux = 0, muy = 0;
        for (var i = 0; i < levels; i++)
        {
            mux += i * px[i];
            muy += i * py[i];
        }

        double sigx2 = 0, sigy2 = 0;
        for (var i = 0; i < levels; i++)
        {
            var dxm = i - mux;
            var dym = i - muy;
            sigx2 += (dxm * dxm) * px[i];
            sigy2 += (dym * dym) * py[i];
        }

        var sigx = Math.Sqrt(sigx2);
        var sigy = Math.Sqrt(sigy2);

        double contrast = 0;
        double energy = 0;
        double homogeneity = 0;
        double entropy = 0;
        double corrNumer = 0;

        const double eps = 1e-12;
        for (var i = 0; i < levels; i++)
        {
            for (var j = 0; j < levels; j++)
            {
                var p = counts[i, j];
                if (p <= 0) continue;

                var diff = i - j;
                contrast += (diff * diff) * p;
                energy += p * p;
                homogeneity += p / (1.0 + Math.Abs(diff));
                entropy -= p * Math.Log(p + eps);
                corrNumer += ((i - mux) * (j - muy)) * p;
            }
        }

        var correlation = (sigx * sigy) < eps ? 0.0 : (corrNumer / (sigx * sigy));
        return new GlcmFeatures(contrast, correlation, energy, homogeneity, entropy);
    }

    private static Mat ToGray8U(Mat src)
    {
        Mat gray;
        if (src.Channels() == 1)
        {
            gray = src;
        }
        else
        {
            gray = new Mat();
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        if (gray.Type() == MatType.CV_8UC1)
        {
            return ReferenceEquals(gray, src) ? gray.Clone() : gray;
        }

        // Normalize to 0..255 then convert to 8U.
        using var gray32 = new Mat();
        gray.ConvertTo(gray32, MatType.CV_32FC1);

        Cv2.MinMaxLoc(gray32, out var min, out var max, out _, out _);
        var scale = (max - min) < 1e-12 ? 0.0 : (255.0 / (max - min));
        var shift = -min * scale;

        var out8 = new Mat();
        gray32.ConvertTo(out8, MatType.CV_8UC1, scale, shift);

        if (!ReferenceEquals(gray, src))
        {
            gray.Dispose();
        }

        return out8;
    }

    private static Mat Quantize(Mat gray8U, int levels)
    {
        if (gray8U.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Quantize expects CV_8UC1 grayscale input.", nameof(gray8U));
        }

        var dst = new Mat(gray8U.Rows, gray8U.Cols, MatType.CV_8UC1);
        var srcIdx = gray8U.GetGenericIndexer<byte>();
        var dstIdx = dst.GetGenericIndexer<byte>();

        // Map [0,255] -> [0,levels-1]
        var denom = 256.0 / levels;
        for (var y = 0; y < gray8U.Rows; y++)
        {
            for (var x = 0; x < gray8U.Cols; x++)
            {
                var v = srcIdx[y, x];
                var q = (int)(v / denom);
                if (q < 0) q = 0;
                if (q >= levels) q = levels - 1;
                dstIdx[y, x] = (byte)q;
            }
        }

        return dst;
    }
}
