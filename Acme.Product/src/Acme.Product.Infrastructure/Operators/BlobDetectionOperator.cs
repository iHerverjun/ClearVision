// BlobDetectionOperator.cs
// Blob检测算子 - 检测图像中的连通区域
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// Blob检测算子 - 检测图像中的连通区域
/// </summary>
public class BlobDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.BlobAnalysis;

    public BlobDetectionOperator(ILogger<BlobDetectionOperator> logger) : base(logger)
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

        var minArea = GetFloatParam(@operator, "MinArea", 100f, min: 0);
        var maxArea = GetFloatParam(@operator, "MaxArea", 100000f, min: 0);
        var minCircularity = GetDoubleParam(@operator, "MinCircularity", 0.0, min: 0, max: 1.0);
        var minConvexity = GetDoubleParam(@operator, "MinConvexity", 0.0, min: 0, max: 1.0);
        var minInertiaRatio = GetDoubleParam(@operator, "MinInertiaRatio", 0.0, min: 0, max: 1.0);

        var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // SimpleBlobDetector 内部会自动处理灰度转换，支持彩色和灰度输入
        var detector = new SimpleBlobDetector.Params();
        detector.FilterByArea = true;
        detector.MinArea = minArea;
        detector.MaxArea = maxArea;

        if (minCircularity > 0)
        {
            detector.FilterByCircularity = true;
            detector.MinCircularity = (float)minCircularity;
        }

        if (minConvexity > 0)
        {
            detector.FilterByConvexity = true;
            detector.MinConvexity = (float)minConvexity;
        }

        if (minInertiaRatio > 0)
        {
            detector.FilterByInertia = true;
            detector.MinInertiaRatio = (float)minInertiaRatio;
        }

        using var blobDetector = SimpleBlobDetector.Create(detector);
        var keypoints = blobDetector.Detect(src);

        // 准备彩色结果图（用于绘制彩色标注）
        using var colorSrc = new Mat();
        if (src.Channels() == 1)
            Cv2.CvtColor(src, colorSrc, ColorConversionCodes.GRAY2BGR);
        else
            src.CopyTo(colorSrc);

        foreach (var kp in keypoints)
        {
            Cv2.Circle(colorSrc, (int)kp.Pt.X, (int)kp.Pt.Y, (int)kp.Size / 2, new Scalar(0, 255, 0), 2);
            Cv2.Circle(colorSrc, (int)kp.Pt.X, (int)kp.Pt.Y, 3, new Scalar(0, 0, 255), -1);
        }

        // P0: 使用ImageWrapper实现零拷贝输出
        var additionalData = new Dictionary<string, object>
        {
            { "BlobCount", keypoints.Length },
            { "Blobs", keypoints.Select(kp => new Dictionary<string, object>
            {
                { "X", kp.Pt.X },
                { "Y", kp.Pt.Y },
                { "Size", kp.Size },
                { "Area", Math.PI * Math.Pow(kp.Size / 2, 2) }
            }).ToList() }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(colorSrc, additionalData)));
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

        return ValidationResult.Valid();
    }
}
