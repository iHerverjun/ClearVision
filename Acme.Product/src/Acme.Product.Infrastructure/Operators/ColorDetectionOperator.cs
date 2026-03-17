// ColorDetectionOperator.cs
// 颜色范围检测
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;


using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 颜色检测算子 - 基于 HSV/Lab 空间的颜色分析与分级
/// ColorDetection = 45
/// </summary>
[OperatorMeta(
    DisplayName = "颜色检测",
    Description = "基于 HSV/Lab 空间的颜色分析与分级",
    Category = "颜色处理",
    IconName = "color"
)]
[InputPort("Image", "输入图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "结果图像", PortDataType.Image)]
[OutputPort("ColorInfo", "颜色信息", PortDataType.Any)]
[OutputPort("AnalysisMode", "分析模式", PortDataType.String)]
[OutputPort("ColorSpace", "颜色空间", PortDataType.String)]
[OperatorParam("ColorSpace", "颜色空间", "enum", DefaultValue = "HSV", Options = new[] { "HSV|HSV", "Lab|Lab" })]
[OperatorParam("AnalysisMode", "分析模式", "enum", DefaultValue = "Average", Options = new[] { "Average|平均色", "Dominant|主色提取", "Range|颜色范围检测" })]
[OperatorParam("HueLow", "H下限", "int", DefaultValue = 0, Min = 0, Max = 180)]
[OperatorParam("HueHigh", "H上限", "int", DefaultValue = 180, Min = 0, Max = 180)]
[OperatorParam("SatLow", "S下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("SatHigh", "S上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
[OperatorParam("ValLow", "V下限", "int", DefaultValue = 50, Min = 0, Max = 255)]
[OperatorParam("ValHigh", "V上限", "int", DefaultValue = 255, Min = 0, Max = 255)]
[OperatorParam("DominantK", "主色数量K", "int", DefaultValue = 3, Min = 1, Max = 10)]
public class ColorDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ColorDetection;

    public ColorDetectionOperator(ILogger<ColorDetectionOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 获取参数
        var colorSpace = GetStringParam(@operator, "ColorSpace", "HSV");
        var analysisMode = GetStringParam(@operator, "AnalysisMode", "Average");
        var hueLow = GetIntParam(@operator, "HueLow", 0, 0, 180);
        var hueHigh = GetIntParam(@operator, "HueHigh", 180, 0, 180);
        var satLow = GetIntParam(@operator, "SatLow", 50, 0, 255);
        var satHigh = GetIntParam(@operator, "SatHigh", 255, 0, 255);
        var valLow = GetIntParam(@operator, "ValLow", 50, 0, 255);
        var valHigh = GetIntParam(@operator, "ValHigh", 255, 0, 255);
        var dominantK = GetIntParam(@operator, "DominantK", 3, 1, 10);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 执行分析
        var result = analysisMode switch
        {
            "Average" => AnalyzeAverageColor(src, colorSpace),
            "Dominant" => AnalyzeDominantColors(src, dominantK),
            "Range" => AnalyzeColorRange(src, colorSpace, hueLow, hueHigh, satLow, satHigh, valLow, valHigh),
            _ => AnalyzeAverageColor(src, colorSpace)
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// 平均色分析
    /// </summary>
    private OperatorExecutionOutput AnalyzeAverageColor(Mat src, string colorSpace)
    {
        using var converted = new Mat();
        ColorConversionCodes conversionCode = colorSpace.ToUpper() switch
        {
            "HSV" => ColorConversionCodes.BGR2HSV,
            "Lab" => ColorConversionCodes.BGR2Lab,
            _ => ColorConversionCodes.BGR2HSV
        };
        
        Cv2.CvtColor(src, converted, conversionCode);
        
        // 计算平均值
        var mean = Cv2.Mean(converted);
        
        var resultImage = src.Clone();
        
        // 显示信息
        string info = colorSpace.ToUpper() switch
        {
            "HSV" => $"H:{mean.Val0:F1} S:{mean.Val1:F1} V:{mean.Val2:F1}",
            "Lab" => $"L:{mean.Val0:F1} a:{mean.Val1:F1} b:{mean.Val2:F1}",
            _ => $"C1:{mean.Val0:F1} C2:{mean.Val1:F1} C3:{mean.Val2:F1}"
        };
        
        Cv2.PutText(resultImage, info, new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        var colorData = colorSpace.ToUpper() switch
        {
            "HSV" => new Dictionary<string, object>
            {
                { "Hue", mean.Val0 },
                { "Saturation", mean.Val1 },
                { "Value", mean.Val2 }
            },
            "Lab" => new Dictionary<string, object>
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

        // 统一的 ColorInfo 结构
        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "Average" },
            { "AnalysisMode", "Average" },
            { "ColorSpace", colorSpace },
            { "PrimaryData", colorData },
            { "Summary", info }
        };

        var additionalData = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "Average" },
            { "ColorSpace", colorSpace }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData));
    }

    /// <summary>
    /// 主色提取 - 使用 K-Means 聚类
    /// </summary>
    private OperatorExecutionOutput AnalyzeDominantColors(Mat src, int k)
    {
        // Resize 以加速处理
        using var resized = new Mat();
        Cv2.Resize(src, resized, new Size(64, 64));
        
        // 转换为 float 并 reshape
        using var floatMat = new Mat();
        resized.ConvertTo(floatMat, MatType.CV_32FC3);
        
        var data = floatMat.Reshape(1, floatMat.Rows * floatMat.Cols);
        
        // K-Means 聚类
        var bestLabels = new Mat();
        var centers = new Mat();
        Cv2.Kmeans(data, k, bestLabels, 
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 10, 1.0),
            3, KMeansFlags.RandomCenters, centers);

        // 统计每个聚类的大小
        var labelCounts = new int[k];
        for (int i = 0; i < bestLabels.Rows; i++)
        {
            labelCounts[bestLabels.At<int>(i)]++;
        }

        // 提取主色
        var dominantColors = new List<Dictionary<string, object>>();
        for (int i = 0; i < k; i++)
        {
            var center = centers.At<Vec3f>(i);
            dominantColors.Add(new Dictionary<string, object>
            {
                { "Rank", i + 1 },
                { "Percentage", (double)labelCounts[i] / bestLabels.Rows },
                { "B", center.Item0 },
                { "G", center.Item1 },
                { "R", center.Item2 },
                { "Hex", $"#{((int)center.Item2):X2}{((int)center.Item1):X2}{((int)center.Item0):X2}" }
            });
        }

        // 按占比排序
        dominantColors = dominantColors.OrderByDescending(c => (double)c["Percentage"]).ToList();
        for (int i = 0; i < dominantColors.Count; i++)
        {
            dominantColors[i]["Rank"] = i + 1;
        }

        // 创建可视化结果
        var resultImage = src.Clone();
        int barHeight = 30;
        int barY = resultImage.Height - barHeight - 10;
        
        for (int i = 0; i < Math.Min(k, 5); i++)
        {
            var color = dominantColors[i];
            var scalar = new Scalar((byte)color["B"], (byte)color["G"], (byte)color["R"]);
            int barWidth = (int)(200 * (double)color["Percentage"]);
            Cv2.Rectangle(resultImage, 
                new Rect(10, barY - i * (barHeight + 5), barWidth, barHeight),
                scalar, -1);
            Cv2.Rectangle(resultImage, 
                new Rect(10, barY - i * (barHeight + 5), barWidth, barHeight),
                new Scalar(255, 255, 255), 1);
        }

        // 统一的 ColorInfo 结构
        var colorInfo = new Dictionary<string, object>
        {
            { "Mode", "Dominant" },
            { "AnalysisMode", "Dominant" },
            { "ColorSpace", "BGR" },
            { "PrimaryData", dominantColors },
            { "K", k }
        };

        var additionalData = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "Dominant" },
            { "ColorSpace", "BGR" }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData));
    }

    /// <summary>
    /// 颜色范围检测
    /// </summary>
    private OperatorExecutionOutput AnalyzeColorRange(Mat src, string colorSpace, 
        int hueLow, int hueHigh, int satLow, int satHigh, int valLow, int valHigh)
    {
        using var converted = new Mat();
        ColorConversionCodes conversionCode = colorSpace.ToUpper() switch
        {
            "HSV" => ColorConversionCodes.BGR2HSV,
            "Lab" => ColorConversionCodes.BGR2Lab,
            _ => ColorConversionCodes.BGR2HSV
        };
        
        Cv2.CvtColor(src, converted, conversionCode);
        
        // 创建掩膜
        using var mask = new Mat();
        var lower = new Scalar(hueLow, satLow, valLow);
        var upper = new Scalar(hueHigh, satHigh, valHigh);
        Cv2.InRange(converted, lower, upper, mask);
        
        // 计算占比
        var totalPixels = mask.Rows * mask.Cols;
        var whitePixels = Cv2.CountNonZero(mask);
        var percentage = (double)whitePixels / totalPixels;
        
        // 创建结果图像
        using var coloredMask = new Mat();
        Cv2.CvtColor(mask, coloredMask, ColorConversionCodes.GRAY2BGR);
        Cv2.BitwiseAnd(src, coloredMask, coloredMask);
        
        var resultImage = src.Clone();
        
        // 在原图上叠加掩膜
        var overlay = new Mat();
        Cv2.AddWeighted(src, 0.7, coloredMask, 0.3, 0, overlay);
        
        // 显示信息
        var info = $"Range: {percentage:P1} ({whitePixels}/{totalPixels})";
        Cv2.PutText(overlay, info, new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 255, 0), 2);

        // 统一的 ColorInfo 结构
        var rangeData = new Dictionary<string, object>
        {
            { "Coverage", percentage },
            { "MatchedPixels", whitePixels },
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
            { "PrimaryData", rangeData }
        };

        var additionalData = new Dictionary<string, object>
        {
            { "ColorInfo", colorInfo },
            { "AnalysisMode", "Range" },
            { "ColorSpace", colorSpace }
        };

        return OperatorExecutionOutput.Success(CreateImageOutput(overlay, additionalData));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var colorSpace = GetStringParam(@operator, "ColorSpace", "HSV");
        var analysisMode = GetStringParam(@operator, "AnalysisMode", "Average");
        
        if (colorSpace != "HSV" && colorSpace != "Lab")
        {
            return ValidationResult.Invalid("颜色空间必须是 HSV 或 Lab");
        }

        if (analysisMode != "Average" && analysisMode != "Dominant" && analysisMode != "Range")
        {
            return ValidationResult.Invalid("分析模式必须是 Average、Dominant 或 Range");
        }

        return ValidationResult.Valid();
    }
}
