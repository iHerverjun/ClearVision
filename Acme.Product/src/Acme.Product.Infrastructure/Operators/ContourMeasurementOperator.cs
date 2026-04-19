using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Contour Measurement",
    Description = "Measures contour area, perimeter, and centroid with grayscale-weighted area estimation.",
    Category = "Detection",
    IconName = "contour-measure",
    Keywords = new[] { "Contour", "Area", "Perimeter", "Shape", "Centroid" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("Area", "Area", PortDataType.Float)]
[OutputPort("Perimeter", "Perimeter", PortDataType.Float)]
[OutputPort("ContourCount", "Contour Count", PortDataType.Integer)]
[OperatorParam("Threshold", "Threshold", "double", DefaultValue = 127.0, Min = 0.0, Max = 255.0)]
[OperatorParam("MinArea", "Min Area", "int", DefaultValue = 100, Min = 0)]
[OperatorParam("MaxArea", "Max Area", "int", DefaultValue = 100000, Min = 0)]
[OperatorParam("SortBy", "Sort By", "enum", DefaultValue = "Area", Options = new[] { "Area|Area", "Perimeter|Perimeter" })]
public class ContourMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ContourMeasurement;

    public ContourMeasurementOperator(ILogger<ContourMeasurementOperator> logger) : base(logger)
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

        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var minArea = GetDoubleParam(@operator, "MinArea", 100.0, min: 0);
        var maxArea = GetDoubleParam(@operator, "MaxArea", 100000.0, min: minArea);
        var sortBy = GetStringParam(@operator, "SortBy", "Area");

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        var resultImage = src.Clone();
        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);
        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.Tree, ContourApproximationModes.ApproxNone);

        var contourResults = new List<Dictionary<string, object>>();
        for (var i = 0; i < contours.Length; i++)
        {
            var contour = contours[i];
            if (contour.Length == 0)
            {
                continue;
            }

            var boundingRect = Cv2.BoundingRect(contour);
            if (boundingRect.Width <= 0 || boundingRect.Height <= 0)
            {
                continue;
            }

            using var contourMask = new Mat(gray.Rows, gray.Cols, MatType.CV_8UC1, Scalar.Black);
            Cv2.DrawContours(contourMask, contours, i, Scalar.White, -1);

            using var maskedGray = new Mat();
            gray.CopyTo(maskedGray, contourMask);
            var weightedMoments = Cv2.Moments(maskedGray, false);
            var area = weightedMoments.M00 / 255.0;
            if (area < minArea || area > maxArea)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, true);
            var centerX = Math.Abs(weightedMoments.M00) > 1e-9
                ? weightedMoments.M10 / weightedMoments.M00
                : boundingRect.X + (boundingRect.Width / 2.0);
            var centerY = Math.Abs(weightedMoments.M00) > 1e-9
                ? weightedMoments.M01 / weightedMoments.M00
                : boundingRect.Y + (boundingRect.Height / 2.0);
            var contourPointCount = contour.Length;
            var localizationSigmaPx = EstimateContourUncertaintyPx(contourPointCount, perimeter);
            var rectArea = boundingRect.Width * boundingRect.Height;
            var extent = rectArea > 0 ? area / rectArea : 0.0;
            var circularity = perimeter > 0 ? 4.0 * Math.PI * area / (perimeter * perimeter) : 0.0;
            var equivalentDiameter = area > 0 ? Math.Sqrt((4.0 * area) / Math.PI) : 0.0;

            Cv2.DrawContours(resultImage, contours, i, new Scalar(0, 255, 0), 2);
            Cv2.Circle(
                resultImage,
                new Point((int)Math.Round(centerX), (int)Math.Round(centerY)),
                3,
                new Scalar(0, 0, 255),
                -1);
            Cv2.Rectangle(resultImage, boundingRect, new Scalar(255, 0, 0), 1);

            contourResults.Add(new Dictionary<string, object>
            {
                ["Index"] = i,
                ["Area"] = area,
                ["Perimeter"] = perimeter,
                ["CenterX"] = centerX,
                ["CenterY"] = centerY,
                ["BoundingRect"] = $"{boundingRect.X},{boundingRect.Y},{boundingRect.Width},{boundingRect.Height}",
                ["Circularity"] = circularity,
                ["Extent"] = extent,
                ["ContourPointCount"] = contourPointCount,
                ["EquivalentDiameter"] = equivalentDiameter,
                ["UncertaintyPx"] = localizationSigmaPx
            });
        }

        contourResults = sortBy.Equals("Perimeter", StringComparison.OrdinalIgnoreCase)
            ? contourResults.OrderByDescending(result => Convert.ToDouble(result["Perimeter"])).ToList()
            : contourResults.OrderByDescending(result => Convert.ToDouble(result["Area"])).ToList();

        var additionalData = new Dictionary<string, object>
        {
            ["ContourCount"] = contourResults.Count,
            ["Contours"] = contourResults
        };

        var firstContour = contourResults.FirstOrDefault();
        if (firstContour != null)
        {
            foreach (var (key, value) in firstContour)
            {
                additionalData[key] = value;
            }
        }

        var hasFeature = contourResults.Count > 0;
        var reportedUncertaintyPx = firstContour != null && firstContour.TryGetValue("UncertaintyPx", out var contourUncertainty)
            ? Convert.ToDouble(contourUncertainty)
            : double.NaN;
        additionalData["StatusCode"] = hasFeature ? "OK" : "NoFeature";
        additionalData["StatusMessage"] = hasFeature ? "Success" : "No contour found";
        additionalData["Confidence"] = hasFeature && double.IsFinite(reportedUncertaintyPx)
            ? 1.0 / (1.0 + reportedUncertaintyPx)
            : 0.0;
        additionalData["UncertaintyPx"] = hasFeature ? reportedUncertaintyPx : double.NaN;

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0);
        var minArea = GetDoubleParam(@operator, "MinArea", 100.0);
        var maxArea = GetDoubleParam(@operator, "MaxArea", 100000.0);

        if (threshold < 0 || threshold > 255)
        {
            return ValidationResult.Invalid("Threshold must be within [0, 255].");
        }

        if (minArea < 0)
        {
            return ValidationResult.Invalid("MinArea must be non-negative.");
        }

        if (maxArea < minArea)
        {
            return ValidationResult.Invalid("MaxArea must be greater than or equal to MinArea.");
        }

        return ValidationResult.Valid();
    }

    private static double EstimateContourUncertaintyPx(int contourPointCount, double perimeter)
    {
        var effectiveSamples = Math.Max(contourPointCount, (int)Math.Ceiling(Math.Max(perimeter, 0.0)));
        if (effectiveSamples <= 0)
        {
            return double.NaN;
        }

        var quantizationSigmaPx = 0.5 / Math.Sqrt(effectiveSamples);
        return Math.Clamp(quantizationSigmaPx, 0.01, 0.2);
    }
}
