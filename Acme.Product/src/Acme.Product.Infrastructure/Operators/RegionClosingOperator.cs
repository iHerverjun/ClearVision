// RegionClosingOperator.cs
// 区域闭运算算子 - 先膨胀后腐蚀，用于填充小孔洞
// 对标 Halcon: closing_region

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Closing",
    Description = "Closing operation (dilation followed by erosion) for filling small holes and connecting nearby regions.",
    Category = "Morphology",
    IconName = "region-closing",
    Keywords = new[] { "Region", "Closing", "Morphology", "HoleFilling", "Connect" }
)]
[InputPort("Region", "Input Region", PortDataType.Any, IsRequired = true)]
[InputPort("Image", "Reference Image (Optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("Region", "Closed Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Closed Area", PortDataType.Integer)]
[OperatorParam("KernelShape", "Structuring Element Shape", "enum", DefaultValue = "Rectangle", Options = new[] { "Rectangle|Rectangle", "Ellipse|Ellipse", "Cross|Cross" })]
[OperatorParam("KernelWidth", "Kernel Width", "int", DefaultValue = 3, Min = 1, Max = 99)]
[OperatorParam("KernelHeight", "Kernel Height", "int", DefaultValue = 3, Min = 1, Max = 99)]
public class RegionClosingOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionClosing;

    public RegionClosingOperator(ILogger<RegionClosingOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        var kernelShape = GetStringParam(@operator, "KernelShape", "Rectangle");
        var kernelWidth = GetIntParam(@operator, "KernelWidth", 3, 1, 99);
        var kernelHeight = GetIntParam(@operator, "KernelHeight", 3, 1, 99);

        if (!TryGetInputRegion(inputs, "Region", out var region) || region == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Input region required."));

        if (region.IsEmpty) return Task.FromResult(CreateEmptyOutput());

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var shape = kernelShape.ToLowerInvariant() switch { "ellipse" => MorphologyKernelShape.Ellipse, "cross" => MorphologyKernelShape.Cross, _ => MorphologyKernelShape.Rectangle };
        var kernel = new MorphologyKernel(shape, kernelWidth, kernelHeight);

        // 闭运算 = 先膨胀后腐蚀
        var dilated = Dilate(region, kernel);
        var closed = Erode(dilated, kernel);

        stopwatch.Stop();

        Mat visualization = TryGetInputImage(inputs, "Image", out var img) && img != null
            ? CreateVisualization(img.GetMat(), region, closed)
            : CreateRegionVisualization(region, closed);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, new Dictionary<string, object>
        {
            { "Region", closed },
            { "OriginalArea", region.Area },
            { "Area", closed.Area },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private Region Dilate(Region region, MorphologyKernel kernel)
    {
        var offsets = kernel.GetOffsets().ToList();
        if (offsets.Count == 0) return region;

        var expanded = new HashSet<(int x, int y)>();
        foreach (var run in region.RunLengths)
            for (int x = run.StartX; x <= run.EndX; x++)
                foreach (var (dx, dy) in offsets)
                    expanded.Add((x + dx, run.Y + dy));

        return PointsToRuns(expanded);
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
                bool allInside = offsets.All(off => region.ContainsPoint(x + off.dx, y + off.dy));
                if (allInside)
                {
                    int startX = x;
                    while (x <= run.EndX && offsets.All(off => region.ContainsPoint(x + 1 + off.dx, y + off.dy))) x++;
                    resultRuns.Add(new RunLength(y, startX, x));
                }
            }
        }
        return new Region(resultRuns).MergeAdjacentRuns();
    }

    private Region PointsToRuns(HashSet<(int x, int y)> points)
    {
        if (points.Count == 0) return new Region();
        var runs = new List<RunLength>();
        foreach (var group in points.GroupBy(p => p.Item2).OrderBy(g => g.Key))
        {
            var xs = group.Select(p => p.Item1).OrderBy(x => x).ToList();
            int start = xs[0], prev = start;
            for (int i = 1; i < xs.Count; i++)
            {
                if (xs[i] > prev + 1) { runs.Add(new RunLength(group.Key, start, prev)); start = xs[i]; }
                prev = xs[i];
            }
            runs.Add(new RunLength(group.Key, start, prev));
        }
        return new Region(runs);
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs?.TryGetValue(key, out var val) == true && val is Region r) { region = r; return true; }
        return false;
    }

    private Mat CreateVisualization(Mat bg, Region orig, Region closed)
    {
        var res = bg.Clone();
        using var mat = closed.ToMat();
        var bbox = closed.BoundingBox;
        var roi = new Rect(bbox.X, bbox.Y, mat.Width, mat.Height);
        if (roi.X >= 0 && roi.Y >= 0 && roi.Right <= res.Width && roi.Bottom <= res.Height)
        {
            using var c = new Mat(mat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(c, c, c, mat);
            Cv2.AddWeighted(res[roi], 0.7, c, 0.5, 0, res[roi]);
        }
        Cv2.PutText(res, $"Closing: {orig.Area} -> {closed.Area}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        return res;
    }

    private Mat CreateRegionVisualization(Region orig, Region closed)
    {
        var bbox = orig.BoundingBox;
        int pad = 20, w = Math.Max(400, bbox.Width + pad * 2), h = Math.Max(300, bbox.Height + pad * 2);
        var mat = new Mat(h, w, MatType.CV_8UC3, Scalar.Black);
        using var cmat = closed.ToMat();
        var cbbox = closed.BoundingBox;
        var croi = new Rect(cbbox.X - bbox.X + pad, cbbox.Y - bbox.Y + pad, cmat.Width, cmat.Height);
        if (croi.X >= 0 && croi.Y >= 0 && croi.Right <= w && croi.Bottom <= h)
        {
            using var c = new Mat(cmat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 0));
            Cv2.BitwiseAnd(c, c, c, cmat);
            c.CopyTo(mat[croi], cmat);
        }
        Cv2.PutText(mat, $"Original: {orig.Area}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 2);
        Cv2.PutText(mat, $"Closed: {closed.Area}", new Point(10, 60), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        return mat;
    }

    private OperatorExecutionOutput CreateEmptyOutput()
    {
        var m = new Mat(300, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.PutText(m, "Empty Region", new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
        return OperatorExecutionOutput.Success(CreateImageOutput(m, new Dictionary<string, object> { { "Region", new Region() }, { "Area", 0 } }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var kw = GetIntParam(@operator, "KernelWidth", 3);
        var kh = GetIntParam(@operator, "KernelHeight", 3);
        if (kw < 1 || kw > 99) return ValidationResult.Invalid("KernelWidth 1-99.");
        if (kh < 1 || kh > 99) return ValidationResult.Invalid("KernelHeight 1-99.");
        return ValidationResult.Valid();
    }
}
