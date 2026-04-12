using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.Calibration;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Coordinate Transform",
    Description = "Converts pixel coordinates to physical coordinates using CalibrationBundleV2 Transform2D.",
    Category = "Calibration",
    IconName = "coordinate-transform",
    Keywords = new[] { "coordinate", "pixel", "physical", "calibration", "transform2d" }
)]
[InputPort("Image", "Input Image", PortDataType.Image, IsRequired = false)]
[InputPort("PixelX", "Pixel X", PortDataType.Float, IsRequired = false)]
[InputPort("PixelY", "Pixel Y", PortDataType.Float, IsRequired = false)]
[InputPort("CalibrationData", "Calibration Bundle V2 JSON", PortDataType.String, IsRequired = false)]
[OutputPort("Image", "Result Image", PortDataType.Image)]
[OutputPort("PhysicalX", "Physical X", PortDataType.Float)]
[OutputPort("PhysicalY", "Physical Y", PortDataType.Float)]
[OperatorParam("PixelX", "Pixel X", "double", DefaultValue = 0.0)]
[OperatorParam("PixelY", "Pixel Y", "double", DefaultValue = 0.0)]
public class CoordinateTransformOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CoordinateTransform;

    public CoordinateTransformOperator(ILogger<CoordinateTransformOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var pixelX = GetParameterOrInput(inputs, @operator, "PixelX", 0.0);
        var pixelY = GetParameterOrInput(inputs, @operator, "PixelY", 0.0);

        if (!TryResolveCalibrationData(@operator, inputs, out var calibrationJson))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("CalibrationBundleV2 data is required. Provide CalibrationData input or inline CalibrationData parameter."));
        }

        if (!CalibrationBundleV2Json.TryDeserialize(calibrationJson!, out var bundle, out var parseError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Invalid CalibrationBundleV2: {parseError}"));
        }

        if (!CalibrationBundleV2Json.TryRequireAccepted(bundle, out var acceptedError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(acceptedError));
        }

        if (!IsSupportedKind(bundle.CalibrationKind))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                $"Unsupported CalibrationKind for CoordinateTransform: {bundle.CalibrationKind}."));
        }

        if (!CalibrationPlanarTransformRuntime.TryCreate(
                bundle,
                new[] { TransformModelV2.ScaleOffset, TransformModelV2.Similarity, TransformModelV2.Affine, TransformModelV2.Homography },
                out var runtime,
                out var runtimeError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(runtimeError));
        }

        if (!runtime.TryApplyForward(pixelX, pixelY, out var physicalX, out var physicalY, out var transformError))
        {
            return Task.FromResult(OperatorExecutionOutput.Failure($"Coordinate transform failed: {transformError}"));
        }

        var outputData = new Dictionary<string, object>
        {
            ["PixelX"] = pixelX,
            ["PixelY"] = pixelY,
            ["PhysicalX"] = physicalX,
            ["PhysicalY"] = physicalY,
            ["TransformModel"] = runtime.Model.ToString(),
            ["CalibrationKind"] = bundle.CalibrationKind.ToString(),
            ["SourceFrame"] = bundle.SourceFrame,
            ["TargetFrame"] = bundle.TargetFrame,
            ["Unit"] = bundle.Unit
        };

        if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
        {
            var src = imageWrapper.GetMat();
            if (src.Empty())
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
            }

            var resultImage = src.Clone();
            DrawOverlay(resultImage, pixelX, pixelY, physicalX, physicalY, runtime.Model.ToString(), bundle.Unit);
            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, outputData)));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(outputData));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var pixelX = GetDoubleParam(@operator, "PixelX", 0.0);
        var pixelY = GetDoubleParam(@operator, "PixelY", 0.0);

        if (!double.IsFinite(pixelX) || !double.IsFinite(pixelY))
        {
            return ValidationResult.Invalid("PixelX and PixelY must be finite numbers.");
        }

        return ValidationResult.Valid();
    }

    private static bool IsSupportedKind(CalibrationKindV2 kind)
    {
        return kind == CalibrationKindV2.PlanarTransform2D || kind == CalibrationKindV2.RigidTransform2D;
    }

    private static void DrawOverlay(
        Mat image,
        double pixelX,
        double pixelY,
        double physicalX,
        double physicalY,
        string model,
        string unit)
    {
        var center = new Point((int)Math.Round(pixelX), (int)Math.Round(pixelY));
        Cv2.Circle(image, center, 5, new Scalar(0, 0, 255), -1);
        Cv2.Circle(image, center, 11, new Scalar(0, 255, 0), 2);

        Cv2.PutText(
            image,
            $"Pixel: ({pixelX:F2}, {pixelY:F2})",
            new Point(10, 30),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);
        Cv2.PutText(
            image,
            $"Physical: ({physicalX:F4}, {physicalY:F4}) {unit}",
            new Point(10, 55),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);
        Cv2.PutText(
            image,
            $"Model: {model}",
            new Point(10, 80),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(255, 255, 255),
            2);
    }

    private bool TryResolveCalibrationData(
        Operator @operator,
        Dictionary<string, object>? inputs,
        out string? calibrationData)
    {
        calibrationData = null;

        if (inputs != null &&
            inputs.TryGetValue("CalibrationData", out var data) &&
            data is string inlineData &&
            !string.IsNullOrWhiteSpace(inlineData))
        {
            calibrationData = inlineData;
            return true;
        }

        var inlineParameterData = GetStringParam(@operator, "CalibrationData", string.Empty);
        if (!string.IsNullOrWhiteSpace(inlineParameterData))
        {
            calibrationData = inlineParameterData;
            return true;
        }

        return false;
    }

    private double GetParameterOrInput(
        Dictionary<string, object>? inputs,
        Operator @operator,
        string paramName,
        double defaultValue)
    {
        if (inputs != null && inputs.TryGetValue(paramName, out var value) && value != null)
        {
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                // Fall through and read parameter value.
            }
        }

        return GetDoubleParam(@operator, paramName, defaultValue);
    }
}
