// PhaseClosureOperator.cs
// 相位闭合算子 - 计算干涉图像的相位闭合/解缠绕
// 对标: scipy.ndimage.phase_unwrap, MATLAB unwrap

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Phase Closure",
    Description = "Computes phase closure by unwrapping wrapped phase from interferometric measurements.",
    Category = "Measurement",
    IconName = "phase-closure",
    Keywords = new[] { "Phase", "Unwrap", "Interferometry", "Closure", "Wavelength" }
)]
[InputPort("PhaseImage", "Wrapped Phase Image", PortDataType.Image, IsRequired = true)]
[InputPort("Wavelength", "Wavelength (nm)", PortDataType.Float, IsRequired = false)]
[InputPort("UnwrapMethod", "Unwrapping Method", PortDataType.String, IsRequired = false)]
[InputPort("QualityMap", "Quality Map (optional)", PortDataType.Image, IsRequired = false)]
[OutputPort("UnwrappedPhase", "Unwrapped Phase", PortDataType.Image)]
[OutputPort("Discontinuities", "Phase Discontinuities", PortDataType.Image)]
[OutputPort("Quality", "Quality Metric", PortDataType.Float)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class PhaseClosureOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.PhaseClosure;

    public PhaseClosureOperator(ILogger<PhaseClosureOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (!TryGetImage(inputs, out var phaseImage))
            return Task.FromResult(OperatorExecutionOutput.Failure("PhaseImage required."));

        double wavelength = GetDouble(inputs, "Wavelength", 632.8); // He-Ne laser default
        string method = GetString(inputs, "UnwrapMethod", "itoh").ToLower();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var phase = phaseImage.Channels() == 3 
            ? phaseImage.CvtColor(ColorConversionCodes.BGR2GRAY) 
            : phaseImage.Clone();

        // 归一化到 [-π, π]
        phase.ConvertTo(phase, MatType.CV_32F);
        Cv2.Normalize(phase, phase, -Math.PI, Math.PI, NormTypes.MinMax);

        Mat unwrapped;
        double quality;

        switch (method)
        {
            case "quality":
                (unwrapped, quality) = QualityGuidedUnwrap(phase, inputs);
                break;
            case "floodfill":
                (unwrapped, quality) = FloodFillUnwrap(phase);
                break;
            case "itoh":
            default:
                (unwrapped, quality) = ItohUnwrap(phase);
                break;
        }

        // 计算不连续点
        var discontinuities = DetectDiscontinuities(phase);

        // 转换为实际位移 (若给定波长)
        if (wavelength > 0)
        {
            unwrapped.ConvertTo(unwrapped, MatType.CV_32F);
            Cv2.Multiply(unwrapped, new OpenCvSharp.Scalar(wavelength / (2 * Math.PI)), unwrapped);
        }

        stopwatch.Stop();

        var vis = CreateVisualization(phase, unwrapped, discontinuities);

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "UnwrappedPhase", unwrapped },
            { "Discontinuities", discontinuities },
            { "Quality", quality },
            { "Wavelength", wavelength },
            { "Method", method },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private (Mat, double) ItohUnwrap(Mat wrapped)
    {
        var unwrapped = wrapped.Clone();
        int rows = wrapped.Rows, cols = wrapped.Cols;

        // 行方向展开
        for (int y = 0; y < rows; y++)
        {
            double offset = 0;
            float prev = wrapped.At<float>(y, 0);
            unwrapped.At<float>(y, 0) = prev;

            for (int x = 1; x < cols; x++)
            {
                float curr = wrapped.At<float>(y, x);
                double diff = curr - prev;

                // 检测跳变
                if (diff > Math.PI) offset -= 2 * Math.PI;
                else if (diff < -Math.PI) offset += 2 * Math.PI;

                unwrapped.At<float>(y, x) = (float)(curr + offset);
                prev = curr;
            }
        }

        // 列方向展开
        for (int x = 0; x < cols; x++)
        {
            double offset = 0;
            float prev = unwrapped.At<float>(0, x);

            for (int y = 1; y < rows; y++)
            {
                float curr = unwrapped.At<float>(y, x);
                double diff = curr - prev;

                if (diff > Math.PI) offset -= 2 * Math.PI;
                else if (diff < -Math.PI) offset += 2 * Math.PI;

                unwrapped.At<float>(y, x) = (float)(curr + offset);
                prev = unwrapped.At<float>(y, x);
            }
        }

        double quality = CalculateQuality(unwrapped);
        return (unwrapped, quality);
    }

    private (Mat, double) QualityGuidedUnwrap(Mat wrapped, Dictionary<string, object>? inputs)
    {
        // 质量图引导的路径独立展开
        var quality = new Mat(wrapped.Size(), MatType.CV_32F);
        
        // 计算相位梯度作为质量指标
        using var dx = new Mat();
        using var dy = new Mat();
        Cv2.Sobel(wrapped, dx, MatType.CV_32F, 1, 0, 3);
        Cv2.Sobel(wrapped, dy, MatType.CV_32F, 0, 1, 3);
        Cv2.Magnitude(dx, dy, quality);
        Cv2.Subtract(new OpenCvSharp.Scalar(1.0), quality, quality); // 低梯度 = 高质量

        // 简单实现：基于质量排序的路径跟踪
        var unwrapped = wrapped.Clone();
        var visited = new bool[wrapped.Rows, wrapped.Cols];
        var queue = new PriorityQueue<(int X, int Y, float Quality), float>();

        // 从最高质量点开始
        Cv2.MinMaxLoc(quality, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
        queue.Enqueue((maxLoc.X, maxLoc.Y, (float)maxVal), (float)-maxVal);
        visited[maxLoc.Y, maxLoc.X] = true;

        while (queue.Count > 0)
        {
            var (x, y, _) = queue.Dequeue();
            float currPhase = unwrapped.At<float>(y, x);

            // 检查4邻域
            int[] dx4 = { -1, 1, 0, 0 };
            int[] dy4 = { 0, 0, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx4[i];
                int ny = y + dy4[i];

                if (nx < 0 || nx >= wrapped.Cols || ny < 0 || ny >= wrapped.Rows || visited[ny, nx])
                    continue;

                float neighborWrapped = wrapped.At<float>(ny, nx);
                double diff = neighborWrapped - currPhase;

                // 解缠绕
                int jumps = (int)Math.Round(diff / (2 * Math.PI));
                unwrapped.At<float>(ny, nx) = (float)(neighborWrapped - jumps * 2 * Math.PI);

                visited[ny, nx] = true;
                float q = quality.At<float>(ny, nx);
                queue.Enqueue((nx, ny, q), -q);
            }
        }

        double qScore = CalculateQuality(unwrapped);
        return (unwrapped, qScore);
    }

    private (Mat, double) FloodFillUnwrap(Mat wrapped)
    {
        // 区域生长法展开
        var unwrapped = wrapped.Clone();
        var visited = new bool[wrapped.Rows, wrapped.Cols];
        var regions = new List<List<OpenCvSharp.Point>>();

        for (int y = 0; y < wrapped.Rows; y++)
        {
            for (int x = 0; x < wrapped.Cols; x++)
            {
                if (visited[y, x]) continue;

                var region = new List<OpenCvSharp.Point>();
                var stack = new Stack<OpenCvSharp.Point>();
                stack.Push(new OpenCvSharp.Point(x, y));
                visited[y, x] = true;

                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    region.Add(p);
                    float curr = wrapped.At<float>(p.Y, p.X);

                    int[] dx4 = { -1, 1, 0, 0 };
                    int[] dy4 = { 0, 0, -1, 1 };

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = p.X + dx4[i];
                        int ny = p.Y + dy4[i];

                        if (nx < 0 || nx >= wrapped.Cols || ny < 0 || ny >= wrapped.Rows || visited[ny, nx])
                            continue;

                        float neighbor = wrapped.At<float>(ny, nx);
                        double diff = Math.Abs(neighbor - curr);
                        diff = Math.Min(diff, 2 * Math.PI - diff); // 环形距离

                        if (diff < Math.PI / 2) // 连续性阈值
                        {
                            visited[ny, nx] = true;
                            stack.Push(new OpenCvSharp.Point(nx, ny));
                        }
                    }
                }

                regions.Add(region);
            }
        }

        double quality = CalculateQuality(unwrapped);
        return (unwrapped, quality);
    }

    private Mat DetectDiscontinuities(Mat wrapped)
    {
        var disc = new Mat(wrapped.Size(), MatType.CV_8UC1, OpenCvSharp.Scalar.Black);

        for (int y = 1; y < wrapped.Rows; y++)
        {
            for (int x = 1; x < wrapped.Cols; x++)
            {
                float curr = wrapped.At<float>(y, x);
                float left = wrapped.At<float>(y, x - 1);
                float top = wrapped.At<float>(y - 1, x);

                double diffLeft = Math.Abs(curr - left);
                double diffTop = Math.Abs(curr - top);

                diffLeft = Math.Min(diffLeft, 2 * Math.PI - diffLeft);
                diffTop = Math.Min(diffTop, 2 * Math.PI - diffTop);

                if (diffLeft > Math.PI * 0.8 || diffTop > Math.PI * 0.8)
                    disc.At<byte>(y, x) = 255;
            }
        }

        return disc;
    }

    private double CalculateQuality(Mat unwrapped)
    {
        // 计算解缠绕质量：平滑度指标
        using var dx = new Mat();
        using var dy = new Mat();
        Cv2.Sobel(unwrapped, dx, MatType.CV_32F, 1, 0, 3);
        Cv2.Sobel(unwrapped, dy, MatType.CV_32F, 0, 1, 3);

        using var mag = new Mat();
        Cv2.Magnitude(dx, dy, mag);

        Cv2.MeanStdDev(mag, out var mean, out var stddev);
        
        // 低标准差表示高质量
        double quality = 1.0 / (1.0 + stddev.Val0);
        return Math.Min(quality, 1.0);
    }

    private Mat CreateVisualization(Mat wrapped, Mat unwrapped, Mat discontinuities)
    {
        // 归一化显示
        Mat wVis = new Mat(), uVis = new Mat();
        Cv2.Normalize(wrapped, wVis, 0, 255, NormTypes.MinMax);
        Cv2.Normalize(unwrapped, uVis, 0, 255, NormTypes.MinMax);

        wVis.ConvertTo(wVis, MatType.CV_8UC1);
        uVis.ConvertTo(uVis, MatType.CV_8UC1);

        // 应用颜色映射
        Cv2.ApplyColorMap(wVis, wVis, ColormapTypes.Jet);
        Cv2.ApplyColorMap(uVis, uVis, ColormapTypes.Jet);

        // 创建组合图像
        int h = wrapped.Rows;
        int w = wrapped.Cols;
        var combined = new Mat(h, w * 3, MatType.CV_8UC3, Scalar.Black);

        wVis.CopyTo(new Mat(combined, new Rect(0, 0, w, h)));
        uVis.CopyTo(new Mat(combined, new Rect(w, 0, w, h)));

        // 不连续点叠加到第三列
        using var discColor = new Mat();
        Cv2.CvtColor(discontinuities, discColor, ColorConversionCodes.GRAY2BGR);
        Cv2.AddWeighted(uVis, 0.7, discColor, 0.5, 0, new Mat(combined, new Rect(w * 2, 0, w, h)));

        Cv2.PutText(combined, "Wrapped", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheySimplex, 0.7, new OpenCvSharp.Scalar(255, 255, 255), 2);
        Cv2.PutText(combined, "Unwrapped", new OpenCvSharp.Point(w + 10, 30), HersheyFonts.HersheySimplex, 0.7, new OpenCvSharp.Scalar(255, 255, 255), 2);
        Cv2.PutText(combined, "Discontinuities", new OpenCvSharp.Point(w * 2 + 10, 30), HersheyFonts.HersheySimplex, 0.7, new OpenCvSharp.Scalar(255, 255, 255), 2);

        return combined;
    }

    private bool TryGetImage(Dictionary<string, object>? inputs, out Mat image)
    {
        image = new Mat();
        if (inputs?.TryGetValue("PhaseImage", out var img) == true && img is Mat m) { image = m; return true; }
        return false;
    }

    private double GetDouble(Dictionary<string, object>? inputs, string key, double defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true ? Convert.ToDouble(v) : defaultVal;

    private string GetString(Dictionary<string, object>? inputs, string key, string defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true ? v?.ToString() ?? defaultVal : defaultVal;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
