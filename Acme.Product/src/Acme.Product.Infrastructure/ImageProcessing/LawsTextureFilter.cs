using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// Laws texture filtering (5x5) + local energy computation.
///
/// Notes:
/// - This implementation keeps things simple and uses OpenCV's Filter2D for the 5x5 kernel.
/// - For classic Laws energy, you often subtract a local mean (illumination normalization) before filtering.
/// </summary>
public static class LawsTextureFilter
{
    public static Mat Apply(
        Mat src,
        string kernelCombo,
        bool subtractLocalMean = true,
        int localMeanWindowSize = 15,
        BorderTypes borderType = BorderTypes.Replicate)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (src.Empty()) throw new ArgumentException("Input image is empty.", nameof(src));

        var (kY, kX) = ParseKernelCombo(kernelCombo);
        if (subtractLocalMean && localMeanWindowSize < 3) throw new ArgumentOutOfRangeException(nameof(localMeanWindowSize));

        using var gray32F = ToGray32F01(src);

        Mat? demeaned = null;
        var input = gray32F;
        if (subtractLocalMean)
        {
            var win = EnsureOdd(localMeanWindowSize);
            demeaned = new Mat();
            using var mean = new Mat();
            Cv2.Blur(gray32F, mean, new Size(win, win), new Point(-1, -1), borderType);
            Cv2.Subtract(gray32F, mean, demeaned);
            input = demeaned;
        }

        using var kernel = CreateKernel5x5(kY, kX);
        var filtered = new Mat();
        Cv2.Filter2D(input, filtered, MatType.CV_32FC1, kernel, new Point(-1, -1), 0, borderType);

        demeaned?.Dispose();
        return filtered;
    }

    public static Mat ComputeEnergy(
        Mat filtered32F,
        int windowSize = 15,
        BorderTypes borderType = BorderTypes.Replicate)
    {
        if (filtered32F == null) throw new ArgumentNullException(nameof(filtered32F));
        if (filtered32F.Empty()) throw new ArgumentException("Filtered image is empty.", nameof(filtered32F));
        if (filtered32F.Type() != MatType.CV_32FC1)
        {
            throw new ArgumentException("Filtered image must be CV_32FC1.", nameof(filtered32F));
        }

        var win = EnsureOdd(windowSize);
        if (win < 3) throw new ArgumentOutOfRangeException(nameof(windowSize));

        using var sq = new Mat();
        Cv2.Multiply(filtered32F, filtered32F, sq);

        var energy = new Mat();
        // Energy is local mean of squared responses.
        Cv2.Blur(sq, energy, new Size(win, win), new Point(-1, -1), borderType);
        return energy;
    }

    private static (float[] kY, float[] kX) ParseKernelCombo(string kernelCombo)
    {
        if (string.IsNullOrWhiteSpace(kernelCombo))
        {
            throw new ArgumentException("Kernel combo is required (e.g. \"E5L5\").", nameof(kernelCombo));
        }

        var s = kernelCombo.Trim().ToUpperInvariant();
        if (s.Length != 4 || s[1] != '5' || s[3] != '5')
        {
            throw new ArgumentException("Kernel combo must look like \"E5L5\" / \"R5R5\".", nameof(kernelCombo));
        }

        // Convention: "E5L5" means K = E5' * L5, i.e. kernelY=E5 and kernelX=L5.
        return (Get1DKernel(s[0]), Get1DKernel(s[2]));
    }

    private static float[] Get1DKernel(char code)
    {
        return code switch
        {
            'L' => new[] { 1f, 4f, 6f, 4f, 1f },         // Level
            'E' => new[] { -1f, -2f, 0f, 2f, 1f },       // Edge
            'S' => new[] { -1f, 0f, 2f, 0f, -1f },       // Spot
            'W' => new[] { -1f, 2f, 0f, -2f, 1f },       // Wave
            'R' => new[] { 1f, -4f, 6f, -4f, 1f },       // Ripple
            _ => throw new ArgumentOutOfRangeException(nameof(code), $"Unknown Laws kernel code: {code}")
        };
    }

    private static Mat CreateKernel5x5(float[] kY, float[] kX)
    {
        if (kY.Length != 5) throw new ArgumentException("kY must have length 5.", nameof(kY));
        if (kX.Length != 5) throw new ArgumentException("kX must have length 5.", nameof(kX));

        var k = new float[5, 5];
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                k[y, x] = kY[y] * kX[x];
            }
        }

        return Mat.FromArray(k);
    }

    private static Mat ToGray32F01(Mat src)
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

        var depth = gray.Depth();
        double scale = depth switch
        {
            MatType.CV_8U => 1.0 / 255.0,
            MatType.CV_16U => 1.0 / 65535.0,
            MatType.CV_32F => 1.0,
            MatType.CV_64F => 1.0,
            _ => 1.0
        };

        var outMat = new Mat();
        gray.ConvertTo(outMat, MatType.CV_32FC1, scale);

        if (!ReferenceEquals(gray, src))
        {
            gray.Dispose();
        }

        return outMat;
    }

    private static int EnsureOdd(int v)
    {
        if (v <= 0) return v;
        return (v % 2 == 0) ? v + 1 : v;
    }
}

