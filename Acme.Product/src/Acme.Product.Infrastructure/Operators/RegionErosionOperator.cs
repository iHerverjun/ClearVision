// RegionErosionOperator.cs
// 区域腐蚀算子 - 基于游程编码的区域级形态学腐蚀
// 对标 Halcon: erosion_region
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
/// 区域腐蚀算子 - 基于游程编码的区域级形态学腐蚀
/// 对标 Halcon erosion_region
/// </summary>
[OperatorMeta(
    DisplayName = "Region Erosion",
    Description = "Erodes a region using a specified structuring element (Region-based morphology).",
    Category = "Morphology",
    IconName = "region-erosion",
    Keywords = new[] { "Region", "Erosion", "Morphology", "Shrink", "RLE" }
)]
[InputPort("Region", "Input Region", PortDataType.Any, IsRequired = true)]
[InputPort("Image", "Reference Image (Optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("Region", "Eroded Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Eroded Area", PortDataType.Integer)]
[OperatorParam("KernelShape", "Structuring Element Shape", "enum", DefaultValue = "Rectangle", Options = new[] { "Rectangle|Rectangle", "Ellipse|Ellipse", "Cross|Cross" })]
[OperatorParam("KernelWidth", "Kernel Width", "int", DefaultValue = 3, Min = 1, Max = 99)]
[OperatorParam("KernelHeight", "Kernel Height", "int", DefaultValue = 3, Min = 1, Max = 99)]
[OperatorParam("Iterations", "Iterations", "int", DefaultValue = 1, Min = 1, Max = 100)]
public class RegionErosionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionErosion;

    public RegionErosionOperator(ILogger<RegionErosionOperator> logger) : base(logger)
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

        // 执行腐蚀
        var erodedRegion = ErodeRegion(region, kernel, iterations);

        stopwatch.Stop();

        // 创建可视化图像
        Mat visualization;
        if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
        {
            visualization = CreateVisualization(imageWrapper.GetMat(), region, erodedRegion);
        }
        else
        {
            visualization = CreateRegionVisualization(region, erodedRegion);
        }

        var resultData = new Dictionary<string, object>
        {
            { "Region", erodedRegion },
            { "OriginalArea", region.Area },
            { "Area", erodedRegion.Area },
            { "AreaReduction", region.Area - erodedRegion.Area },
            { "ReductionRatio", region.Area > 0 ? (double)(region.Area - erodedRegion.Area) / region.Area : 0 },
            { "Iterations", iterations },
            { "Kernel", new { Shape = kernelShape, Width = kernelWidth, Height = kernelHeight } },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, resultData)));
    }

    private Region ErodeRegion(Region region, MorphologyKernel kernel, int iterations)
    {
        var current = region;

        for (int i = 0; i < iterations; i++)
        {
            current = ErodeOnce(current, kernel);
            if (current.IsEmpty) break;
        }

        return current;
    }

    private Region ErodeOnce(Region region, MorphologyKernel kernel)
    {
        if (region.IsEmpty) return new Region();

        // 获取核的偏移量
        var offsets = kernel.GetOffsets().ToList();
        if (offsets.Count == 0) return region;

        // 对于腐蚀，点在结果中当且仅当核完全包含在输入区域中
        // 即：对于每个偏移量 (dx, dy)，点 (x+dx, y+dy) 必须在原区域中

        var resultRuns = new List<RunLength>();
        var bbox = region.BoundingBox;
        int halfKernelW = kernel.Width / 2;
        int halfKernelH = kernel.Height / 2;

        // 扩展搜索范围
        int searchY1 = bbox.Y - halfKernelH;
        int searchY2 = bbox.Bottom + halfKernelH;

        foreach (var run in region.RunLengths)
        {
            int y = run.Y;

            // 对于游程中的每个点，检查核是否完全包含
            for (int x = run.StartX; x <= run.EndX; x++)
            {
                bool allInside = true;

                foreach (var (dx, dy) in offsets)
                {
                    int checkX = x + dx;
                    int checkY = y + dy;

                    if (!region.ContainsPoint(checkX, checkY))
                    {
                        allInside = false;
                        break;
                    }
                }

                if (allInside)
                {
                    // 找到连续的内部点
                    int startX = x;
                    while (x <= run.EndX)
                    {
                        bool nextInside = true;
                        int nextX = x + 1;

                        if (nextX <= run.EndX)
                        {
                            foreach (var (dx, dy) in offsets)
                            {
                                if (!region.ContainsPoint(nextX + dx, y + dy))
                                {
                                    nextInside = false;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            nextInside = false;
                        }

                        if (!nextInside) break;
                        x++;
                    }

                    resultRuns.Add(new RunLength(y, startX, x));
                }
            }
        }

        // 合并相邻游程
        var result = new Region(resultRuns);
        return result.MergeAdjacentRuns();
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

    private Mat CreateVisualization(Mat background, Region original, Region eroded)
    {
        var result = background.Clone();

        // 绘制原始区域轮廓（半透明）
        using var originalMat = original.ToMat();
        var originalBbox = original.BoundingBox;
        var roi = new Rect(originalBbox.X, originalBbox.Y, originalMat.Width, originalMat.Height);
        
        if (roi.X >= 0 && roi.Y >= 0 && roi.Right <= result.Width && roi.Bottom <= result.Height)
        {
            using var colorMat = new Mat(originalMat.Size(), MatType.CV_8UC3, new Scalar(255, 0, 0));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, originalMat);
            Cv2.AddWeighted(result[roi], 0.7, colorMat, 0.3, 0, result[roi]);
        }

        // 绘制腐蚀后区域（绿色）
        using var erodedMat = eroded.ToMat();
        var erodedBbox = eroded.BoundingBox;
        var erodedRoi = new Rect(erodedBbox.X, erodedBbox.Y, erodedMat.Width, erodedMat.Height);
        
        if (erodedRoi.X >= 0 && erodedRoi.Y >= 0 && erodedRoi.Right <= result.Width && erodedRoi.Bottom <= result.Height)
        {
            using var colorMat = new Mat(erodedMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, erodedMat);
            Cv2.AddWeighted(result[erodedRoi], 0.7, colorMat, 0.5, 0, result[erodedRoi]);
        }

        // 添加信息文本
        Cv2.PutText(result, $"Erosion: {original.Area} -> {eroded.Area}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);

        return result;
    }

    private Mat CreateRegionVisualization(Region original, Region eroded)
    {
        var bbox = original.BoundingBox;
        int pad = 20;
        int width = Math.Max(400, bbox.Width + pad * 2);
        int height = Math.Max(300, bbox.Height + pad * 2);

        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        // 绘制原始区域轮廓（蓝色）
        var originalPoints = original.GetContourPoints();
        if (originalPoints.Count > 0)
        {
            var shiftedPoints = originalPoints.Select(p => new Point(p.X - bbox.X + pad, p.Y - bbox.Y + pad)).ToArray();
            Cv2.Polylines(mat, new[] { shiftedPoints }, true, new Scalar(255, 0, 0), 2);
        }

        // 绘制腐蚀后区域（绿色填充）
        using var erodedMat = eroded.ToMat();
        var erodedBbox = eroded.BoundingBox;
        var erodedRoi = new Rect(erodedBbox.X - bbox.X + pad, erodedBbox.Y - bbox.Y + pad, 
            erodedMat.Width, erodedMat.Height);
        
        if (erodedRoi.X >= 0 && erodedRoi.Y >= 0 && erodedRoi.Right <= width && erodedRoi.Bottom <= height)
        {
            using var colorMat = new Mat(erodedMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, erodedMat);
            colorMat.CopyTo(mat[erodedRoi], erodedMat);
        }

        // 添加信息文本
        Cv2.PutText(mat, $"Original Area: {original.Area}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 0, 0), 2);
        Cv2.PutText(mat, $"Eroded Area: {eroded.Area}", new Point(10, 60),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);

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
