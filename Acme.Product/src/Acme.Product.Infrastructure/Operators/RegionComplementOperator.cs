// RegionComplementOperator.cs
// 区域补集算子 - 相对于指定图像大小的补集
// 对标 Halcon: complement

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Acme.Product.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Region Complement",
    Description = "Computes the complement of a region relative to an image size.",
    Category = "Region",
    IconName = "region-complement",
    Keywords = new[] { "Region", "Complement", "Invert", "Background" }
)]
[InputPort("Region", "Input Region", PortDataType.Any, IsRequired = true)]
[InputPort("ImageWidth", "Image Width", PortDataType.Integer, IsRequired = false)]
[InputPort("ImageHeight", "Image Height", PortDataType.Integer, IsRequired = false)]
[InputPort("Image", "Reference Image (optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("Region", "Complement Region", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("Area", "Complement Area", PortDataType.Integer)]
public class RegionComplementOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.RegionComplement;

    public RegionComplementOperator(ILogger<RegionComplementOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryGetInputRegion(inputs, "Region", out var region) || region == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Region required."));

        // 获取图像尺寸
        int width = 0, height = 0;
        if (inputs?.TryGetValue("Image", out var img) == true && img is Mat mat)
        {
            width = mat.Width;
            height = mat.Height;
        }
        if (inputs?.TryGetValue("ImageWidth", out var w) == true && w is int iw)
        {
            width = iw;
        }
        if (inputs?.TryGetValue("ImageHeight", out var h) == true && h is int ih)
        {
            height = ih;
        }

        if (width <= 0 || height <= 0)
        {
            // 如果没有指定尺寸，使用区域边界框的扩展
            var bb = region.BoundingBox;
            width = bb.Right + 10;
            height = bb.Bottom + 10;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var compRuns = new List<RunLength>();
        var sortedRuns = region.RunLengths.OrderBy(r => r.Y).ThenBy(r => r.StartX).ToList();
        int runIndex = 0;

        for (int y = 0; y < height; y++)
        {
            var runsInRow = new List<RunLength>();
            while (runIndex < sortedRuns.Count && sortedRuns[runIndex].Y == y)
            {
                runsInRow.Add(sortedRuns[runIndex]);
                runIndex++;
            }

            if (runsInRow.Count == 0)
            {
                // 整行都是背景
                if (width > 0)
                    compRuns.Add(new RunLength(y, 0, width - 1));
                continue;
            }

            // 填充游程间的空隙
            int currentPos = 0;
            foreach (var run in runsInRow.OrderBy(r => r.StartX))
            {
                if (run.StartX > currentPos)
                {
                    compRuns.Add(new RunLength(y, currentPos, run.StartX - 1));
                }
                currentPos = Math.Max(currentPos, run.EndX + 1);
            }

            // 填充行尾
            if (currentPos < width)
            {
                compRuns.Add(new RunLength(y, currentPos, width - 1));
            }
        }

        var complement = new Region(compRuns).MergeAdjacentRuns();

        stopwatch.Stop();

        var vis = CreateVisualization(region, complement, width, height);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "Region", complement },
            { "Area", complement.Area },
            { "InputArea", region.Area },
            { "TotalArea", width * height },
            { "FillRatio", (double)region.Area / (width * height) },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private bool TryGetInputRegion(Dictionary<string, object>? inputs, string key, out Region? region)
    {
        region = null;
        if (inputs?.TryGetValue(key, out var val) == true && val is Region r) { region = r; return true; }
        return false;
    }

    private Mat CreateVisualization(Region region, Region complement, int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, OpenCvSharp.Scalar.Black);

        // 原始区域用蓝色
        using var rmat = region.ToMat();
        var rbbox = region.BoundingBox;
        var roi = new OpenCvSharp.Rect(rbbox.X, rbbox.Y, rmat.Width, rmat.Height);
        if (roi.X >= 0 && roi.Y >= 0 && roi.Right <= mat.Width && roi.Bottom <= mat.Height)
        {
            using var c = new Mat(rmat.Size(), MatType.CV_8UC3, new OpenCvSharp.Scalar(255, 0, 0));
            Cv2.BitwiseAnd(c, c, c, rmat);
            Cv2.AddWeighted(mat[roi], 0.8, c, 0.5, 0, mat[roi]);
        }

        // 补集用绿色轮廓
        var pts = complement.GetContourPoints();
        if (pts.Count > 0)
            Cv2.Polylines(mat, new[] { pts.ToArray() }, true, new OpenCvSharp.Scalar(0, 255, 0), 1);

        Cv2.PutText(mat, $"Complement: {complement.Area}", new OpenCvSharp.Point(10, 20), HersheyFonts.HersheySimplex, 0.5, new OpenCvSharp.Scalar(0, 255, 0), 1);
        return mat;
    }

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
