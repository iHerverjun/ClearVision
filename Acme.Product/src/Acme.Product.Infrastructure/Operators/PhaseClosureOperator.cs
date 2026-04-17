// PhaseClosureOperator.cs
// 相位闭合 / 解缠绕算子

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Phase Closure",
    Description = "Unwraps wrapped phase maps while preserving the original phase domain semantics.",
    Category = "Measurement",
    IconName = "phase-closure",
    Keywords = new[] { "Phase", "Unwrap", "Interferometry", "Closure", "Wavelength" }
)]
[InputPort("PhaseImage", "Wrapped Phase Image", PortDataType.Image, IsRequired = true)]
[InputPort("Wavelength", "Wavelength (nm)", PortDataType.Float, IsRequired = false)]
[InputPort("UnwrapMethod", "Unwrapping Method", PortDataType.String, IsRequired = false)]
[InputPort("QualityMap", "Quality Map (optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("UnwrappedPhase", "Unwrapped Phase", PortDataType.Image)]
[OutputPort("Discontinuities", "Phase Discontinuities", PortDataType.Image)]
[OutputPort("Quality", "Quality Metric", PortDataType.Float)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class PhaseClosureOperator : OperatorBase
{
    private const double TwoPi = 2.0 * Math.PI;

    public override OperatorType OperatorType => OperatorType.PhaseClosure;

    public PhaseClosureOperator(ILogger<PhaseClosureOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "PhaseImage", out var phaseWrapper) || phaseWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("PhaseImage required."));
        }

        var wavelength = GetDouble(inputs, "Wavelength", 0.0);
        var method = GetString(inputs, "UnwrapMethod", "itoh").Trim().ToLowerInvariant();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var wrappedPhase = PrepareWrappedPhaseInput(phaseWrapper.GetMat());
        using var qualityMap = TryGetOptionalQualityMap(inputs);

        Mat unwrappedPhase;
        double quality;

        switch (method)
        {
            case "quality":
                (unwrappedPhase, quality) = QualityGuidedUnwrap(wrappedPhase, qualityMap);
                break;
            case "floodfill":
                (unwrappedPhase, quality) = FloodFillUnwrap(wrappedPhase);
                break;
            case "itoh":
            default:
                (unwrappedPhase, quality) = ItohUnwrap(wrappedPhase);
                break;
        }

        var scaledPhase = wavelength > 0
            ? ConvertPhaseToPhysicalDisplacement(unwrappedPhase, wavelength)
            : unwrappedPhase.Clone();
        unwrappedPhase.Dispose();

        var discontinuities = DetectDiscontinuities(wrappedPhase);
        var visualization = CreateVisualization(wrappedPhase, scaledPhase, discontinuities);

        stopwatch.Stop();
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, new Dictionary<string, object>
        {
            { "UnwrappedPhase", new ImageWrapper(scaledPhase) },
            { "Discontinuities", new ImageWrapper(discontinuities) },
            { "Quality", quality },
            { "Wavelength", wavelength },
            { "Method", method },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private static Mat PrepareWrappedPhaseInput(Mat source)
    {
        using var gray = source.Channels() == 1
            ? source.Clone()
            : source.CvtColor(ColorConversionCodes.BGR2GRAY);

        using var phase32 = new Mat();
        var depth = gray.Depth();
        var scale = depth switch
        {
            MatType.CV_8U => TwoPi / 255.0,
            MatType.CV_16U => TwoPi / 65535.0,
            MatType.CV_32F => 1.0,
            MatType.CV_64F => 1.0,
            _ => 1.0
        };

        gray.ConvertTo(phase32, MatType.CV_32FC1, scale);
        var wrapped = new Mat(phase32.Size(), MatType.CV_32FC1);
        for (var y = 0; y < phase32.Rows; y++)
        {
            for (var x = 0; x < phase32.Cols; x++)
            {
                wrapped.Set(y, x, WrapToPi(phase32.At<float>(y, x)));
            }
        }

        return wrapped;
    }

    private Mat? TryGetOptionalQualityMap(Dictionary<string, object>? inputs)
    {
        if (!TryGetInputImage(inputs, "QualityMap", out var qualityWrapper) || qualityWrapper == null)
        {
            return null;
        }

        using var gray = qualityWrapper.GetMat().Channels() == 1
            ? qualityWrapper.GetMat().Clone()
            : qualityWrapper.GetMat().CvtColor(ColorConversionCodes.BGR2GRAY);

        var quality = new Mat();
        gray.ConvertTo(quality, MatType.CV_32FC1);
        return quality;
    }

    private static (Mat unwrapped, double quality) ItohUnwrap(Mat wrapped)
    {
        var unwrapped = wrapped.Clone();

        for (var y = 0; y < wrapped.Rows; y++)
        {
            var running = wrapped.At<float>(y, 0);
            unwrapped.Set(y, 0, running);
            for (var x = 1; x < wrapped.Cols; x++)
            {
                var currentWrapped = wrapped.At<float>(y, x);
                running += WrappedDifference(currentWrapped, wrapped.At<float>(y, x - 1));
                unwrapped.Set(y, x, running);
            }
        }

        for (var x = 0; x < wrapped.Cols; x++)
        {
            var running = unwrapped.At<float>(0, x);
            for (var y = 1; y < wrapped.Rows; y++)
            {
                var currentUnwrapped = unwrapped.At<float>(y, x);
                running += WrappedDifference(currentUnwrapped, unwrapped.At<float>(y - 1, x));
                unwrapped.Set(y, x, running);
            }
        }

        return (unwrapped, CalculateQuality(unwrapped));
    }

    private static (Mat unwrapped, double quality) QualityGuidedUnwrap(Mat wrapped, Mat? externalQualityMap)
    {
        using var quality = externalQualityMap?.Clone() ?? BuildQualityMap(wrapped);
        var unwrapped = new Mat(wrapped.Size(), MatType.CV_32FC1, Scalar.All(float.NaN));
        var visited = new bool[wrapped.Rows, wrapped.Cols];
        var queue = new PriorityQueue<Point, float>();

        Cv2.MinMaxLoc(quality, out _, out _, out _, out var seed);
        unwrapped.Set(seed.Y, seed.X, wrapped.At<float>(seed.Y, seed.X));
        visited[seed.Y, seed.X] = true;
        queue.Enqueue(seed, -quality.At<float>(seed.Y, seed.X));

        ProcessUnwrapQueue(wrapped, quality, unwrapped, visited, queue, usePriority: true);
        FillRemainingIslands(wrapped, quality, unwrapped, visited, usePriority: true);

        return (unwrapped, CalculateQuality(unwrapped));
    }

    private static (Mat unwrapped, double quality) FloodFillUnwrap(Mat wrapped)
    {
        using var quality = BuildQualityMap(wrapped);
        var unwrapped = new Mat(wrapped.Size(), MatType.CV_32FC1, Scalar.All(float.NaN));
        var visited = new bool[wrapped.Rows, wrapped.Cols];
        var queue = new PriorityQueue<Point, float>();

        var seed = new Point(0, 0);
        unwrapped.Set(seed.Y, seed.X, wrapped.At<float>(seed.Y, seed.X));
        visited[seed.Y, seed.X] = true;
        queue.Enqueue(seed, 0f);

        ProcessUnwrapQueue(wrapped, quality, unwrapped, visited, queue, usePriority: false);
        FillRemainingIslands(wrapped, quality, unwrapped, visited, usePriority: false);

        return (unwrapped, CalculateQuality(unwrapped));
    }

    private static void FillRemainingIslands(Mat wrapped, Mat quality, Mat unwrapped, bool[,] visited, bool usePriority)
    {
        for (var y = 0; y < wrapped.Rows; y++)
        {
            for (var x = 0; x < wrapped.Cols; x++)
            {
                if (visited[y, x])
                {
                    continue;
                }

                var queue = new PriorityQueue<Point, float>();
                unwrapped.Set(y, x, wrapped.At<float>(y, x));
                visited[y, x] = true;
                queue.Enqueue(new Point(x, y), usePriority ? -quality.At<float>(y, x) : 0f);
                ProcessUnwrapQueue(wrapped, quality, unwrapped, visited, queue, usePriority);
            }
        }
    }

    private static void ProcessUnwrapQueue(
        Mat wrapped,
        Mat quality,
        Mat unwrapped,
        bool[,] visited,
        PriorityQueue<Point, float> queue,
        bool usePriority)
    {
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentWrapped = wrapped.At<float>(current.Y, current.X);
            var currentUnwrapped = unwrapped.At<float>(current.Y, current.X);

            foreach (var neighbor in EnumerateNeighbors(current, wrapped.Size()))
            {
                if (visited[neighbor.Y, neighbor.X])
                {
                    continue;
                }

                var neighborWrapped = wrapped.At<float>(neighbor.Y, neighbor.X);
                var delta = WrappedDifference(neighborWrapped, currentWrapped);
                unwrapped.Set(neighbor.Y, neighbor.X, currentUnwrapped + delta);
                visited[neighbor.Y, neighbor.X] = true;
                queue.Enqueue(neighbor, usePriority ? -quality.At<float>(neighbor.Y, neighbor.X) : 0f);
            }
        }
    }

    private static IEnumerable<Point> EnumerateNeighbors(Point point, Size size)
    {
        var offsets = new[]
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1)
        };

        foreach (var offset in offsets)
        {
            var nx = point.X + offset.X;
            var ny = point.Y + offset.Y;
            if (nx >= 0 && nx < size.Width && ny >= 0 && ny < size.Height)
            {
                yield return new Point(nx, ny);
            }
        }
    }

    private static Mat BuildQualityMap(Mat wrapped)
    {
        using var dx = new Mat();
        using var dy = new Mat();
        using var magnitude = new Mat();

        Cv2.Sobel(wrapped, dx, MatType.CV_32FC1, 1, 0, 3);
        Cv2.Sobel(wrapped, dy, MatType.CV_32FC1, 0, 1, 3);
        Cv2.Magnitude(dx, dy, magnitude);

        var quality = new Mat();
        Cv2.Add(magnitude, Scalar.All(1.0), quality);
        Cv2.Divide(1.0, quality, quality);
        return quality;
    }

    private static float WrappedDifference(float current, float reference)
    {
        return WrapToPi(current - reference);
    }

    private static float WrapToPi(double value)
    {
        return (float)Math.Atan2(Math.Sin(value), Math.Cos(value));
    }

    private static Mat ConvertPhaseToPhysicalDisplacement(Mat phase, double wavelength)
    {
        var scaled = new Mat();
        phase.ConvertTo(scaled, MatType.CV_32FC1, wavelength / TwoPi);
        return scaled;
    }

    private static Mat DetectDiscontinuities(Mat wrapped)
    {
        var discontinuities = new Mat(wrapped.Size(), MatType.CV_8UC1, Scalar.Black);

        for (var y = 1; y < wrapped.Rows; y++)
        {
            for (var x = 1; x < wrapped.Cols; x++)
            {
                var current = wrapped.At<float>(y, x);
                var left = wrapped.At<float>(y, x - 1);
                var top = wrapped.At<float>(y - 1, x);

                var diffLeft = Math.Abs(WrappedDifference(current, left));
                var diffTop = Math.Abs(WrappedDifference(current, top));
                if (diffLeft > Math.PI * 0.9 || diffTop > Math.PI * 0.9)
                {
                    discontinuities.Set(y, x, (byte)255);
                }
            }
        }

        return discontinuities;
    }

    private static double CalculateQuality(Mat unwrapped)
    {
        using var dx = new Mat();
        using var dy = new Mat();
        using var magnitude = new Mat();

        Cv2.Sobel(unwrapped, dx, MatType.CV_32FC1, 1, 0, 3);
        Cv2.Sobel(unwrapped, dy, MatType.CV_32FC1, 0, 1, 3);
        Cv2.Magnitude(dx, dy, magnitude);
        Cv2.MeanStdDev(magnitude, out _, out var stddev);

        return 1.0 / (1.0 + stddev.Val0);
    }

    private static Mat CreateVisualization(Mat wrapped, Mat unwrapped, Mat discontinuities)
    {
        using var wrappedVis = NormalizeForDisplay(wrapped);
        using var unwrappedVis = NormalizeForDisplay(unwrapped);
        using var discontinuityColor = new Mat();

        Cv2.CvtColor(discontinuities, discontinuityColor, ColorConversionCodes.GRAY2BGR);

        var combined = new Mat(wrapped.Rows, wrapped.Cols * 3, MatType.CV_8UC3, Scalar.Black);
        wrappedVis.CopyTo(new Mat(combined, new Rect(0, 0, wrapped.Cols, wrapped.Rows)));
        unwrappedVis.CopyTo(new Mat(combined, new Rect(wrapped.Cols, 0, wrapped.Cols, wrapped.Rows)));
        Cv2.AddWeighted(unwrappedVis, 0.7, discontinuityColor, 0.5, 0, new Mat(combined, new Rect(wrapped.Cols * 2, 0, wrapped.Cols, wrapped.Rows)));

        Cv2.PutText(combined, "Wrapped", new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);
        Cv2.PutText(combined, "Unwrapped", new Point(wrapped.Cols + 10, 30), HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);
        Cv2.PutText(combined, "Discontinuities", new Point((wrapped.Cols * 2) + 10, 30), HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);
        return combined;
    }

    private static Mat NormalizeForDisplay(Mat source)
    {
        using var normalized = new Mat();
        Cv2.Normalize(source, normalized, 0, 255, NormTypes.MinMax);
        normalized.ConvertTo(normalized, MatType.CV_8UC1);

        var colored = new Mat();
        Cv2.ApplyColorMap(normalized, colored, ColormapTypes.Jet);
        return colored;
    }

    private static double GetDouble(Dictionary<string, object>? inputs, string key, double defaultValue) =>
        inputs?.TryGetValue(key, out var value) == true ? Convert.ToDouble(value) : defaultValue;

    private static string GetString(Dictionary<string, object>? inputs, string key, string defaultValue) =>
        inputs?.TryGetValue(key, out var value) == true ? value?.ToString() ?? defaultValue : defaultValue;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
