// FFT1DOperator.cs
// 一维FFT算子 - 对1D信号进行快速傅里叶变换
// 对标: numpy.fft.fft, scipy.fftpack

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Numerics;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "FFT 1D",
    Description = "Performs 1D Fast Fourier Transform on input signal or image rows/columns.",
    Category = "Frequency",
    IconName = "fft-1d",
    Keywords = new[] { "FFT", "Fourier", "Frequency", "Spectrum", "DFT" }
)]
[InputPort("Input", "Input Signal or Image", PortDataType.Any, IsRequired = true)]
[InputPort("Axis", "Transform Axis (0=row, 1=col)", PortDataType.Integer, IsRequired = false)]
[OutputPort("Spectrum", "Frequency Spectrum", PortDataType.Any)]
[OutputPort("Magnitude", "Magnitude Spectrum", PortDataType.Any)]
[OutputPort("Phase", "Phase Spectrum", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class FFT1DOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.FFT1D;

    public FFT1DOperator(ILogger<FFT1DOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (inputs?.TryGetValue("Input", out var input) != true || input == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Input required."));

        int axis = GetInt(inputs, "Axis", 0);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Complex[]? spectrum = null;
        double[]? magnitudes = null;
        double[]? phases = null;
        Mat? vis = null;

        if (input is double[] signal || input is float[] fsignal || input is int[] isignal)
        {
            // 1D 信号处理
            double[] data = input switch
            {
                double[] d => d,
                float[] f => f.Select(x => (double)x).ToArray(),
                int[] i => i.Select(x => (double)x).ToArray(),
                _ => Array.Empty<double>()
            };

            spectrum = FFT(data);
            magnitudes = spectrum.Select(c => c.Magnitude).ToArray();
            phases = spectrum.Select(c => c.Phase).ToArray();

            vis = CreateSpectrumVisualization(magnitudes, phases);
        }
        else if (input is Mat image)
        {
            // 2D 图像处理 - 按指定轴进行FFT
            using var gray = image.Channels() == 3 ? image.CvtColor(ColorConversionCodes.BGR2GRAY) : image.Clone();

            if (axis == 0)
            {
                // 对每行进行FFT
                var rowSpectra = new List<Complex[]>();
                var rowMags = new List<double[]>();

                for (int y = 0; y < gray.Rows; y++)
                {
                    var row = new double[gray.Cols];
                    for (int x = 0; x < gray.Cols; x++)
                        row[x] = gray.At<byte>(y, x);

                    var rowSpectrum = FFT(row);
                    rowSpectra.Add(rowSpectrum);
                    rowMags.Add(rowSpectrum.Select(c => c.Magnitude).ToArray());
                }

                // 创建幅度图
                vis = new Mat(rowMags.Count, rowMags[0].Length, MatType.CV_8UC1);
                double maxMag = rowMags.SelectMany(m => m).Max();
                for (int y = 0; y < vis.Rows; y++)
                {
                    for (int x = 0; x < vis.Cols; x++)
                    {
                        byte val = (byte)(255 * rowMags[y][x] / (maxMag + 1e-10));
                        vis.At<byte>(y, x) = val;
                    }
                }
            }
            else
            {
                // 对每列进行FFT
                var colSpectra = new List<Complex[]>();
                var colMags = new List<double[]>();

                for (int x = 0; x < gray.Cols; x++)
                {
                    var col = new double[gray.Rows];
                    for (int y = 0; y < gray.Rows; y++)
                        col[y] = gray.At<byte>(y, x);

                    var colSpectrum = FFT(col);
                    colSpectra.Add(colSpectrum);
                    colMags.Add(colSpectrum.Select(c => c.Magnitude).ToArray());
                }

                // 创建幅度图
                vis = new Mat(colMags[0].Length, colMags.Count, MatType.CV_8UC1);
                double maxMag = colMags.SelectMany(m => m).Max();
                for (int x = 0; x < vis.Cols; x++)
                {
                    for (int y = 0; y < vis.Rows; y++)
                    {
                        byte val = (byte)(255 * colMags[x][y] / (maxMag + 1e-10));
                        vis.At<byte>(y, x) = val;
                    }
                }
            }

            Cv2.ApplyColorMap(vis, vis, ColormapTypes.Jet);
        }
        else
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("Input must be 1D array or Mat image."));
        }

        stopwatch.Stop();

        return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
        {
            { "Spectrum", spectrum ?? Array.Empty<Complex>() },
            { "Magnitude", magnitudes ?? Array.Empty<double>() },
            { "Phase", phases ?? Array.Empty<double>() },
            { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
        })));
    }

    private Complex[] FFT(double[] input)
    {
        int n = input.Length;
        // 使用OpenCV的DFT进行FFT计算
        using var src = new Mat(n, 1, MatType.CV_64FC1, input);
        using var dst = new Mat();
        Cv2.Dft(src, dst, DftFlags.ComplexOutput);

        var result = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            var val = dst.At<Vec2d>(i);
            result[i] = new Complex(val[0], val[1]);
        }

        return result;
    }

    private Mat CreateSpectrumVisualization(double[] magnitudes, double[] phases)
    {
        int width = Math.Max(512, magnitudes.Length * 2);
        int height = 300;
        var vis = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        // 归一化
        double maxMag = magnitudes.Max();
        double maxPhase = Math.PI;

        // 绘制幅度谱
        int centerY = height / 2 - 20;
        for (int i = 0; i < magnitudes.Length - 1; i++)
        {
            int x1 = (int)(i * (double)width / magnitudes.Length);
            int x2 = (int)((i + 1) * (double)width / magnitudes.Length);
            int y1 = centerY - (int)(100 * magnitudes[i] / (maxMag + 1e-10));
            int y2 = centerY - (int)(100 * magnitudes[i + 1] / (maxMag + 1e-10));
            Cv2.Line(vis, new OpenCvSharp.Point(x1, y1), new OpenCvSharp.Point(x2, y2), new OpenCvSharp.Scalar(0, 255, 0), 1);
        }

        // 绘制相位谱
        centerY = height - 50;
        for (int i = 0; i < phases.Length - 1; i++)
        {
            int x1 = (int)(i * (double)width / phases.Length);
            int x2 = (int)((i + 1) * (double)width / phases.Length);
            int y1 = centerY - (int)(40 * phases[i] / maxPhase);
            int y2 = centerY - (int)(40 * phases[i + 1] / maxPhase);
            Cv2.Line(vis, new OpenCvSharp.Point(x1, y1), new OpenCvSharp.Point(x2, y2), new OpenCvSharp.Scalar(255, 0, 255), 1);
        }

        Cv2.PutText(vis, "Magnitude", new OpenCvSharp.Point(10, 30), HersheyFonts.HersheySimplex, 0.5, new OpenCvSharp.Scalar(0, 255, 0), 1);
        Cv2.PutText(vis, "Phase", new OpenCvSharp.Point(10, height - 70), HersheyFonts.HersheySimplex, 0.5, new OpenCvSharp.Scalar(255, 0, 255), 1);

        return vis;
    }

    private int GetInt(Dictionary<string, object>? inputs, string key, int defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true && v is int i ? i : defaultVal;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}

public static class ComplexExtensions
{
    public static double Magnitude(this Complex c) => c.Magnitude;
    public static double Phase(this Complex c) => c.Phase;
}
