using System.Collections;
using OpenCvSharp;

namespace Acme.Product.Tests.Performance;

internal static class PreprocessingTestSupport
{
    public static string ResolveWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Acme.Product")) &&
                File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static string ResolveTestDataPath(string relativePath)
    {
        var candidate = Path.Combine(ResolveWorkspaceRoot(), "Acme.Product", "tests", "TestData", relativePath);
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException($"Test data file not found: {candidate}", candidate);
        }

        return candidate;
    }

    public static string ResolveWorkspacePath(params string[] segments)
    {
        var candidate = Path.Combine(new[] { ResolveWorkspaceRoot() }.Concat(segments).ToArray());
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            throw new FileNotFoundException($"Workspace path not found: {candidate}", candidate);
        }

        return candidate;
    }

    public static string EnsureReportDirectory()
    {
        var path = Path.Combine(ResolveWorkspaceRoot(), "Acme.Product", "test_results");
        Directory.CreateDirectory(path);
        return path;
    }

    public static double ComputeMeanAbsoluteError(Mat actual, Mat reference)
    {
        using var actualGray = ToGray(actual);
        using var referenceGray = ToGray(reference);
        using var diff = new Mat();
        Cv2.Absdiff(actualGray, referenceGray, diff);
        return Cv2.Mean(diff).Val0;
    }

    public static double ComputePsnr(Mat actual, Mat reference)
    {
        using var actualGray = ToGray(actual);
        using var referenceGray = ToGray(reference);
        return Cv2.PSNR(actualGray, referenceGray);
    }

    public static double ComputeEntropy(Mat image)
    {
        using var gray = ToGray(image);
        using var hist = new Mat();
        Cv2.CalcHist(
            new[] { gray },
            new[] { 0 },
            null,
            hist,
            1,
            new[] { 256 },
            new Rangef[] { new(0, 256) });

        var total = gray.Rows * gray.Cols;
        var entropy = 0d;
        for (var i = 0; i < 256; i++)
        {
            var probability = hist.At<float>(i) / total;
            if (probability > 0)
            {
                entropy -= probability * Math.Log(probability, 2);
            }
        }

        return entropy;
    }

    public static double ComputeRmsContrast(Mat image)
    {
        using var gray = ToGray(image);
        Cv2.MeanStdDev(gray, out _, out var stddev);
        return stddev.Val0;
    }

    public static double ComputeIlluminationCoefficientOfVariation(Mat image)
    {
        using var gray = ToGray(image);
        using var background = new Mat();
        var minDim = Math.Min(gray.Rows, gray.Cols);
        var kernel = Math.Max(31, MakeOdd(Math.Max(5, minDim / 8)));
        Cv2.GaussianBlur(gray, background, new Size(kernel, kernel), 0);
        Cv2.MeanStdDev(background, out var mean, out var stddev);
        return stddev.Val0 / Math.Max(mean.Val0, 1.0);
    }

    public static double ComputeSharpness(Mat image)
    {
        using var gray = ToGray(image);
        using var laplacian = new Mat();
        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var stddev);
        return stddev.Val0 * stddev.Val0;
    }

    public static void DisposeObjectGraph(object? value)
    {
        DisposeObjectGraph(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static void DisposeObjectGraph(object? value, HashSet<object> visited)
    {
        if (value == null)
        {
            return;
        }

        if (value is not ValueType && value is not string && !visited.Add(value))
        {
            return;
        }

        if (value is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                DisposeObjectGraph(entry.Value, visited);
            }

            return;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                DisposeObjectGraph(item, visited);
            }
        }
    }

    private static Mat ToGray(Mat image)
    {
        if (image.Channels() == 1)
        {
            return image.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static int MakeOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }
}
