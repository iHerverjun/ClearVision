using System.Collections;
using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "颜色测量",
    Description = "Measures Lab delta-E or HSV statistics over a selected ROI.",
    Category = "颜色处理",
    IconName = "color-measure",
    Keywords = new[] { "color", "deltaE", "lab", "hsv" },
    Version = "2.0.0"
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[InputPort("ReferenceColor", "Reference Color", PortDataType.Any, IsRequired = false)]
[OutputPort("LabMean", "Lab Mean", PortDataType.Any)]
[OutputPort("ReferenceLab", "Reference Lab", PortDataType.Any)]
[OutputPort("DeltaE", "DeltaE", PortDataType.Float)]
[OutputPort("HueMean", "Hue Mean", PortDataType.Float)]
[OutputPort("SaturationMean", "Saturation Mean", PortDataType.Float)]
[OutputPort("ValueMean", "Value Mean", PortDataType.Float)]
[OutputPort("HueValid", "Hue Valid", PortDataType.Boolean)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OperatorParam("MeasurementMode", "Measurement Mode", "enum", DefaultValue = "LabDeltaE", Options = new[] { "LabDeltaE|Lab DeltaE", "HsvStats|HSV Stats" })]
[OperatorParam("DeltaEMethod", "DeltaE Method", "enum", DefaultValue = "CIEDE2000", Options = new[] { "CIE76|CIE76", "CIEDE2000|CIEDE2000" })]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0)]
[OperatorParam("RoiW", "ROI W", "int", DefaultValue = 0)]
[OperatorParam("RoiH", "ROI H", "int", DefaultValue = 0)]
[OperatorParam("RefL", "Ref L", "double", DefaultValue = 0.0)]
[OperatorParam("RefA", "Ref A", "double", DefaultValue = 0.0)]
[OperatorParam("RefB", "Ref B", "double", DefaultValue = 0.0)]
public class ColorMeasurementOperator : OperatorBase
{
    private const double MinHueSaturation = 12.0;
    private const double MinHueValue = 12.0;

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

