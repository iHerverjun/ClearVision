// RegionDifferenceOperator.cs
// 区域差集算子 - A - B (在A中但不在B中的点)
// 对标 Halcon: difference

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Difference",
    Description = "Computes the difference of two regions (A - B).",
    Category = "Region",
    IconName = "region-difference",
    Keywords = new[] { "Region", "Difference", "Boolean", "Subtract" }
)]
[InputPort("Region1", "First Region (Minuend)", PortDataType.Any, IsRequired = true)]
[InputPort("Region2", "Second Region (Subtrahend)", PortDataType.Any, IsRequired = true)]
[OutputPort("Region", "Difference Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Difference Area", PortDataType.Integer)]
public class RegionDifferenceOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionDifference;

    public RegionDifferenceOperator(ILogger<RegionDifferenceOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryGetInputRegion(inputs, "Region1", out var r1) || r1 == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region1 required."));
        if (!TryGetInputRegion(inputs, "Region2", out var r2) || r2 == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region2 required."));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var diffRuns = new List<RunLength>();

        foreach (var run1 in r1.RunLengths)
        {
            var runs2SameY = r2.RunLengths.Where(r => r.Y == run1.Y).OrderBy(r => r.StartX).ToList();

            if (runs2SameY.Count == 0)
            {
                // 没有重叠，整个游程保留
                diffRuns.Add(run1);
                continue;
            }

            // 分段减去重叠部分
            int currentStart = run1.StartX;

            foreach (var run2 in runs2SameY)
            {
                if (run2.EndX < currentStart) continue; // run2在左边，无重叠
                if (run2.StartX > run1.EndX) break; // run2在右边，结束

                // run2与当前段有重叠
                if (run2.StartX > currentStart)
                {
                    // 保留重叠前的部分
                    diffRuns.Add(new RunLength(run1.Y, currentStart, run2.StartX - 1));
                }

                // 跳过重叠部分
                currentStart = Math.Max(currentStart, run2.EndX + 1);
                if (currentStart > run1.EndX) break;
            }

            // 保留最后一段
            if (currentStart <= run1.EndX)
            {
                diffRuns.Add(new RunLength(run1.Y, currentStart, run1.EndX));
            }
        }

        var diff = new Region(diffRuns).MergeAdjacentRuns();

        stopwatch.Stop();

        var vis = CreateVisualization(r1, r2, diff);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "Region", diff },
            { "Area", diff.Area },
            { "Region1Area", r1.Area },
            { "Region2Area", r2.Area },
            { "RemovedArea", r1.Area - diff.Area },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs?.TryGetValue(key, out var val) == true && val is Region r) { region = r; return true; }
        return false;
    }

    private Mat CreateVisualization(Region r1, Region r2, Region diff)
    {
        var bbox = r1.BoundingBox;
        int pad = 20, w = Math.Max(400, bbox.Width + pad * 2), h = Math.Max(300, bbox.Height + pad * 2);
        var mat = new Mat(h, w, MatType.CV_8UC3, Scalar.Black);

        // 绘制 Region1 (半透明蓝色)
        DrawRegion(mat, r1, bbox, new Scalar(255, 0, 0), pad, 0.2);
        // 绘制 Region2 (半透明红色)
        DrawRegion(mat, r2, bbox, new Scalar(0, 0, 255), pad, 0.2);
        // 绘制差集 (绿色)
        DrawRegionContour(mat, diff, bbox, new Scalar(0, 255, 0), pad, 2);

        Cv2.PutText(mat, $"Difference: {diff.Area}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
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
