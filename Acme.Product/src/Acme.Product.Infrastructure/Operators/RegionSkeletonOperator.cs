// RegionSkeletonOperator.cs
// 区域骨架化算子 - 使用 Zhang-Suen 并行细化算法
// 对标 Halcon: skeleton

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Skeleton",
    Description = "Extracts skeleton using Zhang-Suen thinning algorithm. Preserves topology and connectivity.",
    Category = "Morphology",
    IconName = "region-skeleton",
    Keywords = new[] { "Region", "Skeleton", "Thinning", "ZhangSuen", "Topology" }
)]
[InputPort("Region", "Input Region", PortDataType.Any, IsRequired = true)]
[InputPort("Image", "Reference Image (Optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("Region", "Skeleton Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("SkeletonLength", "Skeleton Length", PortDataType.Integer)]
[OutputPort("BranchPoints", "Branch Point Count", PortDataType.Integer)]
[OutputPort("EndPoints", "End Point Count", PortDataType.Integer)]
[OperatorParam("MaxIterations", "Max Iterations", "int", DefaultValue = 100, Min = 1, Max = 1000)]
[OperatorParam("PreserveTopology", "Preserve Topology", "bool", DefaultValue = true)]
public class RegionSkeletonOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionSkeleton;

    public RegionSkeletonOperator(ILogger<RegionSkeletonOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        var maxIterations = GetIntParam(@operator, "MaxIterations", 100, 1, 1000);
        var preserveTopology = GetBoolParam(@operator, "PreserveTopology", true);

        if (!TryGetInputRegion(inputs, "Region", out var region) || region == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Input region required."));

        if (region.IsEmpty) return Task.FromResult(CreateEmptyOutput());

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 转换为二值图像进行骨架化
        using var binaryMat = region.ToMat();
        var skeletonMat = ZhangSuenThinning(binaryMat, maxIterations);
        var skeletonRegion = Region.FromMat(skeletonMat);

        // 分析骨架特征
        var (endPoints, branchPoints) = AnalyzeSkeleton(skeletonMat);

        stopwatch.Stop();

        Mat visualization = TryGetInputImage(inputs, "Image", out var img) && img != null
            ? CreateVisualization(img.GetMat(), region, skeletonRegion, endPoints, branchPoints)
            : CreateRegionVisualization(region, skeletonRegion, endPoints, branchPoints);

        skeletonMat.Dispose();

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(visualization, new Dictionary<string, object>
        {
            { "Region", skeletonRegion },
            { "SkeletonLength", skeletonRegion.Area },
            { "EndPoints", endPoints.Count },
            { "BranchPoints", branchPoints.Count },
            { "OriginalArea", region.Area },
            { "ReductionRatio", region.Area > 0 ? 1.0 - (double)skeletonRegion.Area / region.Area : 0 },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private Mat ZhangSuenThinning(Mat binaryInput, int maxIter)
    {
        var src = binaryInput.Clone();
        var dst = new Mat(src.Size(), MatType.CV_8UC1, Scalar.All(0));
        bool changed = true;
        int iter = 0;

        while (changed && iter < maxIter)
        {
            changed = false;
            iter++;

            // Step 1: 标记要删除的点
            var markers = new bool[src.Rows, src.Cols];

            for (int y = 1; y < src.Rows - 1; y++)
            {
                for (int x = 1; x < src.Cols - 1; x++)
                {
                    if (src.At<byte>(y, x) == 0) continue;

                    var p = GetNeighbors(src, x, y);
                    int A = CountTransitions(p);
                    int B = p.Count(v => v == 255);

                    // Zhang-Suen Step 1 conditions
                    if (B >= 2 && B <= 6 && A == 1 && p[0] * p[2] * p[4] == 0 && p[2] * p[4] * p[6] == 0)
                    {
                        markers[y, x] = true;
                        changed = true;
                    }
                }
            }

            // Delete marked points
            for (int y = 0; y < src.Rows; y++)
                for (int x = 0; x < src.Cols; x++)
                    if (markers[y, x]) src.Set(y, x, (byte)0);

            if (!changed) break;

            // Step 2
            markers = new bool[src.Rows, src.Cols];
            for (int y = 1; y < src.Rows - 1; y++)
            {
                for (int x = 1; x < src.Cols - 1; x++)
                {
                    if (src.At<byte>(y, x) == 0) continue;

                    var p = GetNeighbors(src, x, y);
                    int A = CountTransitions(p);
                    int B = p.Count(v => v == 255);

                    // Zhang-Suen Step 2 conditions
                    if (B >= 2 && B <= 6 && A == 1 && p[0] * p[2] * p[6] == 0 && p[0] * p[4] * p[6] == 0)
                    {
                        markers[y, x] = true;
                        changed = true;
                    }
                }
            }

            for (int y = 0; y < src.Rows; y++)
                for (int x = 0; x < src.Cols; x++)
                    if (markers[y, x]) src.Set(y, x, (byte)0);
        }

        src.CopyTo(dst);
        src.Dispose();
        return dst;
    }

    private List<byte> GetNeighbors(Mat img, int x, int y)
    {
        // P8 P1 P2
        // P7 P0 P3
        // P6 P5 P4
        return new List<byte>
        {
            img.At<byte>(y - 1, x),     // P1
            img.At<byte>(y - 1, x + 1), // P2
            img.At<byte>(y, x + 1),     // P3
            img.At<byte>(y + 1, x + 1), // P4
            img.At<byte>(y + 1, x),     // P5
            img.At<byte>(y + 1, x - 1), // P6
            img.At<byte>(y, x - 1),     // P7
            img.At<byte>(y - 1, x - 1)  // P8
        };
    }

    private int CountTransitions(List<byte> p)
    {
        int count = 0;
        for (int i = 0; i < 8; i++)
            if (p[i] == 0 && p[(i + 1) % 8] == 255)
                count++;
        return count;
    }

    private (List<Point> endPoints, List<Point> branchPoints) AnalyzeSkeleton(Mat skeleton)
    {
        var endPoints = new List<Point>();
        var branchPoints = new List<Point>();

        for (int y = 1; y < skeleton.Rows - 1; y++)
        {
            for (int x = 1; x < skeleton.Cols - 1; x++)
            {
                if (skeleton.At<byte>(y, x) == 0) continue;

                var neighbors = GetNeighbors(skeleton, x, y);
                int count = neighbors.Count(v => v == 255);

                if (count == 1) endPoints.Add(new Point(x, y));
                else if (count >= 3) branchPoints.Add(new Point(x, y));
            }
        }

        return (endPoints, branchPoints);
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs?.TryGetValue(key, out var val) == true && val is Region r) { region = r; return true; }
        return false;
    }

    private Mat CreateVisualization(Mat bg, Region orig, Region skel, List<Point> ends, List<Point> branches)
    {
        var res = bg.Clone();
        using var skelMat = skel.ToMat();
        var bbox = skel.BoundingBox;
        var roi = new Rect(bbox.X, bbox.Y, skelMat.Width, skelMat.Height);
        if (roi.X >= 0 && roi.Y >= 0 && roi.Right <= res.Width && roi.Bottom <= res.Height)
        {
            using var c = new Mat(skelMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 255));
            Cv2.BitwiseAnd(c, c, c, skelMat);
            Cv2.AddWeighted(res[roi], 0.8, c, 0.8, 0, res[roi]);
        }
        foreach (var p in ends) Cv2.Circle(res, p, 3, new Scalar(0, 0, 255), -1);
        foreach (var p in branches) Cv2.Circle(res, p, 4, new Scalar(255, 0, 0), -1);
        Cv2.PutText(res, $"Skeleton: {skel.Area}px E:{ends.Count} B:{branches.Count}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 255), 2);
        return res;
    }

    private Mat CreateRegionVisualization(Region orig, Region skel, List<Point> ends, List<Point> branches)
    {
        var bbox = orig.BoundingBox;
        int pad = 20, w = Math.Max(400, bbox.Width + pad * 2), h = Math.Max(300, bbox.Height + pad * 2);
        var mat = new Mat(h, w, MatType.CV_8UC3, Scalar.Black);
        using var skelMat = skel.ToMat();
        var sbbox = skel.BoundingBox;
        var sroi = new Rect(sbbox.X - bbox.X + pad, sbbox.Y - bbox.Y + pad, skelMat.Width, skelMat.Height);
        if (sroi.X >= 0 && sroi.Y >= 0 && sroi.Right <= w && sroi.Bottom <= h)
        {
            using var c = new Mat(skelMat.Size(), MatType.CV_8UC3, new Scalar(0, 255, 255));
            Cv2.BitwiseAnd(c, c, c, skelMat);
            c.CopyTo(mat[sroi], skelMat);
        }
        foreach (var p in ends) Cv2.Circle(mat, new Point(p.X - bbox.X + pad, p.Y - bbox.Y + pad), 3, new Scalar(0, 0, 255), -1);
        foreach (var p in branches) Cv2.Circle(mat, new Point(p.X - bbox.X + pad, p.Y - bbox.Y + pad), 4, new Scalar(255, 0, 0), -1);
        Cv2.PutText(mat, $"Len:{skel.Area} E:{ends.Count} B:{branches.Count}", new Point(10, 30), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 255, 255), 2);
        return mat;
    }

    private OperatorExecutionOutput CreateEmptyOutput()
    {
        var m = new Mat(300, 400, MatType.CV_8UC3, Scalar.Black);
        Cv2.PutText(m, "Empty Region", new Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
        return OperatorExecutionOutput.Success(CreateImageOutput(m, new Dictionary<string, object> { { "Region", new Region() }, { "SkeletonLength", 0 }, { "EndPoints", 0 }, { "BranchPoints", 0 } }));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        var maxIter = GetIntParam(@operator, "MaxIterations", 100);
        if (maxIter < 1 || maxIter > 1000) return ValidationResult.Invalid("MaxIterations 1-1000.");
        return ValidationResult.Valid();
    }
}
