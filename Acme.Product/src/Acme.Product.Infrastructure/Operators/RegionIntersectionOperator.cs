// RegionIntersectionOperator.cs
// 区域交集算子 - 两个区域的交集
// 对标 Halcon: intersection

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Intersection",
    Description = "Computes the intersection of two regions (A ∩ B).",
    Category = "Region",
    IconName = "region-intersection",
    Keywords = new[] { "Region", "Intersection", "Boolean", "Overlap" }
)]
[InputPort("Region1", "First Region", PortDataType.Any, IsRequired = true)]
[InputPort("Region2", "Second Region", PortDataType.Any, IsRequired = true)]
[OutputPort("Region", "Intersection Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Intersection Area", PortDataType.Integer)]
public class RegionIntersectionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionIntersection;

    public RegionIntersectionOperator(ILogger<RegionIntersectionOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryGetInputRegion(inputs, "Region1", out var r1) || r1 == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region1 required."));
        if (!TryGetInputRegion(inputs, "Region2", out var r2) || r2 == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region2 required."));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 计算交集：取两个区域都包含的点
        var intersectRuns = new List<RunLength>();

        foreach (var run1 in r1.RunLengths)
        {
            // 找同一行在r2中的游程
            var runs2SameY = r2.RunLengths.Where(r => r.Y == run1.Y).ToList();

            foreach (var run2 in runs2SameY)
            {
                // 计算两个游程的交集
                int start = Math.Max(run1.StartX, run2.StartX);
                int end = Math.Min(run1.EndX, run2.EndX);

                if (start <= end)
                {
                    intersectRuns.Add(new RunLength(run1.Y, start, end));
                }
            }
        }

        var intersection = new Region(intersectRuns).MergeAdjacentRuns();

        stopwatch.Stop();

        var vis = CreateVisualization(r1, r2, intersection);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "Region", intersection },
            { "Area", intersection.Area },
            { "Region1Area", r1.Area },
            { "Region2Area", r2.Area },
            { "OverlapRatio", Math.Min(r1.Area, r2.Area) > 0 ? (double)intersection.Area / Math.Min(r1.Area, r2.Area) : 0 },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs?.TryGetValue(key, out var val) == true && val is Region r) { region = r; return true; }
        return false;
    }

    private Mat CreateVisualization(Region r1, Region r2, Region inter)
    {
        var bbox = r1.BoundingBox;
        bbox = bbox.Intersect(r2.BoundingBox);
        if (inter.Area > 0) bbox = inter.BoundingBox;

        int pad = 20, w = Math.Max(400, bbox.Width + pad * 2), h = Math.Max(300, bbox.Height + pad * 2);
        var mat = new Mat(h, w, MatType.CV_8UC3, OpenCvSharp.Scalar.Black);

        // 绘制 Region1 (半透明蓝色)
        DrawRegion(mat, r1, bbox, new Scalar(255, 0, 0), pad, 0.2);
        // 绘制 Region2 (半透明红色)
        DrawRegion(mat, r2, bbox, new Scalar(0, 0, 255), pad, 0.2);
        // 绘制交集 (绿色填充)
        DrawRegion(mat, inter, bbox, new Scalar(0, 255, 0), pad, 0.8);

        Cv2.PutText(mat, $"Intersection: {inter.Area}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        return mat;
    }

    private void DrawRegion(Mat mat, Region region, OpenCvSharp.Rect bbox, OpenCvSharp.Scalar color, int pad, double alpha)
    {
        using var rmat = region.ToMat();
        var rbbox = region.BoundingBox;
        var roi = new OpenCvSharp.Rect(rbbox.X - bbox.X + pad, rbbox.Y - bbox.Y + pad, rmat.Width, rmat.Height);
        if (roi.X >= 0 && roi.Y >= 0 && roi.Right <= mat.Width && roi.Bottom <= mat.Height)
        {
            using var c = new Mat(rmat.Size(), MatType.CV_8UC3, new OpenCvSharp.Scalar(color.Val0, color.Val1, color.Val2));
            Cv2.BitwiseAnd(c, c, c, rmat);
            Cv2.AddWeighted(mat[roi], 1 - alpha, c, alpha, 0, mat[roi]);
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