        var measurementMode = ResolveMeasurementMode(@operator);
        if (measurementMode == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("MeasurementMode must be LabDeltaE or HsvStats"));
        }

        using var colorSource = EnsureColorImage(src);
        var roi = MeasurementRoiHelper.ResolveRoi(@operator, colorSource.Width, colorSource.Height);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid"));
        }

        using var roiMat = new Mat(colorSource, roi);
        var resultImage = colorSource.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);

        Dictionary<string, object> output;
        if (measurementMode == "LabDeltaE")
        {
            output = MeasureLabDeltaE(@operator, inputs, roiMat);
            Cv2.PutText(
                resultImage,
                $"DeltaE:{Convert.ToDouble(output["DeltaE"]):F2}",
                new Point(roi.X, Math.Max(20, roi.Y - 5)),
                HersheyFonts.HersheySimplex,
                0.6,
                new Scalar(0, 255, 255),
                2);
        }
        else
        {
            output = MeasureHsvStats(roiMat);
            var hueLabel = Convert.ToBoolean(output["HueValid"])
                ? $"H:{Convert.ToDouble(output["HueMean"]):F1}deg"
                : "H:invalid";
            Cv2.PutText(
                resultImage,
                hueLabel,
                new Point(roi.X, Math.Max(20, roi.Y - 5)),
                HersheyFonts.HersheySimplex,
                0.6,
                new Scalar(0, 255, 255),
                2);
        }

        output["MeasurementMode"] = measurementMode;
        output["StatusCode"] = "OK";
        output["StatusMessage"] = "Success";
        var measurementUncertainty = TryReadMeasurementUncertainty(output);
        output["Confidence"] = MeasurementStatisticsHelper.ComputeConfidenceFromUncertainty(measurementUncertainty);
        output["UncertaintyPx"] = measurementUncertainty;

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        if (ResolveMeasurementMode(@operator) == null)
        {
            return ValidationResult.Invalid("MeasurementMode must be LabDeltaE or HsvStats");
        }

        var deltaEMethod = GetStringParam(@operator, "DeltaEMethod", "CIEDE2000");
        var validMethods = new[] { "CIE76", "CIEDE2000" };
        if (!validMethods.Contains(deltaEMethod, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("DeltaEMethod must be CIE76 or CIEDE2000");
        }

        var roiW = MeasurementRoiHelper.ReadIntParameter(@operator, "RoiW", 0);
        var roiH = MeasurementRoiHelper.ReadIntParameter(@operator, "RoiH", 0);
        if (roiW < 0 || roiH < 0)
        {
            return ValidationResult.Invalid("RoiW/RoiH must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static string? ResolveMeasurementMode(Operator @operator)
    {
        var measurementMode = GetOptionalParameter(@operator, "MeasurementMode");
        if (measurementMode != null)
        {
            return measurementMode switch
            {
                "LabDeltaE" => "LabDeltaE",
                "HsvStats" => "HsvStats",
                _ => null
            };
        }

        // Read-only migration path for historical flows.
        var legacyColorSpace = GetOptionalParameter(@operator, "ColorSpace");
        return legacyColorSpace switch
        {
            null => "LabDeltaE",
            "Lab" => "LabDeltaE",
            "HSV" => "HsvStats",
            _ => null
        };
    }

    private static string? GetOptionalParameter(Operator @operator, string name)
    {
        var raw = @operator.Parameters.FirstOrDefault(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
        return raw?.Trim();
    }

    private static Mat EnsureColorImage(Mat src)
    {
        if (src.Channels() == 3)
        {
            return src.Clone();
        }

        var color = new Mat();
        Cv2.CvtColor(src, color, ColorConversionCodes.GRAY2BGR);
        return color;
    }

    private Dictionary<string, object> MeasureLabDeltaE(Operator @operator, Dictionary<string, object>? inputs, Mat roiMat)
    {
        var labStats = ComputeLabStatistics(roiMat);
        var lValue = labStats.Mean.L;
        var aValue = labStats.Mean.A;
        var bValue = labStats.Mean.B;

        var refL = GetDoubleParam(@operator, "RefL", lValue);
        var refA = GetDoubleParam(@operator, "RefA", aValue);
        var refB = GetDoubleParam(@operator, "RefB", bValue);
        if (inputs != null && inputs.TryGetValue("ReferenceColor", out var referenceObj))
        {
            TryOverrideReferenceLab(referenceObj, ref refL, ref refA, ref refB);
        }

        var labValue = new CieLab(lValue, aValue, bValue);
        var referenceValue = new CieLab(refL, refA, refB);
        var deltaEMethod = GetStringParam(@operator, "DeltaEMethod", "CIEDE2000");
        var deltaE = deltaEMethod.Equals("CIE76", StringComparison.OrdinalIgnoreCase)
            ? ColorDifference.DeltaE76(labValue, referenceValue)
            : ColorDifference.DeltaE00(labValue, referenceValue);
        var deltaESamples = ComputeDeltaESamples(roiMat, referenceValue, deltaEMethod);
        var deltaEMean = deltaESamples.Count > 0 ? deltaESamples.Average() : deltaE;
        var deltaEStdDev = deltaESamples.Count > 0
            ? MeasurementStatisticsHelper.ComputePopulationStdDev(deltaESamples, deltaEMean)
            : 0.0;
        var deltaEStdError = MeasurementStatisticsHelper.ComputeStandardError(deltaEStdDev, deltaESamples.Count);

        return new Dictionary<string, object>
        {
            { "LabMean", new Dictionary<string, object> { { "L", lValue }, { "A", aValue }, { "B", bValue } } },
            { "LabStdDev", new Dictionary<string, object> { { "L", labStats.StdDev.L }, { "A", labStats.StdDev.A }, { "B", labStats.StdDev.B } } },
            { "ReferenceLab", new Dictionary<string, object> { { "L", refL }, { "A", refA }, { "B", refB } } },
            { "DeltaE", deltaE },
            { "DeltaEStdDev", deltaEStdDev },
            { "DeltaEStdError", deltaEStdError },
            { "SampleCount", labStats.SampleCount },
            { "HueMean", double.NaN },
            { "SaturationMean", double.NaN },
            { "ValueMean", double.NaN },
            { "HueValid", false },
            { "MeasurementUncertainty", deltaEStdError }
        };
    }

    private static Dictionary<string, object> MeasureHsvStats(Mat roiMat)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(roiMat, hsv, ColorConversionCodes.BGR2HSV);

        var mean = Cv2.Mean(hsv);
        var saturationMean = mean.Val1 * (100.0 / 255.0);
        var valueMean = mean.Val2 * (100.0 / 255.0);

        var hueAnglesDegrees = new List<double>(hsv.Rows * hsv.Cols);
        for (var y = 0; y < hsv.Rows; y++)
        {
            for (var x = 0; x < hsv.Cols; x++)
            {
                var pixel = hsv.At<Vec3b>(y, x);
                if (pixel.Item1 < MinHueSaturation || pixel.Item2 < MinHueValue)
                {
                    continue;
                }

                hueAnglesDegrees.Add(pixel.Item0 * 2.0);
            }
        }

        if (hueAnglesDegrees.Count == 0)
        {
            return new Dictionary<string, object>
            {
                { "LabMean", new Dictionary<string, object>() },
                { "ReferenceLab", new Dictionary<string, object>() },
                { "DeltaE", double.NaN },
                { "HueMean", double.NaN },
                { "HueCircularStdDeg", double.NaN },
                { "HueStdErrorDeg", double.NaN },
                { "SaturationMean", saturationMean },
                { "ValueMean", valueMean },
                { "HueValid", false },
                { "SampleCount", 0 },
                { "MeasurementUncertainty", double.NaN }
            };
        }

        var (hueMean, hueStdDev) = MeasurementStatisticsHelper.ComputeCircularStatisticsDegrees(hueAnglesDegrees);
        var hueStdError = MeasurementStatisticsHelper.ComputeStandardError(hueStdDev, hueAnglesDegrees.Count);

        return new Dictionary<string, object>
        {
            { "LabMean", new Dictionary<string, object>() },
            { "ReferenceLab", new Dictionary<string, object>() },
            { "DeltaE", double.NaN },
            { "HueMean", hueMean },
            { "HueCircularStdDeg", hueStdDev },
            { "HueStdErrorDeg", hueStdError },
            { "SaturationMean", saturationMean },
            { "ValueMean", valueMean },
            { "HueValid", true },
            { "SampleCount", hueAnglesDegrees.Count },
            { "MeasurementUncertainty", hueStdError }
        };
    }

    private static void TryOverrideReferenceLab(object? referenceObj, ref double refL, ref double refA, ref double refB)
    {
        if (referenceObj == null)
        {
            return;
        }

        if (referenceObj is double[] doubles && doubles.Length >= 3)
        {
            refL = doubles[0];
            refA = doubles[1];
            refB = doubles[2];
            return;
        }

        if (referenceObj is float[] floats && floats.Length >= 3)
        {
            refL = floats[0];
            refA = floats[1];
            refB = floats[2];
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
                .Where(entry => entry.Key != null)
                .ToDictionary(entry => entry.Key!.ToString() ?? string.Empty, entry => entry.Value ?? 0.0, StringComparer.OrdinalIgnoreCase);
            TryOverrideReferenceLab(normalized, ref refL, ref refA, ref refB);
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

    private static double TryReadMeasurementUncertainty(IReadOnlyDictionary<string, object> output)
    {
        if (output.TryGetValue("MeasurementUncertainty", out var raw) &&
            raw != null &&
            double.TryParse(raw.ToString(), out var parsed) &&
            double.IsFinite(parsed))
        {
            return parsed;
        }

        return double.NaN;
    }

    private static LabStatistics ComputeLabStatistics(Mat roiMat)
    {
        var indexer = roiMat.GetGenericIndexer<Vec3b>();
        var lValues = new List<double>(roiMat.Rows * roiMat.Cols);
        var aValues = new List<double>(roiMat.Rows * roiMat.Cols);
        var bValues = new List<double>(roiMat.Rows * roiMat.Cols);

        for (var y = 0; y < roiMat.Rows; y++)
        {
            for (var x = 0; x < roiMat.Cols; x++)
            {
                var pixel = indexer[y, x];
                var lab = CieLabConverter.BgrToLab(pixel.Item0, pixel.Item1, pixel.Item2);
                lValues.Add(lab.L);
                aValues.Add(lab.A);
                bValues.Add(lab.B);
            }
        }

        var mean = new CieLab(
            lValues.Count > 0 ? lValues.Average() : 0.0,
            aValues.Count > 0 ? aValues.Average() : 0.0,
            bValues.Count > 0 ? bValues.Average() : 0.0);
        var stdDev = new CieLab(
            MeasurementStatisticsHelper.ComputePopulationStdDev(lValues, mean.L),
            MeasurementStatisticsHelper.ComputePopulationStdDev(aValues, mean.A),
            MeasurementStatisticsHelper.ComputePopulationStdDev(bValues, mean.B));

        return new LabStatistics(mean, stdDev, lValues.Count);
    }

    private static List<double> ComputeDeltaESamples(Mat roiMat, CieLab reference, string deltaEMethod)
    {
        var deltaESamples = new List<double>(roiMat.Rows * roiMat.Cols);
        var indexer = roiMat.GetGenericIndexer<Vec3b>();
        var useCie76 = deltaEMethod.Equals("CIE76", StringComparison.OrdinalIgnoreCase);

        for (var y = 0; y < roiMat.Rows; y++)
        {
            for (var x = 0; x < roiMat.Cols; x++)
            {
                var pixel = indexer[y, x];
                var lab = CieLabConverter.BgrToLab(pixel.Item0, pixel.Item1, pixel.Item2);
                deltaESamples.Add(useCie76
                    ? ColorDifference.DeltaE76(lab, reference)
                    : ColorDifference.DeltaE00(lab, reference));
            }
        }

        return deltaESamples;
    }

    private readonly record struct LabStatistics(CieLab Mean, CieLab StdDev, int SampleCount);
}
