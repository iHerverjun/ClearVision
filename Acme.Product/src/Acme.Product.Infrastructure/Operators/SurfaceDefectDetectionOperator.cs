using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "表面缺陷检测",
    Description = "Detects surface defects using gradient, aligned reference diff, or local contrast.",
    Category = "AI检测",
    IconName = "surface-defect",
    Keywords = new[] { "surface defect", "scratch", "stain", "traditional detection" },
    Tags = new[] { "experimental", "industrial-remediation", "surface-defect" },
    Version = "2.0.0"
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Reference", "Reference", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("DefectMask", "Defect Mask", PortDataType.Image)]
[OutputPort("ResponseImage", "Response Image", PortDataType.Image)]
[OutputPort("DefectCount", "Defect Count", PortDataType.Integer)]
[OutputPort("DefectArea", "Defect Area", PortDataType.Float)]
[OutputPort("AlignmentScore", "Alignment Score", PortDataType.Float)]
[OutputPort("RejectedReason", "Rejected Reason", PortDataType.String)]
[OutputPort("Diagnostics", "Diagnostics", PortDataType.Any)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "GradientMagnitude", Options = new[] { "GradientMagnitude|GradientMagnitude", "ReferenceDiff|ReferenceDiff", "LocalContrast|LocalContrast" })]
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 35.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MinArea", "Min Area", "int", DefaultValue = 20, Min = 0, Max = 10000000)]
[OperatorParam("MaxArea", "Max Area", "int", DefaultValue = 1000000, Min = 0, Max = 10000000)]
[OperatorParam("MorphCleanSize", "Morph Clean Size", "int", DefaultValue = 3, Min = 1, Max = 301)]
[OperatorParam("AlignmentMode", "Alignment Mode", "enum", DefaultValue = "PhaseCorrelation", Options = new[] { "None|None", "PhaseCorrelation|PhaseCorrelation" })]
[OperatorParam("NormalizationMode", "Normalization Mode", "enum", DefaultValue = "LocalMean", Options = new[] { "None|None", "LocalMean|LocalMean" })]
[OperatorParam("ThresholdMode", "Threshold Mode", "enum", DefaultValue = "Auto", Options = new[] { "Auto|Auto", "Manual|Manual", "Otsu|Otsu", "ReferenceStats|ReferenceStats" })]
[OperatorParam("BackgroundKernelSize", "Background Kernel Size", "int", DefaultValue = 31, Min = 3, Max = 301)]
[OperatorParam("ReferenceStatsSigma", "Reference Stats Sigma", "double", DefaultValue = 2.5, Min = 0.1, Max = 10.0)]
public class SurfaceDefectDetectionOperator : OperatorBase
{
    private const double MinAcceptedPhaseCorrelationResponse = 0.02;
    private const double MaxAcceptedShiftRatio = 0.45;
    private const double MinAcceptedImprovementRatio = -0.04;

    public override OperatorType OperatorType => OperatorType.SurfaceDefectDetection;

