using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Infrastructure.ImageProcessing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "颜色检测",
    Description = "Supports compatibility color analysis plus HSV inspection and Lab DeltaE inspection.",
    Category = "颜色处理",
    IconName = "color",
    Tags = new[] { "experimental", "industrial-remediation", "color-inspection" },
    Version = "2.0.0"
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[InputPort("ReferenceColor", "Reference Color", PortDataType.Any, IsRequired = false)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("ColorInfo", "颜色信息", PortDataType.Any)]
[OutputPort("AnalysisMode", "分析模式", PortDataType.String)]
[OutputPort("ColorSpace", "颜色空间", PortDataType.String)]
[OutputPort("DeltaE", "DeltaE", PortDataType.Float)]
[OutputPort("Coverage", "Coverage", PortDataType.Float)]
[OutputPort("WhiteBalanceStatus", "White Balance Status", PortDataType.String)]
[OutputPort("MeanColor", "Mean Color", PortDataType.Any)]
[OutputPort("DominantColors", "Dominant Colors", PortDataType.Any)]
[OutputPort("Diagnostics", "Diagnostics", PortDataType.Any)]
[OperatorParam("ColorSpace", "颜色空间", "enum", DefaultValue = "HSV", Options = new[] { "HSV|HSV", "Lab|Lab" })]
[OperatorParam("AnalysisMode", "分析模式", "enum", DefaultValue = "Average", Options = new[] { "Average|Legacy Average", "Dominant|Legacy Dominant", "Range|Legacy Range", "HsvInspection|HSV Inspection", "LabDeltaE|Lab DeltaE" })]
[OperatorParam("HueLow", "H下限", "int", DefaultValue = 0, Min = 0, Max = 180)]
[OperatorParam("HueHigh", "H上限", "int", DefaultValue = 180, Min = 0, Max = 180)]
[OperatorParam("SatLow", "S下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("SatHigh", "S上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
[OperatorParam("ValLow", "V下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("ValHigh", "V上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
[OperatorParam("DominantK", "主色数量K", "int", DefaultValue = 3, Min = 1, Max = 10)]
[OperatorParam("DeltaEMethod", "DeltaE Method", "enum", DefaultValue = "CIEDE2000", Options = new[] { "CIE76|CIE76", "CIEDE2000|CIEDE2000" })]
[OperatorParam("RefL", "Ref L", "double", DefaultValue = 0.0)]
[OperatorParam("RefA", "Ref A", "double", DefaultValue = 0.0)]
[OperatorParam("RefB", "Ref B", "double", DefaultValue = 0.0)]
[OperatorParam("RoiX", "ROI X", "int", DefaultValue = 0)]
[OperatorParam("RoiY", "ROI Y", "int", DefaultValue = 0)]
[OperatorParam("RoiW", "ROI W", "int", DefaultValue = 0)]
[OperatorParam("RoiH", "ROI H", "int", DefaultValue = 0)]
[OperatorParam("WhiteBalanceTolerance", "White Balance Tolerance", "double", DefaultValue = 12.0, Min = 0.0, Max = 255.0)]
public class ColorDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ColorDetection;

    public ColorDetectionOperator(ILogger<ColorDetectionOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is required."));
        }

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input image is invalid."));
        }

        using var bgr = EnsureBgr(src);
        var roi = ResolveRoi(@operator, bgr);
        if (roi.Width <= 0 || roi.Height <= 0)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("ROI is invalid."));
        }

        var colorSpace = GetStringParam(@operator, "ColorSpace", "HSV");
        var analysisMode = NormalizeAnalysisMode(GetStringParam(@operator, "AnalysisMode", "Average"));
        var hueLow = GetIntParam(@operator, "HueLow", 0, 0, 180);
        var hueHigh = GetIntParam(@operator, "HueHigh", 180, 0, 180);
        var satLow = GetIntParam(@operator, "SatLow", 50, 0, 255);
        var satHigh = GetIntParam(@operator, "SatHigh", 255, 0, 255);
        var valLow = GetIntParam(@operator, "ValLow", 50, 0, 255);
        var valHigh = GetIntParam(@operator, "ValHigh", 255, 0, 255);
        var dominantK = GetIntParam(@operator, "DominantK", 3, 1, 10);
        var deltaEMethod = GetStringParam(@operator, "DeltaEMethod", "CIEDE2000");
        var whiteBalanceTolerance = GetDoubleParam(@operator, "WhiteBalanceTolerance", 12.0, 0.0, 255.0);

        var result = analysisMode switch
        {
            "LabDeltaE" => AnalyzeLabDeltaE(bgr, roi, @operator, inputs, deltaEMethod, whiteBalanceTolerance),
            "HsvInspection" => AnalyzeHsvInspection(bgr, roi, hueLow, hueHigh, satLow, satHigh, valLow, valHigh, whiteBalanceTolerance),
            "Dominant" => AnalyzeDominantColors(bgr, roi, dominantK, whiteBalanceTolerance),
            "Range" => AnalyzeColorRange(bgr, roi, colorSpace, hueLow, hueHigh, satLow, satHigh, valLow, valHigh, whiteBalanceTolerance),
            _ => AnalyzeAverageColor(bgr, roi, colorSpace, whiteBalanceTolerance)
        };

        return Task.FromResult(result);
    }

    private OperatorExecutionOutput AnalyzeAverageColor(Mat src, Rect roi, string colorSpace, double whiteBalanceTolerance)
    {
        using var converted = new Mat();
        var conversionCode = colorSpace.ToUpperInvariant() switch
        {
            "HSV" => ColorConversionCodes.BGR2HSV,
            "LAB" => ColorConversionCodes.BGR2Lab,
            _ => ColorConversionCodes.BGR2HSV
        };

        Cv2.CvtColor(src, converted, conversionCode);
        using var roiView = new Mat(converted, roi);
        var mean = Cv2.Mean(roiView);

        var resultImage = src.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);

        var info = colorSpace.ToUpperInvariant() switch
        {
            "HSV" => $"H:{mean.Val0:F1} S:{mean.Val1:F1} V:{mean.Val2:F1}",
            "LAB" => $"L:{mean.Val0:F1} a:{mean.Val1:F1} b:{mean.Val2:F1}",
            _ => $"C1:{mean.Val0:F1} C2:{mean.Val1:F1} C3:{mean.Val2:F1}"
        };
        Cv2.PutText(resultImage, info, new Point(roi.X, Math.Max(20, roi.Y - 6)), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        var meanColor = colorSpace.ToUpperInvariant() switch
        {
            "HSV" => new Dictionary<string, object>
            {
                { "Hue", mean.Val0 },
                { "Saturation", mean.Val1 },
                { "Value", mean.Val2 }
            },
            "LAB" => new Dictionary<string, object>
            {
                { "L", mean.Val0 },
                { "a", mean.Val1 },
                { "b", mean.Val2 }
            },
            _ => new Dictionary<string, object>
            {
                { "Channel1", mean.Val0 },
                { "Channel2", mean.Val1 },
                { "Channel3", mean.Val2 }
            }
        };

        var whiteBalance = EvaluateWhiteBalance(src, roi, whiteBalanceTolerance);
        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "Average" },
            { "AnalysisMode", "Average" },
            { "ColorSpace", colorSpace },
            { "PrimaryData", meanColor },
            { "Summary", info },
            { "Coverage", 1.0 },
            { "WhiteBalanceStatus", whiteBalance.Status }
        };

        var output = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "Average" },
            { "ColorSpace", colorSpace },
            { "DeltaE", 0.0 },
            { "Coverage", 1.0 },
            { "WhiteBalanceStatus", whiteBalance.Status },
            { "MeanColor", meanColor },
            { "DominantColors", Array.Empty<object>() },
            { "Diagnostics", CreateDiagnostics("Average", colorSpace, roi, 1.0, whiteBalance) }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output));
    }

    private OperatorExecutionOutput AnalyzeDominantColors(Mat src, Rect roi, int k, double whiteBalanceTolerance)
    {
        using var roiView = new Mat(src, roi);
        using var resized = new Mat();
        Cv2.Resize(roiView, resized, new Size(64, 64));

        using var floatMat = new Mat();
        resized.ConvertTo(floatMat, MatType.CV_32FC3);
        var data = floatMat.Reshape(1, floatMat.Rows * floatMat.Cols);

        using var bestLabels = new Mat();
        using var centers = new Mat();
        Cv2.Kmeans(
            data,
            k,
            bestLabels,
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 10, 1.0),
            3,
            KMeansFlags.RandomCenters,
            centers);

        var labelCounts = new int[k];
        for (var i = 0; i < bestLabels.Rows; i++)
        {
            labelCounts[bestLabels.At<int>(i, 0)]++;
        }

        var dominantColors = new List<Dictionary<string, object>>();
        for (var i = 0; i < k; i++)
        {
            var center = centers.At<Vec3f>(i);
            dominantColors.Add(new Dictionary<string, object>
            {
                { "Rank", i + 1 },
                { "Percentage", (double)labelCounts[i] / Math.Max(1, bestLabels.Rows) },
                { "B", center.Item0 },
                { "G", center.Item1 },
                { "R", center.Item2 },
                { "Hex", $"#{(int)center.Item2:X2}{(int)center.Item1:X2}{(int)center.Item0:X2}" }
            });
        }

        dominantColors = dominantColors.OrderByDescending(x => (double)x["Percentage"]).ToList();
        for (var i = 0; i < dominantColors.Count; i++)
        {
            dominantColors[i]["Rank"] = i + 1;
        }

        var resultImage = src.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);
        var barHeight = 24;
        var barY = resultImage.Height - barHeight - 10;
        for (var i = 0; i < Math.Min(k, 5); i++)
        {
            var color = dominantColors[i];
            var scalar = new Scalar(Convert.ToDouble(color["B"]), Convert.ToDouble(color["G"]), Convert.ToDouble(color["R"]));
            var barWidth = Math.Max(1, (int)(200 * (double)color["Percentage"]));
            var rect = new Rect(10, barY - (i * (barHeight + 5)), barWidth, barHeight);
            Cv2.Rectangle(resultImage, rect, scalar, -1);
            Cv2.Rectangle(resultImage, rect, Scalar.White, 1);
        }

        var whiteBalance = EvaluateWhiteBalance(src, roi, whiteBalanceTolerance);
        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "Dominant" },
            { "AnalysisMode", "Dominant" },
            { "ColorSpace", "BGR" },
            { "PrimaryData", dominantColors },
            { "K", k },
            { "Coverage", 1.0 },
            { "WhiteBalanceStatus", whiteBalance.Status }
        };

        var output = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "Dominant" },
            { "ColorSpace", "BGR" },
            { "DeltaE", 0.0 },
            { "Coverage", 1.0 },
            { "WhiteBalanceStatus", whiteBalance.Status },
            { "MeanColor", dominantColors.FirstOrDefault() ?? new Dictionary<string, object>() },
            { "DominantColors", dominantColors },
            { "Diagnostics", CreateDiagnostics("Dominant", "BGR", roi, 1.0, whiteBalance) }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output));
    }

    private OperatorExecutionOutput AnalyzeColorRange(
        Mat src,
        Rect roi,
        string colorSpace,
        int hueLow,
        int hueHigh,
        int satLow,
        int satHigh,
        int valLow,
        int valHigh,
        double whiteBalanceTolerance)
    {
        using var roiView = new Mat(src, roi);
        using var converted = new Mat();
        var conversionCode = colorSpace.ToUpperInvariant() switch
        {
            "HSV" => ColorConversionCodes.BGR2HSV,
            "LAB" => ColorConversionCodes.BGR2Lab,
            _ => ColorConversionCodes.BGR2HSV
        };

        Cv2.CvtColor(roiView, converted, conversionCode);
        using var mask = colorSpace.Equals("HSV", StringComparison.OrdinalIgnoreCase)
            ? CreateHueWrappedMask(converted, hueLow, hueHigh, satLow, satHigh, valLow, valHigh)
            : CreateSimpleMask(converted, hueLow, hueHigh, satLow, satHigh, valLow, valHigh);

        var totalPixels = Math.Max(1, mask.Rows * mask.Cols);
        var matchedPixels = Cv2.CountNonZero(mask);
        var coverage = (double)matchedPixels / totalPixels;
        var mean = Cv2.Mean(converted, mask);
        var whiteBalance = EvaluateWhiteBalance(src, roi, whiteBalanceTolerance);

        using var highlighted = new Mat();
        Cv2.CvtColor(mask, highlighted, ColorConversionCodes.GRAY2BGR);
        Cv2.BitwiseAnd(roiView, highlighted, highlighted);

        var resultImage = src.Clone();
        using var overlayRoi = new Mat(resultImage, roi);
        Cv2.AddWeighted(roiView, 0.7, highlighted, 0.3, 0, overlayRoi);
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);
        Cv2.PutText(resultImage, $"Range:{coverage:P1}", new Point(roi.X, Math.Max(20, roi.Y - 6)), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);

        var meanColor = new Dictionary<string, object>
        {
            { "Channel1", mean.Val0 },
            { "Channel2", mean.Val1 },
            { "Channel3", mean.Val2 }
        };
        var rangeData = new Dictionary<string, object>
        {
            { "Coverage", coverage },
            { "MatchedPixels", matchedPixels },
            { "TotalPixels", totalPixels },
            { "HueLow", hueLow },
            { "HueHigh", hueHigh },
            { "SatLow", satLow },
            { "SatHigh", satHigh },
            { "ValLow", valLow },
            { "ValHigh", valHigh }
        };
        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "Range" },
            { "AnalysisMode", "Range" },
            { "ColorSpace", colorSpace },
            { "PrimaryData", rangeData },
            { "WhiteBalanceStatus", whiteBalance.Status }
        };

        var output = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "Range" },
            { "ColorSpace", colorSpace },
            { "DeltaE", 0.0 },
            { "Coverage", coverage },
            { "WhiteBalanceStatus", whiteBalance.Status },
            { "MeanColor", meanColor },
            { "DominantColors", Array.Empty<object>() },
            { "Diagnostics", CreateDiagnostics("Range", colorSpace, roi, coverage, whiteBalance) }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output));
    }

    private OperatorExecutionOutput AnalyzeHsvInspection(
        Mat src,
        Rect roi,
        int hueLow,
        int hueHigh,
        int satLow,
        int satHigh,
        int valLow,
        int valHigh,
        double whiteBalanceTolerance)
    {
        using var roiView = new Mat(src, roi);
        using var hsv = new Mat();
        Cv2.CvtColor(roiView, hsv, ColorConversionCodes.BGR2HSV);

        using var mask = CreateHueWrappedMask(hsv, hueLow, hueHigh, satLow, satHigh, valLow, valHigh);
        var totalPixels = Math.Max(1, mask.Rows * mask.Cols);
        var matchedPixels = Cv2.CountNonZero(mask);
        var coverage = (double)matchedPixels / totalPixels;
        var mean = Cv2.Mean(hsv, mask);
        var whiteBalance = EvaluateWhiteBalance(src, roi, whiteBalanceTolerance);

        using var highlighted = new Mat();
        Cv2.CvtColor(mask, highlighted, ColorConversionCodes.GRAY2BGR);
        Cv2.BitwiseAnd(roiView, highlighted, highlighted);

        var resultImage = src.Clone();
        using var overlayRoi = new Mat(resultImage, roi);
        Cv2.AddWeighted(roiView, 0.65, highlighted, 0.35, 0, overlayRoi);
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);
        Cv2.PutText(resultImage, $"HSV:{coverage:P1}", new Point(roi.X, Math.Max(20, roi.Y - 6)), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);

        var meanColor = new Dictionary<string, object>
        {
            { "Hue", mean.Val0 },
            { "Saturation", mean.Val1 },
            { "Value", mean.Val2 }
        };
        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "HsvInspection" },
            { "AnalysisMode", "HsvInspection" },
            { "ColorSpace", "HSV" },
            { "PrimaryData", meanColor },
            { "Coverage", coverage },
            { "Summary", $"HSV coverage {coverage:P1}" },
            { "WhiteBalanceStatus", whiteBalance.Status }
        };

        var output = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "HsvInspection" },
            { "ColorSpace", "HSV" },
            { "DeltaE", 0.0 },
            { "Coverage", coverage },
            { "WhiteBalanceStatus", whiteBalance.Status },
            { "MeanColor", meanColor },
            { "DominantColors", Array.Empty<object>() },
            { "Diagnostics", CreateDiagnostics("HsvInspection", "HSV", roi, coverage, whiteBalance) }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output));
    }

    private OperatorExecutionOutput AnalyzeLabDeltaE(
        Mat src,
        Rect roi,
        Operator @operator,
        Dictionary<string, object>? inputs,
        string deltaEMethod,
        double whiteBalanceTolerance)
    {
        var meanLab = CieLabConverter.ComputeMeanLabBgr8U(src, roi);
        var hasReference = TryResolveReferenceLab(@operator, inputs, out var referenceLab);
        if (!hasReference)
        {
            return OperatorExecutionOutput.Failure("LabDeltaE mode requires a complete reference color (ReferenceColor or RefL/RefA/RefB).");
        }

        var deltaE = deltaEMethod.Equals("CIE76", StringComparison.OrdinalIgnoreCase)
            ? ColorDifference.DeltaE76(meanLab, referenceLab)
            : ColorDifference.DeltaE00(meanLab, referenceLab);

        var whiteBalance = EvaluateWhiteBalance(src, roi, whiteBalanceTolerance);
        var resultImage = src.Clone();
        Cv2.Rectangle(resultImage, roi, new Scalar(0, 255, 255), 2);
        Cv2.PutText(resultImage, $"DeltaE:{deltaE:F2}", new Point(roi.X, Math.Max(20, roi.Y - 6)), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 2);

        var meanColor = new Dictionary<string, object>
        {
            { "L", meanLab.L },
            { "A", meanLab.A },
            { "B", meanLab.B }
        };

        var diagnostics = CreateDiagnostics("LabDeltaE", "Lab", roi, 1.0, whiteBalance);
        diagnostics["ReferenceProvided"] = hasReference;
        diagnostics["DeltaEMethod"] = deltaEMethod;
        diagnostics["ReferenceL"] = referenceLab.L;
        diagnostics["ReferenceA"] = referenceLab.A;
        diagnostics["ReferenceB"] = referenceLab.B;

        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "LabDeltaE" },
            { "AnalysisMode", "LabDeltaE" },
            { "ColorSpace", "Lab" },
            { "PrimaryData", meanColor },
            { "Summary", $"DeltaE {deltaE:F2}" },
            { "WhiteBalanceStatus", whiteBalance.Status }
        };

        var output = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "LabDeltaE" },
            { "ColorSpace", "Lab" },
            { "DeltaE", deltaE },
            { "Coverage", 1.0 },
            { "WhiteBalanceStatus", whiteBalance.Status },
            { "MeanColor", meanColor },
            { "DominantColors", Array.Empty<object>() },
            { "Diagnostics", diagnostics }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, output));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var colorSpace = GetStringParam(@operator, "ColorSpace", "HSV");
        if (!new[] { "HSV", "Lab" }.Contains(colorSpace, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("ColorSpace must be HSV or Lab");
        }

        var analysisMode = GetStringParam(@operator, "AnalysisMode", "Average");
        if (!new[] { "Average", "Dominant", "Range", "HsvInspection", "LabDeltaE" }.Contains(analysisMode, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("AnalysisMode must be Average, Dominant, Range, HsvInspection or LabDeltaE");
        }

        var deltaEMethod = GetStringParam(@operator, "DeltaEMethod", "CIEDE2000");
        if (!new[] { "CIE76", "CIEDE2000" }.Contains(deltaEMethod, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("DeltaEMethod must be CIE76 or CIEDE2000");
        }

        var roiW = GetOptionalInt(@operator, "RoiW") ?? 0;
        var roiH = GetOptionalInt(@operator, "RoiH") ?? 0;
        if (roiW < 0 || roiH < 0)
        {
            return ValidationResult.Invalid("RoiW/RoiH must be >= 0");
        }

        return ValidationResult.Valid();
    }

    private static string NormalizeAnalysisMode(string analysisMode)
    {
        return analysisMode.Trim() switch
        {
            var value when value.Equals("LabDeltaE", StringComparison.OrdinalIgnoreCase) => "LabDeltaE",
            var value when value.Equals("HsvInspection", StringComparison.OrdinalIgnoreCase) => "HsvInspection",
            var value when value.Equals("Dominant", StringComparison.OrdinalIgnoreCase) => "Dominant",
            var value when value.Equals("Range", StringComparison.OrdinalIgnoreCase) => "Range",
            _ => "Average"
        };
    }

    private static Mat EnsureBgr(Mat src)
    {
        if (src.Channels() == 3)
        {
            return src.Clone();
        }

        var bgr = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, bgr, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            Cv2.CvtColor(src, bgr, ColorConversionCodes.BGRA2BGR);
        }

        return bgr;
    }

    private static Rect ResolveRoi(Operator @operator, Mat src)
    {
        var roiX = GetOptionalInt(@operator, "RoiX") ?? 0;
        var roiY = GetOptionalInt(@operator, "RoiY") ?? 0;
        var roiW = GetOptionalInt(@operator, "RoiW") ?? 0;
        var roiH = GetOptionalInt(@operator, "RoiH") ?? 0;

        if (roiW <= 0)
        {
            roiW = src.Width - roiX;
        }

        if (roiH <= 0)
        {
            roiH = src.Height - roiY;
        }

        return ClampRect(new Rect(roiX, roiY, roiW, roiH), src.Width, src.Height);
    }

    private static bool TryResolveReferenceLab(Operator @operator, Dictionary<string, object>? inputs, out CieLab referenceLab)
    {
        referenceLab = default;

        if (inputs != null &&
            inputs.TryGetValue("ReferenceColor", out var referenceObject) &&
            TryOverrideReference(referenceObject, out var refL, out var refA, out var refB))
        {
            referenceLab = new CieLab(refL, refA, refB);
            return true;
        }

        if (TryResolveParameterReference(@operator, out refL, out refA, out refB))
        {
            referenceLab = new CieLab(refL, refA, refB);
            return true;
        }

        return false;
    }

    private static bool TryResolveParameterReference(Operator @operator, out double refL, out double refA, out double refB)
    {
        refL = 0;
        refA = 0;
        refB = 0;

        var l = GetOptionalDouble(@operator, "RefL");
        var a = GetOptionalDouble(@operator, "RefA");
        var b = GetOptionalDouble(@operator, "RefB");
        if (l == null && a == null && b == null)
        {
            return false;
        }

        if (l == null || a == null || b == null)
        {
            return false;
        }

        refL = l.Value;
        refA = a.Value;
        refB = b.Value;
        return true;
    }

    private static int? GetOptionalInt(Operator @operator, string name)
    {
        var parameter = @operator.Parameters.FirstOrDefault(p => p.Name == name);
        if (parameter?.Value == null)
        {
            return null;
        }

        if (parameter.Value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => jsonElement.GetInt32(),
                System.Text.Json.JsonValueKind.String when int.TryParse(jsonElement.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return Convert.ToInt32(parameter.Value);
    }

    private static double? GetOptionalDouble(Operator @operator, string name)
    {
        var parameter = @operator.Parameters.FirstOrDefault(p => p.Name == name);
        if (parameter?.Value == null)
        {
            return null;
        }

        if (parameter.Value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.String when double.TryParse(jsonElement.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return Convert.ToDouble(parameter.Value);
    }

    private static bool TryOverrideReference(object? referenceObject, out double refL, out double refA, out double refB)
    {
        refL = 0;
        refA = 0;
        refB = 0;

        if (referenceObject == null)
        {
            return false;
        }

        if (referenceObject is double[] doubles && doubles.Length >= 3)
        {
            refL = doubles[0];
            refA = doubles[1];
            refB = doubles[2];
            return true;
        }

        if (referenceObject is float[] floats && floats.Length >= 3)
        {
            refL = floats[0];
            refA = floats[1];
            refB = floats[2];
            return true;
        }

        if (referenceObject is IDictionary<string, object> dict)
        {
            var hasL = TryGetDouble(dict, "L", out refL);
            var hasA = TryGetDouble(dict, "A", out refA);
            var hasB = TryGetDouble(dict, "B", out refB);
            return hasL && hasA && hasB;
        }

        return false;
    }

    private static bool TryGetDouble(IDictionary<string, object> dict, string key, out double value)
    {
        value = 0;
        var entry = dict.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(entry.Key) || entry.Value == null)
        {
            return false;
        }

        return entry.Value switch
        {
            double d => (value = d) == d,
            float f => (value = f) == f,
            int i => (value = i) == i,
            long l => (value = l) == l,
            _ => double.TryParse(entry.Value.ToString(), out value)
        };
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        var w = Math.Clamp(rect.Width, 0, width - x);
        var h = Math.Clamp(rect.Height, 0, height - y);
        return new Rect(x, y, w, h);
    }

    private static Mat CreateSimpleMask(Mat converted, int low0, int high0, int low1, int high1, int low2, int high2)
    {
        var mask = new Mat();
        Cv2.InRange(converted, new Scalar(low0, low1, low2), new Scalar(high0, high1, high2), mask);
        return mask;
    }

    private static Mat CreateHueWrappedMask(Mat hsv, int hueLow, int hueHigh, int satLow, int satHigh, int valLow, int valHigh)
    {
        if (hueLow <= hueHigh)
        {
            return CreateSimpleMask(hsv, hueLow, hueHigh, satLow, satHigh, valLow, valHigh);
        }

        using var lowerWrap = CreateSimpleMask(hsv, 0, hueHigh, satLow, satHigh, valLow, valHigh);
        using var upperWrap = CreateSimpleMask(hsv, hueLow, 180, satLow, satHigh, valLow, valHigh);
        var mask = new Mat();
        Cv2.BitwiseOr(lowerWrap, upperWrap, mask);
        return mask;
    }

    private static WhiteBalanceResult EvaluateWhiteBalance(Mat src, Rect roi, double tolerance)
    {
        using var roiView = new Mat(src, roi);
        var mean = Cv2.Mean(roiView);
        var minChannel = Math.Min(mean.Val0, Math.Min(mean.Val1, mean.Val2));
        var maxChannel = Math.Max(mean.Val0, Math.Max(mean.Val1, mean.Val2));
        var deviation = maxChannel - minChannel;
        return new WhiteBalanceResult(
            deviation <= tolerance ? "Balanced" : "Suspect",
            deviation,
            mean.Val0,
            mean.Val1,
            mean.Val2);
    }

    private static Dictionary<string, object> CreateDiagnostics(string mode, string colorSpace, Rect roi, double coverage, WhiteBalanceResult whiteBalance)
    {
        return new Dictionary<string, object>
        {
            { "Mode", mode },
            { "ColorSpace", colorSpace },
            { "RoiX", roi.X },
            { "RoiY", roi.Y },
            { "RoiW", roi.Width },
            { "RoiH", roi.Height },
            { "Coverage", coverage },
            { "WhiteBalanceStatus", whiteBalance.Status },
            { "GrayWorldDeviation", whiteBalance.Deviation },
            { "MeanB", whiteBalance.MeanB },
            { "MeanG", whiteBalance.MeanG },
            { "MeanR", whiteBalance.MeanR }
        };
    }

    private readonly record struct WhiteBalanceResult(string Status, double Deviation, double MeanB, double MeanG, double MeanR);
}
