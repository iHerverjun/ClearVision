// FindContoursOperator.cs
// 轮廓查找算子 - 查找图像中的轮廓
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 轮廓查找算子 - 查找图像中的轮廓
/// </summary>
public class FindContoursOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ContourDetection;

    public FindContoursOperator(ILogger<FindContoursOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        var mode = GetStringParam(@operator, "Mode", "External");
        var method = GetStringParam(@operator, "Method", "Simple");
        var minArea = GetFloatParam(@operator, "MinArea", 100f, min: 0);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f, min: 0);
        var drawContours = GetBoolParam(@operator, "DrawContours", true);
        var threshold = GetDoubleParam(@operator, "Threshold", 127.0, min: 0, max: 255);
        var maxValue = GetDoubleParam(@operator, "MaxValue", 255.0, min: 0, max: 255);
        var thresholdType = GetIntParam(@operator, "ThresholdType", 0, min: 0, max: 1);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 转换为灰度图
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // 二值化
        using var binary = new Mat();
        var threshType = thresholdType == 0 ? ThresholdTypes.Binary : ThresholdTypes.BinaryInv;
        Cv2.Threshold(gray, binary, threshold, maxValue, threshType);

        // 查找轮廓
        var retrievalMode = mode.ToLower() switch
        {
            "external" => RetrievalModes.External,
            "list" => RetrievalModes.List,
            "tree" => RetrievalModes.Tree,
            "flooded" => RetrievalModes.FloodFill,
            _ => RetrievalModes.External
        };

        var contourApprox = method.ToLower() switch
        {
            "none" => OpenCvSharp.ContourApproximationModes.ApproxNone,
            "simple" => OpenCvSharp.ContourApproximationModes.ApproxSimple,
            "tc89_l1" => OpenCvSharp.ContourApproximationModes.ApproxTC89L1,
            "tc89_kcos" => OpenCvSharp.ContourApproximationModes.ApproxTC89KCOS,
            _ => OpenCvSharp.ContourApproximationModes.ApproxSimple
        };

        Cv2.FindContours(binary, out Point[][] contours, out HierarchyIndex[] hierarchy, retrievalMode, contourApprox);

        // 筛选轮廓
        var filteredContours = contours
            .Where(c =>
            {
                var area = Cv2.ContourArea(c);
                return area >= minArea && area <= maxArea;
            })
            .ToArray();

        // 绘制轮廓
        var resultImg = src.Clone();
        if (drawContours && filteredContours.Length > 0)
        {
            for (int i = 0; i < filteredContours.Length; i++)
            {
                Cv2.DrawContours(resultImg, filteredContours, i, new Scalar(0, 255, 0), 2);

                // 计算轮廓中心
                var moments = Cv2.Moments(filteredContours[i]);
                if (moments.M00 != 0)
                {
                    int cx = (int)(moments.M10 / moments.M00);
                    int cy = (int)(moments.M01 / moments.M00);
                    Cv2.Circle(resultImg, cx, cy, 3, new Scalar(0, 0, 255), -1);
                    Cv2.PutText(resultImg, i.ToString(), new Point(cx + 5, cy - 5),
                        HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 0, 0), 1);
                }
            }
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        // 构建轮廓信息
        var contourInfos = filteredContours.Select((c, index) =>
        {
            var area = Cv2.ContourArea(c);
            var perimeter = Cv2.ArcLength(c, true);
            var rect = Cv2.BoundingRect(c);

            return new Dictionary<string, object>
            {
                { "Id", index },
                { "Area", area },
                { "Perimeter", perimeter },
                { "X", rect.X },
                { "Y", rect.Y },
                { "Width", rect.Width },
                { "Height", rect.Height },
                { "PointCount", c.Length }
            };
        }).ToList();

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "ContourCount", filteredContours.Length },
            { "Contours", contourInfos }
        };
        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultImg, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var minArea = GetFloatParam(@operator, "MinArea", 100f);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f);

        if (minArea < 0 || maxArea < 0)
        {
            return ValidationResult.Invalid("面积范围不能为负数");
        }

        if (minArea >= maxArea)
        {
            return ValidationResult.Invalid("最小面积必须小于最大面积");
        }

        var mode = GetStringParam(@operator, "Mode", "External").ToLower();
        var validModes = new[] { "external", "list", "tree", "flooded" };
        if (!validModes.Contains(mode))
        {
            return ValidationResult.Invalid($"不支持的轮廓模式: {mode}");
        }

        return ValidationResult.Valid();
    }
}
