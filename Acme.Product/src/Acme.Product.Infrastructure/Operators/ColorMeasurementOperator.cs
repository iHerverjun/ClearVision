using System.Collections;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "颜色测量",
    Description = "Measures average Lab/HSV values and computes DeltaE.",
    Category = "颜色处理",
    IconName = "color-measure",
    Keywords = new[] { "color", "deltaE", "lab", "hsv" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("ReferenceColor", "Reference Color", PortDataType.Any, IsRequired = false)]
[OutputPort("L", "L", PortDataType.Float)]
[OutputPort("A", "A", PortDataType.Float)]
[OutputPort("B", "B", PortDataType.Float)]
[OutputPort("H", "H", PortDataType.Float)]
[OutputPort("S", "S", PortDataType.Float)]
[OutputPort("V", "V", PortDataType.Float)]
[OutputPort("DeltaE", "DeltaE", PortDataType.Float)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("ColorSpace", "Color Space", "enum", DefaultValue = "Lab", Options = new[] { "Lab|Lab", "HSV|HSV" })]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0)]
[OperatorParam("RoiW", "ROI W", "int", DefaultValue = 0)]
[OperatorParam("RoiH", "ROI H", "int", DefaultValue = 0)]
[OperatorParam("RefL", "Ref L", "double", DefaultValue = 0.0)]
[OperatorParam("RefA", "Ref A", "double", DefaultValue = 0.0)]
[OperatorParam("RefB", "Ref B", "double", DefaultValue = 0.0)]
public class ColorMeasurementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ColorMeasurement;

    public ColorMeasurementOperator(ILogger<ColorMeasurementOperator> logger) : base(logger)
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

        var colorSpace = GetStringParam(@operator, "ColorSpace", "Lab");

        var roiX = GetIntParam(@operator, "RoiX", 0, 0, Math.Max(0, src.Width - 1));
        var roiY = GetIntParam(@operator, "RoiY", 0, 0, Math.Max(0, src.Height - 1));
        var roiW = GetIntParam(@operator, "RoiW", 0, 0, src.Width);
        var roiH = GetIntParam(@operator, "RoiH", 0, 0, src.Height);

        if (roiW <= 0)
        {
            roiW = src.Width - roiX;
        }

        if (roiH <= 0)
        {
            roiH = src.Height - roiY;
        }

        var roi = ClampRect(new Rect(roiX, roiY, roiW, roiH), src.Width, src.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid"));
        }

        using var lab = new Mat();
        using var hsv = new Mat();
        Cv2.CvtColor(src, lab, ColorConversionCodes.BGR2Lab);
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);

        using var labRoi = new Mat(lab, roi);
        using var hsvRoi = new Mat(hsv, roi);

        var labMean = Cv2.Mean(labRoi);
        var hsvMean = Cv2.Mean(hsvRoi);

        var lValue = labMean.Val0;
        var aValue = labMean.Val1;
        var bValue = labMean.Val2;

        var hValue = hsvMean.Val0;
        var sValue = hsvMean.Val1;
        var vValue = hsvMean.Val2;

        var refL = GetDoubleParam(@operator, "RefL", lValue);
        var refA = GetDoubleParam(@operator, "RefA", aValue);
        var refB = GetDoubleParam(@operator, "RefB", bValue);

        if (inputs != null && inputs.TryGetValue("ReferenceColor", out var referenceObj))
        {
            TryOverrideReference(referenceObj, ref refL, ref refA, ref refB);
        }

        var deltaE = Math.Sqrt(
            (lValue - refL) * (lValue - refL) +
            (aValue - refA) * (aValue - refA) +
            (bValue - refB) * (bValue - refB));

        var resultImage = src.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);
        Cv2.PutText(
            resultImage,
            $"DeltaE:{deltaE:F2}",
            new Point(roi.X, Math.Max(20, roi.Y - 5)),
            HersheyFonts.HersheySimplex,
            0.6,
            new Scalar(0, 255, 255),
            2);

        var output = new Dictionary<string, object>
        {
            { "L", lValue },
            { "A", aValue },
            { "B", bValue },
            { "H", hValue },
            { "S", sValue },
            { "V", vValue },
            { "DeltaE", deltaE },
            { "ColorSpace", colorSpace }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var colorSpace = GetStringParam(@operator, "ColorSpace", "Lab");
        var validSpaces = new[] { "Lab", "HSV" };
        if (!validSpaces.Contains(colorSpace, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ColorSpace must be Lab or HSV");
        }

        var roiW = GetIntParam(@operator, "RoiW", 0);
        var roiH = GetIntParam(@operator, "RoiH", 0);
        if (roiW < 0 || roiH < 0)
        {
            return ValidationResult.Invalid("RoiW/RoiH must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        var w = Math.Clamp(rect.Width, 0, width - x);
        var h = Math.Clamp(rect.Height, 0, height - y);
        return new Rect(x, y, w, h);
    }

    private static void TryOverrideReference(object? referenceObj, ref double refL, ref double refA, ref double refB)
    {
        if (referenceObj == null)
        {
            return;
        }

        if (referenceObj is double[] arr && arr.Length >= 3)
        {
            refL = arr[0];
            refA = arr[1];
            refB = arr[2];
            return;
        }

        if (referenceObj is float[] floatArr && floatArr.Length >= 3)
        {
            refL = floatArr[0];
            refA = floatArr[1];
            refB = floatArr[2];
            return;
        }

        if (referenceObj is IDictionary<string, object> dict)
        {
            if (TryGetDouble(dict, "L", out var l))
            {
                refL = l;
            }

            if (TryGetDouble(dict, "A", out var a))
            {
                refA = a;
            }

            if (TryGetDouble(dict, "B", out var b))
            {
                refB = b;
            }
            return;
        }

        if (referenceObj is IDictionary legacy)
        {
            var normalized = legacy.Cast<DictionaryEntry>()
                .Where(e => e.Key != null)
                .ToDictionary(e => e.Key!.ToString() ?? string.Empty, e => e.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            TryOverrideReference(normalized, ref refL, ref refA, ref refB);
        }
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        if (!dict.TryGetValue(key, out var raw) || raw == null)
        {
            return false;
        }

        return raw switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(raw.ToString(), out value)
        };
    }
}
