using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Subpixel Edge Detection",
    Description = "Extracts subpixel edges using Steger or interpolation-based methods.",
    Category = "Feature Extraction",
    IconName = "edge-subpixel"
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("Edges", "Edge Points", PortDataType.Any)]
[OperatorParam("LowThreshold", "Low Threshold", "double", DefaultValue = 50.0, Min = 0.0, Max = 255.0)]
[OperatorParam("HighThreshold", "High Threshold", "double", DefaultValue = 150.0, Min = 0.0, Max = 255.0)]
[OperatorParam("Sigma", "Gaussian Sigma", "double", DefaultValue = 1.0, Min = 0.1, Max = 10.0)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "GradientInterp", Options = new[] { "Steger|Steger", "GradientInterp|GradientInterp", "GaussianFit|GaussianFit" })]
[OperatorParam("EdgeThreshold", "Steger Edge Threshold", "double", DefaultValue = 10.0, Min = 0.0, Max = 1000.0)]
public class SubpixelEdgeDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.SubpixelEdgeDetection;

    public SubpixelEdgeDetectionOperator(ILogger<SubpixelEdgeDetectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var lowThreshold = GetDoubleParam(@operator, "LowThreshold", 50.0, min: 0.0, max: 255.0);
        var highThreshold = GetDoubleParam(@operator, "HighThreshold", 150.0, min: 0.0, max: 255.0);
        var sigma = GetDoubleParam(@operator, "Sigma", 1.0, min: 0.1, max: 10.0);
        var method = GetStringParam(@operator, "Method", "GradientInterp");
        var edgeThreshold = GetDoubleParam(@operator, "EdgeThreshold", 10.0, min: 0.0, max: 1000.0);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        return RunCpuBoundWork(() =>
        {
            var resultImage = src.Clone();

            List<SubpixelEdgePoint> edgePoints;
            int contourCount;
            if (method.Equals("Steger", StringComparison.OrdinalIgnoreCase))
            {
                using var detector = new StegerSubpixelEdgeDetector
                {
                    EdgeThreshold = edgeThreshold,
                    MaxOffset = 0.5
                };

                edgePoints = detector.DetectEdges(src, lowThreshold, highThreshold);
                contourCount = edgePoints.Count > 0 ? 1 : 0;
            }
            else
            {
                (edgePoints, contourCount) = DetectEdgesTraditional(src, lowThreshold, highThreshold, sigma, method);
            }

            foreach (var point in edgePoints)
            {
                Cv2.Circle(resultImage, new Point((int)point.X, (int)point.Y), 1, new Scalar(0, 255, 0), -1);
                var endX = (int)(point.X + point.NormalX * 5);
                var endY = (int)(point.Y + point.NormalY * 5);
                Cv2.Line(resultImage, new Point((int)point.X, (int)point.Y), new Point(endX, endY), new Scalar(255, 0, 0), 1);
            }

            Cv2.PutText(
                resultImage,
                $"{method}: {edgePoints.Count} edges",
                new Point(10, 30),
                HersheyFonts.HersheySimplex,
                0.7,
                new Scalar(255, 255, 0),
                2);

            var subpixelEdges = edgePoints.Select(p => new Dictionary<string, object>
            {
                { "X", p.X },
                { "Y", p.Y },
                { "NormalX", p.NormalX },
                { "NormalY", p.NormalY },
                { "Strength", p.Strength }
            }).ToList();

            var additionalData = new Dictionary<string, object>
            {
                { "Edges", subpixelEdges },
                { "EdgeCount", edgePoints.Count },
                { "ContourCount", contourCount },
                { "Method", method }
            };

            return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData));
        }, cancellationToken);
    }

    private (List<SubpixelEdgePoint> points, int contourCount) DetectEdgesTraditional(
        Mat src,
        double lowThreshold,
        double highThreshold,
        double sigma,
        string method)
    {
        var edgePoints = new List<SubpixelEdgePoint>();

        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        using var blurred = new Mat();
        var kernelSize = Math.Max(3, ((int)(sigma * 6)) | 1);
        Cv2.GaussianBlur(gray, blurred, new Size(kernelSize, kernelSize), sigma);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, lowThreshold, highThreshold);
        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        using var sobelX = new Mat();
        using var sobelY = new Mat();
        Cv2.Sobel(blurred, sobelX, MatType.CV_64F, 1, 0, 3);
        Cv2.Sobel(blurred, sobelY, MatType.CV_64F, 0, 1, 3);

        var candidates = BuildCandidates(contours, gray, sobelX, sobelY);
        if (candidates.Count == 0)
        {
            return (edgePoints, contours.Length);
        }

        var g1Values = SampleAlongGradient(gray, candidates, -1.0);
        var g3Values = SampleAlongGradient(gray, candidates, 1.0);

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var g1 = g1Values[i];
            var g2 = gray.At<byte>(candidate.Y, candidate.X);
            var g3 = g3Values[i];

            var denom = g1 - (2 * g2) + g3;
            if (Math.Abs(denom) < 1e-10)
            {
                continue;
            }

            var offset = 0.5 * (g1 - g3) / denom;
            if (method.Equals("GaussianFit", StringComparison.OrdinalIgnoreCase))
            {
                offset *= Math.Exp(-(offset * offset) / 2.0);
            }

            edgePoints.Add(new SubpixelEdgePoint
            {
                X = candidate.X + (offset * candidate.DxNorm),
                Y = candidate.Y + (offset * candidate.DyNorm),
                NormalX = candidate.DxNorm,
                NormalY = candidate.DyNorm,
                Strength = candidate.Strength
            });
        }

        return (edgePoints, contours.Length);
    }

    private static List<TraditionalCandidate> BuildCandidates(
        IEnumerable<Point[]> contours,
        Mat gray,
        Mat sobelX,
        Mat sobelY)
    {
        var candidates = new List<TraditionalCandidate>();
        foreach (var contour in contours)
        {
            foreach (var point in contour)
            {
                var x = point.X;
                var y = point.Y;

                if (x <= 0 || x >= gray.Width - 1 || y <= 0 || y >= gray.Height - 1)
                {
                    continue;
                }

                var dx = sobelX.At<double>(y, x);
                var dy = sobelY.At<double>(y, x);
                var gradMag = Math.Sqrt((dx * dx) + (dy * dy));
                if (gradMag < 1e-6)
                {
                    continue;
                }

                candidates.Add(new TraditionalCandidate
                {
                    X = x,
                    Y = y,
                    DxNorm = dx / gradMag,
                    DyNorm = dy / gradMag,
                    Strength = gradMag
                });
            }
        }

        return candidates;
    }

    private static double[] SampleAlongGradient(Mat gray, IReadOnlyList<TraditionalCandidate> candidates, double direction)
    {
        using var mapX = new Mat(candidates.Count, 1, MatType.CV_32FC1);
        using var mapY = new Mat(candidates.Count, 1, MatType.CV_32FC1);
        for (var i = 0; i < candidates.Count; i++)
        {
            mapX.Set(i, 0, (float)(candidates[i].X + (direction * candidates[i].DxNorm)));
            mapY.Set(i, 0, (float)(candidates[i].Y + (direction * candidates[i].DyNorm)));
        }

        using var sampled = new Mat();
        Cv2.Remap(gray, sampled, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Replicate);

        var values = new double[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            values[i] = sampled.At<byte>(i, 0);
        }

        return values;
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var lowThreshold = GetDoubleParam(@operator, "LowThreshold", 50.0);
        if (lowThreshold < 0 || lowThreshold > 255)
        {
            return ValidationResult.Invalid("LowThreshold must be between 0 and 255.");
        }

        var highThreshold = GetDoubleParam(@operator, "HighThreshold", 150.0);
        if (highThreshold < 0 || highThreshold > 255)
        {
            return ValidationResult.Invalid("HighThreshold must be between 0 and 255.");
        }

        if (lowThreshold >= highThreshold)
        {
            return ValidationResult.Invalid("LowThreshold must be smaller than HighThreshold.");
        }

        var sigma = GetDoubleParam(@operator, "Sigma", 1.0);
        if (sigma < 0.1 || sigma > 10.0)
        {
            return ValidationResult.Invalid("Sigma must be between 0.1 and 10.0.");
        }

        var method = GetStringParam(@operator, "Method", "GradientInterp");
        var validMethods = new[] { "Steger", "GradientInterp", "GaussianFit" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid($"Method must be one of: {string.Join(", ", validMethods)}");
        }

        var edgeThreshold = GetDoubleParam(@operator, "EdgeThreshold", 10.0);
        if (edgeThreshold < 0 || edgeThreshold > 1000)
        {
            return ValidationResult.Invalid("EdgeThreshold must be between 0 and 1000.");
        }

        return ValidationResult.Valid();
    }

    private sealed class TraditionalCandidate
    {
        public int X { get; init; }
        public int Y { get; init; }
        public double DxNorm { get; init; }
        public double DyNorm { get; init; }
        public double Strength { get; init; }
    }
}