    public SurfaceDefectDetectionOperator(ILogger<SurfaceDefectDetectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required"));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid"));
        }

        var method = GetStringParam(@operator, "Method", "GradientMagnitude");
        var manualThreshold = GetDoubleParam(@operator, "Threshold", 35.0, 0.0, 255.0);
        var minArea = GetIntParam(@operator, "MinArea", 20, 0, 10_000_000);
        var maxArea = GetIntParam(@operator, "MaxArea", 1_000_000, 0, 10_000_000);
        var cleanSize = GetIntParam(@operator, "MorphCleanSize", 3, 1, 301);
        var alignmentMode = GetStringParam(@operator, "AlignmentMode", "PhaseCorrelation");
        var normalizationMode = GetStringParam(@operator, "NormalizationMode", "LocalMean");
        var thresholdMode = GetStringParam(@operator, "ThresholdMode", "Auto");
        var backgroundKernelSize = GetIntParam(@operator, "BackgroundKernelSize", 31, 3, 301);
        var referenceStatsSigma = GetDoubleParam(@operator, "ReferenceStatsSigma", 2.5, 0.1, 10.0);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var response = BuildResponseMap(
            method,
            gray,
            inputs,
            alignmentMode,
            normalizationMode,
            backgroundKernelSize,
            out var alignmentScore,
            out var alignmentShift,
            out var rejectedReason);

        if (response.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Failed to compute defect response map"));
        }

        using var binary = new Mat();
        var appliedThreshold = ApplyThreshold(response, binary, method, thresholdMode, manualThreshold, referenceStatsSigma);

        var oddKernel = cleanSize % 2 == 0 ? cleanSize + 1 : cleanSize;
        if (oddKernel > 1)
        {
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(oddKernel, oddKernel));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);
        }

        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var resultImage = src.Clone();
        var defectMask = new Mat(binary.Size(), MatType.CV_8UC1, Scalar.Black);
        var responseImage = response.Clone();

        var defectCount = 0;
        var defectArea = 0.0;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minArea || area > maxArea)
            {
                continue;
            }

            defectCount++;
            defectArea += area;

            Cv2.DrawContours(defectMask, new[] { contour }, -1, Scalar.White, -1);
            var rect = Cv2.BoundingRect(contour);
            Cv2.Rectangle(resultImage, rect, new Scalar(0, 0, 255), 2);
        }

        Cv2.PutText(resultImage, $"Defects:{defectCount} Area:{defectArea:F1}", new Point(8, 24), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 2);
        Cv2.PutText(resultImage, $"Thr:{appliedThreshold:F1} Align:{alignmentScore:F2}", new Point(8, 48), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);

        Cv2.MeanStdDev(response, out var responseMean, out var responseStdDev);
        var diagnostics = new Dictionary<string, object>
        {
            { "Method", method },
            { "AlignmentMode", alignmentMode },
            { "NormalizationMode", normalizationMode },
            { "ThresholdMode", ResolveThresholdMode(method, thresholdMode) },
            { "AppliedThreshold", appliedThreshold },
            { "AlignmentScore", alignmentScore },
            { "AlignmentShiftX", alignmentShift.X },
            { "AlignmentShiftY", alignmentShift.Y },
            { "CandidateCount", contours.Length },
            { "AcceptedCount", defectCount },
            { "RejectedReason", rejectedReason },
            { "ResponseMean", responseMean.Val0 },
            { "ResponseStdDev", responseStdDev.Val0 }
        };

        var additional = new Dictionary<string, object>
        {
            { "DefectMask", new ImageWrapper(defectMask) },
            { "ResponseImage", new ImageWrapper(responseImage) },
            { "DefectCount", defectCount },
            { "DefectArea", defectArea },
            { "AlignmentScore", alignmentScore },
            { "RejectedReason", rejectedReason },
            { "Diagnostics", diagnostics }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additional)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "GradientMagnitude");
        var validMethods = new[] { "GradientMagnitude", "ReferenceDiff", "LocalContrast" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be GradientMagnitude, ReferenceDiff or LocalContrast");
        }

        var minArea = GetIntParam(@operator, "MinArea", 20);
        var maxArea = GetIntParam(@operator, "MaxArea", 1_000_000);
        if (minArea < 0 || maxArea <= 0 || minArea > maxArea)
        {
            return ValidationResult.Invalid("Invalid MinArea/MaxArea range");
        }

        var alignmentMode = GetStringParam(@operator, "AlignmentMode", "PhaseCorrelation");
        var validAlignmentModes = new[] { "None", "PhaseCorrelation" };
        if (!validAlignmentModes.Contains(alignmentMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("AlignmentMode must be None or PhaseCorrelation");
        }

        var normalizationMode = GetStringParam(@operator, "NormalizationMode", "LocalMean");
        var validNormalizationModes = new[] { "None", "LocalMean" };
        if (!validNormalizationModes.Contains(normalizationMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("NormalizationMode must be None or LocalMean");
        }

        var thresholdMode = GetStringParam(@operator, "ThresholdMode", "Auto");
        var validThresholdModes = new[] { "Auto", "Manual", "Otsu", "ReferenceStats" };
        if (!validThresholdModes.Contains(thresholdMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ThresholdMode must be Auto, Manual, Otsu or ReferenceStats");
        }

        return ValidationResult.Valid();
    }

    private Mat BuildResponseMap(
        string method,
        Mat gray,
        Dictionary<string, object>? inputs,
        string alignmentMode,
        string normalizationMode,
        int backgroundKernelSize,
        out double alignmentScore,
        out Point2d alignmentShift,
        out string rejectedReason)
    {
        alignmentScore = 0;
        alignmentShift = new Point2d(0, 0);
        rejectedReason = string.Empty;

        switch (method.ToLowerInvariant())
        {
            case "gradientmagnitude":
            {
                using var normalized = NormalizeForComparison(gray, normalizationMode, backgroundKernelSize);
                using var gradX = new Mat();
                using var gradY = new Mat();
                using var magnitude = new Mat();
                Cv2.Sobel(normalized, gradX, MatType.CV_32FC1, 1, 0, 3);
                Cv2.Sobel(normalized, gradY, MatType.CV_32FC1, 0, 1, 3);
                Cv2.Magnitude(gradX, gradY, magnitude);
                var result = new Mat();
                magnitude.ConvertTo(result, MatType.CV_8UC1);
                return result;
            }

            case "referencediff":
            {
                if (!TryGetInputImage(inputs, "Reference", out var referenceWrapper) || referenceWrapper == null)
                {
                    throw new InvalidOperationException("Reference image is required in ReferenceDiff mode");
                }

                var reference = referenceWrapper.GetMat();
                if (reference.Empty())
                {
                    throw new InvalidOperationException("Reference image is invalid");
                }

                using var referenceGray = new Mat();
                if (reference.Channels() == 1)
                {
                    reference.CopyTo(referenceGray);
                }
                else
                {
                    Cv2.CvtColor(reference, referenceGray, ColorConversionCodes.BGR2GRAY);
                }

                using var resized = EnsureSize(referenceGray, gray.Size());
                using var aligned = AlignReferenceToSource(gray, resized, alignmentMode, out alignmentScore, out alignmentShift, out rejectedReason);
                using var normalizedSource = NormalizeForComparison(gray, normalizationMode, backgroundKernelSize);
                using var normalizedReference = NormalizeForComparison(aligned, normalizationMode, backgroundKernelSize);

                var result = new Mat();
                Cv2.Absdiff(normalizedSource, normalizedReference, result);
                return result;
            }

            case "localcontrast":
            {
                return NormalizeForComparison(gray, "LocalMean", backgroundKernelSize);
            }

            default:
                throw new InvalidOperationException("Unsupported defect detection method");
        }
    }

    private static Mat EnsureSize(Mat source, Size size)
    {
        if (source.Size() == size)
        {
            return source.Clone();
        }

        var resized = new Mat();
        Cv2.Resize(source, resized, size);
        return resized;
    }

    private static Mat NormalizeForComparison(Mat gray, string normalizationMode, int backgroundKernelSize)
    {
        if (normalizationMode.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return gray.Clone();
        }

        var kernelSize = backgroundKernelSize % 2 == 0 ? backgroundKernelSize + 1 : backgroundKernelSize;
        kernelSize = Math.Max(3, kernelSize);

        using var background = new Mat();
        Cv2.GaussianBlur(gray, background, new Size(kernelSize, kernelSize), 0);

        var normalized = new Mat();
        Cv2.Absdiff(gray, background, normalized);
        return normalized;
    }

    private static Mat AlignReferenceToSource(
        Mat sourceGray,
        Mat referenceGray,
        string alignmentMode,
        out double alignmentScore,
        out Point2d alignmentShift,
        out string rejectedReason)
    {
        alignmentScore = 1.0;
        alignmentShift = new Point2d(0, 0);
        rejectedReason = string.Empty;

        if (alignmentMode.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return referenceGray.Clone();
        }

        try
        {
            using var source32 = new Mat();
            using var reference32 = new Mat();
            sourceGray.ConvertTo(source32, MatType.CV_32FC1);
            referenceGray.ConvertTo(reference32, MatType.CV_32FC1);

            using var window = new Mat();
            var rawShift = Cv2.PhaseCorrelate(source32, reference32, window, out var response);
            // PhaseCorrelate reports the shift from source toward reference; invert it because we warp the reference onto the source.
            var shift = new Point2d(-rawShift.X, -rawShift.Y);
            alignmentScore = response;
            alignmentShift = shift;

            using var transform = new Mat(2, 3, MatType.CV_64FC1, Scalar.All(0));
            transform.Set(0, 0, 1.0);
            transform.Set(1, 1, 1.0);
            transform.Set(0, 2, shift.X);
            transform.Set(1, 2, shift.Y);

            var aligned = new Mat();
            Cv2.WarpAffine(referenceGray, aligned, transform, sourceGray.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);

            var shiftMagnitude = Math.Sqrt((shift.X * shift.X) + (shift.Y * shift.Y));
            var maxAcceptedShift = Math.Min(sourceGray.Width, sourceGray.Height) * MaxAcceptedShiftRatio;
            var baselineDifference = ComputeMeanAbsoluteDifference(sourceGray, referenceGray);
            var alignedDifference = ComputeMeanAbsoluteDifference(sourceGray, aligned);
            var improvementRatio = baselineDifference <= 1e-6
                ? 0.0
                : (baselineDifference - alignedDifference) / baselineDifference;
            var allowedDifferenceIncrease = Math.Max(2.0, baselineDifference * 0.12);

            if (shiftMagnitude > maxAcceptedShift)
            {
                rejectedReason =
                    $"PhaseCorrelation translation alignment rejected: estimated shift ({shift.X:F2}, {shift.Y:F2}) exceeds the supported translation range.";
            }
            else if (response < MinAcceptedPhaseCorrelationResponse)
            {
                rejectedReason =
                    $"PhaseCorrelation translation alignment rejected: response {response:F3} is below {MinAcceptedPhaseCorrelationResponse:F3}.";
            }
            else if (shiftMagnitude > 0.5 &&
                     alignedDifference > baselineDifference + allowedDifferenceIncrease &&
                     improvementRatio < MinAcceptedImprovementRatio)
            {
                rejectedReason =
                    $"PhaseCorrelation translation alignment rejected: translation-only alignment changed similarity by only {improvementRatio:P1}.";
            }

            if (!string.IsNullOrEmpty(rejectedReason))
            {
                aligned.Dispose();
                return referenceGray.Clone();
            }

            return aligned;
        }
        catch (Exception ex)
        {
            alignmentScore = 0.0;
            alignmentShift = new Point2d(0, 0);
            rejectedReason = $"Alignment failed: {ex.Message}";
            return referenceGray.Clone();
        }
    }

    private static double ComputeMeanAbsoluteDifference(Mat first, Mat second)
    {
        using var diff = new Mat();
        Cv2.Absdiff(first, second, diff);
        return Cv2.Mean(diff).Val0;
    }

    private static double ApplyThreshold(
        Mat response,
        Mat binary,
        string method,
        string thresholdMode,
        double manualThreshold,
        double referenceStatsSigma)
    {
        var resolvedMode = ResolveThresholdMode(method, thresholdMode);
        switch (resolvedMode.ToLowerInvariant())
        {
            case "manual":
                Cv2.Threshold(response, binary, manualThreshold, 255, ThresholdTypes.Binary);
                return manualThreshold;
            case "otsu":
                return Cv2.Threshold(response, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            case "referencestats":
                var computed = ComputeReferenceStatsThreshold(response, manualThreshold, referenceStatsSigma);
                Cv2.Threshold(response, binary, computed, 255, ThresholdTypes.Binary);
                return computed;
            default:
                Cv2.Threshold(response, binary, manualThreshold, 255, ThresholdTypes.Binary);
                return manualThreshold;
        }
    }

    private static string ResolveThresholdMode(string method, string thresholdMode)
    {
        if (!thresholdMode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return thresholdMode;
        }

        return method.Equals("ReferenceDiff", StringComparison.OrdinalIgnoreCase)
            ? "ReferenceStats"
            : "Otsu";
    }

    private static double ComputeReferenceStatsThreshold(Mat response, double manualFloor, double sigma)
    {
        Cv2.MeanStdDev(response, out var mean, out var stddev);
        var computed = mean.Val0 + (stddev.Val0 * sigma);
        return Math.Clamp(Math.Max(manualFloor, computed), 0.0, 255.0);
    }
}
