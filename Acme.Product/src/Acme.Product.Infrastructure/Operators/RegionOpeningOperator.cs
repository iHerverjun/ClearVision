// RegionOpeningOperator.cs
// 区域开运算算子 - 先腐蚀后膨胀，用于去除小噪声
// 对标 Halcon: opening_region
// 作者：AI Assistant

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Opening",
    Description = "Opening operation (erosion followed by dilation) for noise removal and smooth region boundaries.",
    Category = "Morphology",
    IconName = "region-opening",
    Keywords = new[] { "Region", "Opening", "Morphology", "NoiseRemoval", "Smooth" }
)]
[InputPort("Region", "Input Region", PortDataType.Any, IsRequired = true)]
[InputPort("Image", "Reference Image (Optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("Region", "Opened Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Opened Area", PortDataType.Integer)]
[OperatorParam("KernelShape", "Structuring Element Shape", "enum", DefaultValue = "Rectangle", Options = new[] { "Rectangle|Rectangle", "Ellipse|Ellipse", "Cross|Cross" })]
[OperatorParam("KernelWidth", "Kernel Width", "int", DefaultValue = 3, Min = 1, Max = 99)]
[OperatorParam("KernelHeight", "Kernel Height", "int", DefaultValue = 3, Min = 1, Max = 99)]
public class RegionOpeningOperator : OperatorBase
{
    private readonly RegionErosionOperator _erosionOperator;
    private readonly RegionDilationOperator _dilationOperator;

    public override OperatorType OperatorType => OperatorType.RegionOpening;

    public RegionOpeningOperator(ILogger<RegionOpeningOperator> logger) : base(logger)
    {
        _erosionOperator = new RegionErosionOperator(NullLogger<RegionErosionOperator>.Instance);
        _dilationOperator = new RegionDilationOperator(NullLogger<RegionDilationOperator>.Instance);
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rectangle");
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3, 1, 99);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3, 1, 99);

        if (!TryGetInputRegion(inputs, "Region", out var region) || region == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input region is required."));
        }

        if (region.IsEmpty)
        {
            return Task.FromResult(CreateEmptyOutput());
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 开运算 = 先腐蚀后膨胀
        var shape = kernelShape.ToLowerInvariant() switch
        {
            "ellipse" => MorphologyKernelShape.Ellipse,
            "cross" => MorphologyKernelShape.Cross,
            _ => MorphologyKernelShape.Rectangle
        };
        var kernel = new MorphologyKernel(shape, kernelWidth, kernelHeight);

        var eroded = Erode(region, kernel);
        var opened = Dilate(eroded, kernel);

        stopwatch.Stop();

        Mat visualization;
        if (TryGetInputImage(inputs, "Image", out var imageWrapper) && imageWrapper != null)
        {
            visualization = CreateVisualization(imageWrapper.GetMat(), region, opened);
        }
        else
        {
            visualization = CreateRegionVisualization(region, opened);
        }

        var resultData = new Dictionary<string, object>
        {
            { "Region", opened },
            { "OriginalArea", region.Area },
            { "Area", opened.Area },
            { "AreaChange", opened.Area - region.Area },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds },
            { "Kernel", new { Shape = kernelShape, Width = kernelWidth, Height = kernelHeight } }
        };

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, resultData)));
    }

    private Region Erode(Region region, MorphologyKernel kernel)
    {
        var offsets = kernel.GetOffsets().ToList();
        if (offsets.Count == 0) return region;

        var resultRuns = new List<RunLength>();

        foreach (var run in region.RunLengths)
        {
            int y = run.Y;

            for (int x = run.StartX; x <= run.EndX; x++)
            {
                bool allInside = true;
                foreach (var (dx, dy) in offsets)
                {
                    if (!region.ContainsPoint(x + dx, y + dy))
                    {
                        allInside = false;
                        break;
                    }
                }

                if (allInside)
                {
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
                        else nextInside = false;

                        if (!nextInside) break;
                        x++;
                    }
                    resultRuns.Add(new RunLength(y, startX, x));
                }
            }
        }

        return new Region(resultRuns).MergeAdjacentRuns();
    }

    private Region Dilate(Region region, MorphologyKernel kernel)
    {
        var offsets = kernel.GetOffsets().ToList();
        if (offsets.Count == 0) return region;

        var expanded = new HashSet<(int x, int y)>();

        foreach (var run in region.RunLengths)
        {
            for (int x = run.StartX; x <= run.EndX; x++)
            {
                foreach (var (dx, dy) in offsets)
                {
                    expanded.Add((x + dx, run.Y + dy));
                }
            }
        }

        return PointsToRuns(expanded);
    }

    private Region PointsToRuns(HashSet<(int x, int y)> points)
    {
        if (points.Count == 0) return new Region();

        var byY = points.GroupBy(p => p.Item2).OrderBy(g => g.Key);
        var runs = new List<RunLength>();

        foreach (var group in byY)
        {
            int y = group.Key;
            var xCoords = group.Select(p => p.Item1).OrderBy(x => x).ToList();

            int startX = xCoords[0];
            int prevX = startX;

            for (int i = 1; i < xCoords.Count; i++)
            {
                if (xCoords[i] > prevX + 1)
                {
                    runs.Add(new RunLength(y, startX, prevX));
                    startX = xCoords[i];
                }
                prevX = xCoords[i];
            }
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

    private Mat CreateVisualization(Mat background, Region original, Region opened)
    {
        var result = background.Clone();
        using var openedMat = opened.ToMat();
        var bbox = opened.BoundingBox;
        var roi = new Rect(bbox.X, bbox.Y, openedMat.Width, openedMat.Height);
        if (roi.X >= 0 && roi.Y >= 0 && roi.Right <= result.Width && roi.Bottom <= result.Height)
        {
            using var colorMat = new Mat(openedMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, openedMat);
            Cv2.AddWeighted(result[roi], 0.7, colorMat, 0.5, 0, result[roi]);
        }
        Cv2.PutText(result, $"Opening: {original.Area} -> {opened.Area}", new Point(10, 30),
            HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        return result;
    }

    private Mat CreateRegionVisualization(Region original, Region opened)
    {
        var bbox = original.BoundingBox;
        int pad = 20;
        int width = Math.Max(400, bbox.Width + pad * 2);
        int height = Math.Max(300, bbox.Height + pad * 2);
        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        using var openedMat = opened.ToMat();
        var openedBbox = opened.BoundingBox;
        var openedRoi = new Rect(openedBbox.X - bbox.X + pad, openedBbox.Y - bbox.Y + pad, openedMat.Width, openedMat.Height);
        if (openedRoi.X >= 0 && openedRoi.Y >= 0 && openedRoi.Right <= width && openedRoi.Bottom <= height)
        {
            using var colorMat = new Mat(openedMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(colorMat, colorMat, colorMat, openedMat);
            colorMat.CopyTo(mat[openedRoi], openedMat);
        }

        Cv2.PutText(mat, $"Original: {original.Area}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);
        Cv2.PutText(mat, $"Opened: {opened.Area}", new Point(10, 60), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        return mat;
    }

    private OperatorExecutionOutput CreateEmptyOutput()
    {
        var mat = new Mat(300, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.PutText(mat, "Empty Region", new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
        return OperatorExecutionOutput.Success(CreateImageOutput(mat, new Dictionary<string, object>
        {
            { "Region", new Region() },
            { "Area", 0 }
        }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3);
        if (kernelWidth < 1 || kernelWidth > 99) return ValidationResult.Invalid("KernelWidth must be 1-99.");
        if (kernelHeight < 1 || kernelHeight > 99) return ValidationResult.Invalid("KernelHeight must be 1-99.");
        return ValidationResult.Valid();
    }
}
