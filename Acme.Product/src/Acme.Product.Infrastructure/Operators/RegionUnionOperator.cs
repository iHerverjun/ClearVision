// RegionUnionOperator.cs
// 区域并集算子 - 两个区域的并集
// 对标 Halcon: union2

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Union",
    Description = "Computes the union of two regions (A ∪ B).",
    Category = "Region",
    IconName = "region-union",
    Keywords = new[] { "Region", "Union", "Boolean", "Merge", "Combine" }
)]
[InputPort("Region1", "First Region", PortDataType.Any, IsRequired = true)]
[InputPort("Region2", "Second Region", PortDataType.Any, IsRequired = true)]
[OutputPort("Region", "Union Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Union Area", PortDataType.Integer)]
public class RegionUnionOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionUnion;

    public RegionUnionOperator(ILogger<RegionUnionOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryGetInputRegion(inputs, "Region1", out var r1) || r1 == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region1 required."));
        if (!TryGetInputRegion(inputs, "Region2", out var r2) || r2 == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region2 required."));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 合并游程并排序
        var allRuns = new List<RunLength>();
        allRuns.AddRange(r1.RunLengths);
        allRuns.AddRange(r2.RunLengths);
        allRuns = allRuns.OrderBy(r => r.Y).ThenBy(r => r.StartX).ToList();

        // 合并重叠的游程
        var merged = MergeOverlappingRuns(allRuns);
        var union = new Region(merged);

        stopwatch.Stop();

        var vis = CreateVisualization(r1, r2, union);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "Region", union },
            { "Area", union.Area },
            { "Region1Area", r1.Area },
            { "Region2Area", r2.Area },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private List<RunLength> MergeOverlappingRuns(List<RunLength> runs)
    {
        if (runs.Count <= 1) return runs;

        var result = new List<RunLength>();
        var current = runs[0];

        for (int i = 1; i < runs.Count; i++)
        {
            var next = runs[i];

            if (next.Y == current.Y && next.StartX <= current.EndX + 1)
            {
                // 同一行且重叠或相邻，合并
                current = new RunLength(current.Y, current.StartX, Math.Max(current.EndX, next.EndX));
            }
            else
            {
                result.Add(current);
                current = next;
            }
        }
        result.Add(current);

        return result;
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs?.TryGetValue(key, out var val) == true && val is Region r) { region = r; return true; }
        return false;
    }

    private Mat CreateVisualization(Region r1, Region r2, Region uni)
    {
        var bbox = uni.BoundingBox;
        int pad = 20, w = Math.Max(400, bbox.Width + pad * 2), h = Math.Max(300, bbox.Height + pad * 2);
        var mat = new Mat(h, w, MatType.CV_8UC3, Scalar.Black);

        // 绘制 Region1 (蓝色半透明)
        DrawRegion(mat, r1, bbox, new Scalar(255, 0, 0), pad, 0.3);
        // 绘制 Region2 (红色半透明)
        DrawRegion(mat, r2, bbox, new Scalar(0, 0, 255), pad, 0.3);
        // 绘制并集 (绿色)
        DrawRegionContour(mat, uni, bbox, new Scalar(0, 255, 0), pad, 2);

        Cv2.PutText(mat, $"Union Area: {uni.Area}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
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

    private void DrawRegionContour(Mat mat, Region region, OpenCvSharp.Rect bbox, OpenCvSharp.Scalar color, int pad, int thickness)
    {
        var pts = region.GetContourPoints();
        if (pts.Count > 0)
        {
            var shifted = pts.Select(p => new Point(p.X - bbox.X + pad, p.Y - bbox.Y + pad)).ToArray();
            Cv2.Polylines(mat, new[] { shifted }, true, color, thickness);
        }
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
