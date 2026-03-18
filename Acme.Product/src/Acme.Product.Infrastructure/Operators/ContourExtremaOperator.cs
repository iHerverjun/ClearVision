// ContourExtremaOperator.cs
// 轮廓极值点算子 - 查找轮廓在特定方向上的极值点
// 对标 Halcon: extremity, get_contour_xld

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Contour Extrema",
    Description = "Finds extremal points of a contour in specified directions.",
    Category = "Measurement",
    IconName = "contour-extrema",
    Keywords = new[] { "Contour", "Extrema", "Min", "Max", "Boundary" }
)]
[InputPort("Contour", "Input Contour (Points)", PortDataType.Any, IsRequired = true)]
[InputPort("Direction", "Search Direction", PortDataType.String, IsRequired = false)]
[InputPort("ReferencePoint", "Reference Point (optional)", PortDataType.Any, IsRequired = false)]
[OutputPort("ExtremaPoints", "Extremal Points", PortDataType.Any)]
[OutputPort("MinPoint", "Minimum Point", PortDataType.Any)]
[OutputPort("MaxPoint", "Maximum Point", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
[OutputPort("MinValue", "Minimum Value", PortDataType.Float)]
[OutputPort("MaxValue", "Maximum Value", PortDataType.Float)]
public class ContourExtremaOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.ContourExtrema;

    public ContourExtremaOperator(ILogger<ContourExtremaOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryGetContour(inputs, out var contour) || contour == null || contour.Count < 2)
            return Task.FromResult(OperatorExecutionOutput.Failure("Valid contour required (at least 2 points)."));

        string direction = GetString(inputs, "Direction", "horizontal").ToLower();

        OpenCvSharp.Point2f? refPoint = null;
        if (inputs?.TryGetValue("ReferencePoint", out var rp) == true)
        {
            if (rp is OpenCvSharp.Point2f p2f) refPoint = p2f;
            else if (rp is OpenCvSharp.Point p) refPoint = new OpenCvSharp.Point2f(p.X, p.Y);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 计算极值点
        var results = ComputeExtrema(contour, direction, refPoint);

        stopwatch.Stop();

        // 创建可视化图像
        int padding = 50;
        var bbox = Cv2.BoundingRect(contour.ToArray());
        int w = Math.Max(400, bbox.Width + padding * 2);
        int h = Math.Max(300, bbox.Height + padding * 2);

        var vis = new Mat(h, w, MatType.CV_8UC3, OpenCvSharp.Scalar.Black);
        var shiftedContour = contour.Select(p => new OpenCvSharp.Point(p.X - bbox.X + padding, p.Y - bbox.Y + padding)).ToArray();

        // 绘制轮廓
        Cv2.Polylines(vis, new[] { shiftedContour }, false, new OpenCvSharp.Scalar(255, 255, 255), 2);

        // 绘制极值点
        var minPt = results.MinPoint;
        var maxPt = results.MaxPoint;
        var shiftedMin = new OpenCvSharp.Point((int)(minPt.X - bbox.X + padding), (int)(minPt.Y - bbox.Y + padding));
        var shiftedMax = new OpenCvSharp.Point((int)(maxPt.X - bbox.X + padding), (int)(maxPt.Y - bbox.Y + padding));

        Cv2.Circle(vis, shiftedMin, 6, new OpenCvSharp.Scalar(0, 0, 255), -1);
        Cv2.Circle(vis, shiftedMax, 6, new OpenCvSharp.Scalar(0, 255, 0), -1);
        Cv2.PutText(vis, "MIN", new OpenCvSharp.Point(shiftedMin.X + 8, shiftedMin.Y), HersheyFonts.HersheySimplex, 0.5, new OpenCvSharp.Scalar(0, 0, 255), 1);
        Cv2.PutText(vis, "MAX", new OpenCvSharp.Point(shiftedMax.X + 8, shiftedMax.Y), HersheyFonts.HersheySimplex, 0.5, new OpenCvSharp.Scalar(0, 255, 0), 1);

        // 如果有参考点，绘制连接线
        if (refPoint.HasValue)
        {
            var shiftedRef = new Point((int)(refPoint.Value.X - bbox.X + padding), (int)(refPoint.Value.Y - bbox.Y + padding));
            Cv2.Circle(vis, shiftedRef, 5, new OpenCvSharp.Scalar(255, 0, 255), -1);
            Cv2.Line(vis, shiftedRef, shiftedMin, new Scalar(0, 0, 255), 1, LineTypes.Link8);
            Cv2.Line(vis, shiftedRef, shiftedMax, new Scalar(0, 255, 0), 1, LineTypes.Link8);
        }

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "ExtremaPoints", results.AllExtrema },
            { "MinPoint", results.MinPoint },
            { "MaxPoint", results.MaxPoint },
            { "MinValue", results.MinValue },
            { "MaxValue", results.MaxValue },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private ExtremaResult ComputeExtrema(List<OpenCvSharp.Point2f> contour, string direction, OpenCvSharp.Point2f? refPoint)
    {
        var values = new List<(OpenCvSharp.Point2f Point, double Value)>();

        foreach (var pt in contour)
        {
            double value = direction switch
            {
                "horizontal" or "x" => pt.X,
                "vertical" or "y" => pt.Y,
                "distance" when refPoint.HasValue => Distance(pt, refPoint.Value),
                _ => pt.X
            };
            values.Add((pt, value));
        }

        if (direction == "distance" && refPoint.HasValue)
        {
            // 找到距离参考点最远和最近的点
            var ordered = values.OrderBy(v => v.Value).ToList();
            return new ExtremaResult
            {
                MinPoint = ordered.First().Point,
                MaxPoint = ordered.Last().Point,
                MinValue = ordered.First().Value,
                MaxValue = ordered.Last().Value,
                AllExtrema = new List<OpenCvSharp.Point2f> { ordered.First().Point, ordered.Last().Point }
            };
        }
        else
        {
            // 找到最小和最大的点
            var minPt = values.OrderBy(v => v.Value).First();
            var maxPt = values.OrderByDescending(v => v.Value).First();

            return new ExtremaResult
            {
                MinPoint = minPt.Point,
                MaxPoint = maxPt.Point,
                MinValue = minPt.Value,
                MaxValue = maxPt.Value,
                AllExtrema = new List<OpenCvSharp.Point2f> { minPt.Point, maxPt.Point }
            };
        }
    }

    private double Distance(OpenCvSharp.Point2f a, OpenCvSharp.Point2f b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private bool TryGetContour(Dictionary<string, object>? inputs, out List<OpenCvSharp.Point2f>? contour)
    {
        contour = null;
        if (inputs?.TryGetValue("Contour", out var val) != true || val == null)
            return false;

        if (val is IEnumerable<OpenCvSharp.Point2f> pts2f) { contour = pts2f.ToList(); return true; }
        if (val is IEnumerable<OpenCvSharp.Point> pts) { contour = pts.Select(p => new OpenCvSharp.Point2f(p.X, p.Y)).ToList(); return true; }
        if (val is OpenCvSharp.Point[] arr) { contour = arr.Select(p => new OpenCvSharp.Point2f(p.X, p.Y)).ToList(); return true; }
        if (val is OpenCvSharp.Point2f[] arr2f) { contour = arr2f.ToList(); return true; }
        return false;
    }

    private string GetString(Dictionary<string, object>? inputs, string key, string defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true ? v?.ToString() ?? defaultVal : defaultVal;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}

public class ExtremaResult
{
    public OpenCvSharp.Point2f MinPoint { get; set; }
    public OpenCvSharp.Point2f MaxPoint { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public List<OpenCvSharp.Point2f> AllExtrema { get; set; } = new();
}
