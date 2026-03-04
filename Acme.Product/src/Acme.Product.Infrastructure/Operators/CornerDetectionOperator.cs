// CornerDetectionOperator.cs
// 角点检测算子
// 检测图像关键角点并输出点集结果
// 作者：蘅芜君
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

using Acme.Product.Core.Attributes;
namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "角点检测",
    Description = "Detects corner points using Harris or Shi-Tomasi.",
    Category = "定位",
    IconName = "corner",
    Keywords = new[] { "corner", "vertex", "harris", "shitomasi" }
)]
[InputPort("Image", "Image", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "Image", PortDataType.Image)]
[OutputPort("Corners", "Corners", PortDataType.PointList)]
[OutputPort("Count", "Count", PortDataType.Integer)]
[OperatorParam("Method", "Method", "enum", DefaultValue = "ShiTomasi", Options = new[] { "Harris|Harris", "ShiTomasi|ShiTomasi" })]
[OperatorParam("MaxCorners", "Max Corners", "int", DefaultValue = 100, Min = 1, Max = 5000)]
[OperatorParam("QualityLevel", "Quality Level", "double", DefaultValue = 0.01, Min = 1E-06, Max = 1.0)]
[OperatorParam("MinDistance", "Min Distance", "double", DefaultValue = 10.0, Min = 0.0, Max = 10000.0)]
[OperatorParam("BlockSize", "Block Size", "int", DefaultValue = 3, Min = 2, Max = 31)]
public class CornerDetectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.CornerDetection;

    public CornerDetectionOperator(ILogger<CornerDetectionOperator> logger) : base(logger)
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

        var method = GetStringParam(@operator, "Method", "ShiTomasi");
        var maxCorners = GetIntParam(@operator, "MaxCorners", 100, 1, 5000);
        var qualityLevel = GetDoubleParam(@operator, "QualityLevel", 0.01, 1e-6, 1.0);
        var minDistance = GetDoubleParam(@operator, "MinDistance", 10.0, 0.0, 10000.0);
        var blockSize = GetIntParam(@operator, "BlockSize", 3, 2, 31);

        using var gray = new Mat();
        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }

        var useHarris = method.Equals("Harris", StringComparison.OrdinalIgnoreCase);
        using var noMask = new Mat();
        var corners = Cv2.GoodFeaturesToTrack(gray, maxCorners, qualityLevel, minDistance, noMask, blockSize, useHarris, 0.04);
        corners ??= Array.Empty<Point2f>();

        if (corners.Length > 0)
        {
            Cv2.CornerSubPix(
                gray,
                corners,
                new Size(5, 5),
                new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01));
        }

        var result = src.Clone();
        foreach (var c in corners)
        {
            var center = new Point((int)Math.Round(c.X), (int)Math.Round(c.Y));
            Cv2.DrawMarker(result, center, new Scalar(0, 0, 255), MarkerTypes.Cross, 12, 2);
        }

        var points = corners.Select(c => new Position(c.X, c.Y)).ToList();
        var output = new Dictionary<string, object>
        {
            { "Corners", points },
            { "Count", points.Count }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(result, output)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var method = GetStringParam(@operator, "Method", "ShiTomasi");
        var validMethods = new[] { "Harris", "ShiTomasi" };
        if (!validMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationResult.Invalid("Method must be Harris or ShiTomasi");
        }

        var maxCorners = GetIntParam(@operator, "MaxCorners", 100);
        if (maxCorners <= 0)
        {
            return ValidationResult.Invalid("MaxCorners must be greater than 0");
        }

        var quality = GetDoubleParam(@operator, "QualityLevel", 0.01);
        if (quality <= 0 || quality > 1.0)
        {
            return ValidationResult.Invalid("QualityLevel must be in (0, 1]");
        }

        return ValidationResult.Valid();
    }
}
