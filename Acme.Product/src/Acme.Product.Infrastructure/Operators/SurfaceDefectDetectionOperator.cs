// SurfaceDefectDetectionOperator.cs
// 表面缺陷检测算子
// 检测划痕、污点等表面缺陷并输出结果
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "表面缺陷检测",
    Description = "Detects surface defects using gradient, reference diff, or local contrast.",
    Category = "AI检测",
    IconName = "surface-defect",
    Keywords = new[] { "surface defect", "scratch", "stain", "traditional detection" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("Reference", "Reference", PortDataType.Image, IsRequired = false)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("DefectMask", "Defect Mask", PortDataType.Image)]
[OutputPort("DefectCount", "Defect Count", PortDataType.Integer)]
[OutputPort("DefectArea", "Defect Area", PortDataType.Float)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "GradientMagnitude", Options = new[] { "GradientMagnitude|GradientMagnitude", "ReferenceDiff|ReferenceDiff", "LocalContrast|LocalContrast" })]
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 35.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MinArea", "Min Area", "int", DefaultValue = 20, Min = 0, Max = 10000000)]
[OperatorParam("MaxArea", "Max Area", "int", DefaultValue = 1000000, Min = 0, Max = 10000000)]
[OperatorParam("MorphCleanSize", "Morph Clean Size", "int", DefaultValue = 3, Min = 1, Max = 301)]
public class SurfaceDefectDetectionOperator : OperatorBase
{
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
        var threshold = GetDoubleParam(@operator, "Threshold", 35.0, 0.0, 255.0);
        var minArea = GetIntParam(@operator, "MinArea", 20, 0, 10_000_000);
        var maxArea = GetIntParam(@operator, "MaxArea", 1_000_000, 0, 10_000_000);
        var cleanSize = GetIntParam(@operator, "MorphCleanSize", 3, 1, 301);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var response = BuildResponseMap(method, gray, inputs);
        if (response.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Failed to compute defect response map"));
        }

        using var binary = new Mat();
        Cv2.Threshold(response, binary, threshold, 255, ThresholdTypes.Binary);

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

        Cv2.PutText(
            resultImage,
            $"Defects:{defectCount} Area:{defectArea:F1}",
            new Point(8, 24),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(0, 255, 255),
            2);

        var additional = new Dictionary<string, object>
        {
            { "DefectMask", new ImageWrapper(defectMask) },
            { "DefectCount", defectCount },
            { "DefectArea", defectArea }
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

        return ValidationResult.Valid();
    }

    private Mat BuildResponseMap(string method, Mat gray, Dictionary<string, object>? inputs)
    {
        switch (method.ToLowerInvariant())
        {
            case "gradientmagnitude":
            {
                using var gradX = new Mat();
                using var gradY = new Mat();
                using var magnitude = new Mat();
                Cv2.Sobel(gray, gradX, MatType.CV_32FC1, 1, 0, 3);
                Cv2.Sobel(gray, gradY, MatType.CV_32FC1, 0, 1, 3);
                Cv2.Magnitude(gradX, gradY, magnitude);
                var result = new Mat();
                Cv2.Normalize(magnitude, magnitude, 0, 255, NormTypes.MinMax);
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

                using var resized = new Mat();
                if (referenceGray.Size() != gray.Size())
                {
                    Cv2.Resize(referenceGray, resized, gray.Size());
                }
                else
                {
                    referenceGray.CopyTo(resized);
                }

                var result = new Mat();
                Cv2.Absdiff(gray, resized, result);
                return result;
            }

            case "localcontrast":
            {
                using var background = new Mat();
                Cv2.GaussianBlur(gray, background, new Size(31, 31), 0);
                var result = new Mat();
                Cv2.Absdiff(gray, background, result);
                return result;
            }

            default:
                throw new InvalidOperationException("Unsupported defect detection method");
        }
    }
}

