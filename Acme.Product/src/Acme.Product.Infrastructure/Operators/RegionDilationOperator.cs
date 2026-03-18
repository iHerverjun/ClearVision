// RegionDilationOperator.cs
// 区域膨胀算子 - 基于游程编码的区域级形态学膨胀
// 对标 Halcon: dilation_region
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 区域膨胀算子 - 基于游程编码的区域级形态学膨胀
/// 对标 Halcon dilation_region
/// </summary>
[OperatorMeta(
    DisplayName = "Region Dilation",
    Description = "Dilates a region using a specified structuring element (Region-based morphology).",
    Category = "Morphology",
    IconName = "region-dilation",
    Keywords = new[] { "Region", "Dilation", "Morphology", "Expand", "Grow", "RLE" }
)]
[InputPort("Region", "Input Region", PortDataType.Any, IsRequired = true)]
[InputPort("Image", "Reference Image (Optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("Region", "Dilated Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Dilated Area", PortDataType.Integer)]
[OperatorParam("KernelShape", "Structuring Element Shape", "enum", DefaultValue = "Rectangle", Options = new[] { "Rectangle|Rectangle", "Ellipse|Ellipse", "Cross|Cross" })]
[OperatorParam("KernelWidth", "Kernel Width", "int", DefaultValue = 3, Min = 1, Max = 99)]
[OperatorParam("KernelHeight", "Kernel Height", "int", DefaultValue = 3, Min = 1, Max = 99)]
[OperatorParam("Iterations", "Iterations", "int", DefaultValue = 1, Min = 1, Max = 100)]
public class RegionDilationOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionDilation;

    public RegionDilationOperator(ILogger<RegionDilationOperator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rectangle");
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3, 1, 99);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3, 1, 99);
        var iterations = GetIntParam(@operator, "Iterations", 1, 1, 100);

        // 获取输入区域
        if (!TryGetInputRegion(inputs, "Region", out var region) || region == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input region is required."));
        }

        if (region.IsEmpty)
        {
            return Task.FromResult(CreateEmptyOutput());
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 创建形态学核
        var shape = kernelShape.ToLowerInvariant() switch
        {
            "ellipse" => MorphologyKernelShape.Ellipse,
            "cross" => MorphologyKernelShape.Cross,
            _ => MorphologyKernelShape.Rectangle
        };
        var kernel = new MorphologyKernel(shape, kernelWidth, kernelHeight);

        // 执行膨胀
        var dilatedRegion = DilateRegion(region, kernel, iterations);

        stopwatch.Stop();

        // 创建可视化图像
        Mat visualization;
        if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
        {
            visualization = CreateVisualization(imageWrapper.GetMat(), region, dilatedRegion);
        }
        else
        {
            visualization = CreateRegionVisualization(region, dilatedRegion);
        }

        var resultData = new Dictionary<string, object>
        {
            { "Region", dilatedRegion },
            { "OriginalArea", region.Area },
            { "Area", dilatedRegion.Area },
            { "AreaIncrease", dilatedRegion.Area - region.Area },
            { "IncreaseRatio", region.Area > 0 ? (double)(dilatedRegion.Area - region.Area) / region.Area : 0 },
            { "Iterations", iterations },
            { "Kernel", new { Shape = kernelShape, Width = kernelWidth, Height = kernelHeight } },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, resultData)));
    }

    private Region DilateRegion(Region region, MorphologyKernel kernel, int iterations)
    {
        var current = region;

        for (int i = 0; i < iterations; i++)
        {
            current = DilateOnce(current, kernel);
        }

        return current;
    }

    private Region DilateOnce(Region region, MorphologyKernel kernel)
    {
        if (region.IsEmpty) return new Region();

        // 获取核的偏移量
        var offsets = kernel.GetOffsets().ToList();
        if (offsets.Count == 0) return region;

        // 对于膨胀，点在结果中当且仅当核与原区域有交集
        // 即：存在某个偏移量 (dx, dy)，使得点 (x+dx, y+dy) 在原区域中

        var expandedRuns = new HashSet<(int x, int y)>();

        foreach (var run in region.RunLengths)
        {
            int y = run.Y;

            for (int x = run.StartX; x <= run.EndX; x++)
            {
                // 对于区域内的每个点，将其邻域（由核定义）加入结果
                foreach (var (dx, dy) in offsets)
                {
                    expandedRuns.Add((x + dx, y + dy));
                }
            }
        }

        // 将点集转换为游程编码
        return PointsToRuns(expandedRuns);
    }

    private Region PointsToRuns(HashSet<(int x, int y)> points)
    {
        if (points.Count == 0) return new Region();

        // 按Y坐标分组
        var byY = points.GroupBy(p => p.y).OrderBy(g => g.Key);
        var runs = new List<RunLength>();

        foreach (var group in byY)
        {
            int y = group.Key;
            var xCoords = group.Select(p => p.x).OrderBy(x => x).ToList();

            // 合并连续的X坐标
            int startX = xCoords[0];
            int prevX = startX;

            for (int i = 1; i < xCoords.Count; i++)
            {
                if (xCoords[i] > prevX + 1)
                {
                    // 不连续，结束当前游程
                    runs.Add(new RunLength(y, startX, prevX));
                    startX = xCoords[i];
                }
                prevX = xCoords[i];
            }

            // 添加最后一个游程
            runs.Add(new RunLength(y, startX, prevX));
        }

        return new Region(runs);
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs == null) return false;

        if (inputs.TryGetValue(key, out var value) && value is Region r)
        {
            region = r;
            return true;
        }

        return false;
    }

    private Mat CreateVisualization(Mat background, Region original, Region dilated)
    {
        var result = background.Clone();

        // 绘制膨胀后区域轮廓（半透明红色）
        using var dilatedMat = dilated.ToMat();
        var dilatedBbox = dilated.BoundingBox;
        var dilatedRoi = new Rect(dilatedBbox.X, dilatedBbox.Y, dilatedMat.Width, dilatedMat.Height);
        
        if (dilatedRoi.X >= 0 && dilatedRoi.Y >= 0 && dilatedRoi.Right <= result.Width && dilatedRoi.Bottom <= result.Height)
        {
            using var colorMat = new Mat(dilatedMat.Size(), MatType.CV_8UC3, new Scalar(0, 0, 255));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, dilatedMat);
            Cv2.AddWeighted(result[dilatedRoi], 0.7, colorMat, 0.3, 0, result[dilatedRoi]);
        }

        // 绘制原始区域（绿色）
        using var originalMat = original.ToMat();
        var originalBbox = original.BoundingBox;
        var originalRoi = new Rect(originalBbox.X, originalBbox.Y, originalMat.Width, originalMat.Height);
        
        if (originalRoi.X >= 0 && originalRoi.Y >= 0 && originalRoi.Right <= result.Width && originalRoi.Bottom <= result.Height)
        {
            using var colorMat = new Mat(originalMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, originalMat);
            Cv2.AddWeighted(result[originalRoi], 0.7, colorMat, 0.5, 0, result[originalRoi]);
        }

        // 添加信息文本
        Cv2.PutText(result, $"Dilation: {original.Area} -> {dilated.Area}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);

        return result;
    }

    private Mat CreateRegionVisualization(Region original, Region dilated)
    {
        var bbox = dilated.BoundingBox;
        int pad = 20;
        int width = Math.Max(400, bbox.Width + pad * 2);
        int height = Math.Max(300, bbox.Height + pad * 2);

        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        // 绘制膨胀后区域轮廓（红色填充）
        using var dilatedMat = dilated.ToMat();
        var dilatedBbox = dilated.BoundingBox;
        var dilatedRoi = new Rect(dilatedBbox.X - bbox.X + pad, dilatedBbox.Y - bbox.Y + pad, 
            dilatedMat.Width, dilatedMat.Height);
        
        if (dilatedRoi.X >= 0 && dilatedRoi.Y >= 0 && dilatedRoi.Right <= width && dilatedRoi.Bottom <= height)
        {
            using var colorMat = new Mat(dilatedMat.Size(), MatType.CV_8UC3, new Scalar(0, 0, 255));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, dilatedMat);
            colorMat.CopyTo(mat[dilatedRoi], dilatedMat);
        }

        // 绘制原始区域轮廓（绿色边框）
        var originalPoints = original.GetContourPoints();
        if (originalPoints.Count > 0)
        {
            var shiftedPoints = originalPoints.Select(p => new Point(p.X - bbox.X + pad, p.Y - bbox.Y + pad)).ToArray();
            Cv2.Polylines(mat, new[] { shiftedPoints }, true, new Scalar(0, 255, 0), 2);
        }

        // 添加信息文本
        Cv2.PutText(mat, $"Original Area: {original.Area}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        Cv2.PutText(mat, $"Dilated Area: {dilated.Area}", new Point(10, 60),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 0, 255), 2);

        return mat;
    }

    private OperatorExecutionOutput CreateEmptyOutput()
    {
        var mat = new Mat(300, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.PutText(mat, "Empty Region", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

        return OperatorExecutionOutput.Success(CreateImageOutput(mat, new Dictionary<string, object>
        {
            { "Region", new Region() },
            { "Area", 0 },
            { "Message", "Input region is empty" }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3);
        var iterations = GetIntParam(@operator, "Iterations", 1);

        if (kernelWidth < 1 || kernelWidth > 99)
            return ValidationResult.Invalid("KernelWidth must be between 1 and 99.");

        if (kernelHeight < 1 || kernelHeight > 99)
            return ValidationResult.Invalid("KernelHeight must be between 1 and 99.");

        if (iterations < 1 || iterations > 100)
            return ValidationResult.Invalid("Iterations must be between 1 and 100.");

        var kernelShape = GetStringParam(@operator, "KernelShape", "Rectangle");
        var validShapes = new[] { "Rectangle", "Ellipse", "Cross" };
        if (!validShapes.Contains(kernelShape, StringComparer.OrdinalIgnoreCase))
            return ValidationResult.Invalid($"KernelShape must be one of: {string.Join(", ", validShapes)}");

        return ValidationResult.Valid();
    }
}
