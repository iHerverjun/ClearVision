// InverseFFT1DOperator.cs
// 一维逆FFT算子 - 将频域信号转换回时域
// 对标: numpy.fft.ifft, scipy.fftpack.ifft

using Acme.Product.Core.Attributes;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Numerics;

namespace Acme.Product.Infrastructure.Operators;

[OperatorMeta(
    DisplayName = "Inverse FFT 1D",
    Description = "Performs 1D Inverse Fast Fourier Transform to convert frequency spectrum back to time domain.",
    Category = "Frequency",
    IconName = "ifft-1d",
    Keywords = new[] { "IFFT", "InverseFFT", "Fourier", "Reconstruction", "Signal" }
)]
[InputPort("Spectrum", "Input Frequency Spectrum", PortDataType.Any, IsRequired = true)]
[InputPort("OutputSize", "Desired Output Size", PortDataType.Integer, IsRequired = false)]
[OutputPort("Signal", "Reconstructed Signal", PortDataType.Any)]
[OutputPort("Real", "Real Part", PortDataType.Any)]
[OutputPort("Imaginary", "Imaginary Part", PortDataType.Any)]
[OutputPort("Image", "Visualization", PortDataType.Image)]
public class InverseFFT1DOperator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.InverseFFT1D;

    public InverseFFT1DOperator(ILogger<InverseFFT1DOperator> logger) : base(logger) { }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken cancellationToken)
    {
        if (inputs?.TryGetValue("Spectrum", out var spec) != true || spec == null)
            return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum required."));

        int outputSize = GetInt(inputs, "OutputSize", 0);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (spec is Complex[] spectrum)
        {
            int n = outputSize > 0 ? Math.Min(outputSize, spectrum.Length) : spectrum.Length;
            var inputSpectrum = spectrum.Take(n).ToArray();

            // 逆FFT
            double[] realSignal = IFFT(inputSpectrum);

            // 分离实部和虚部 (IFFT结果应该是纯实数)
            var realPart = realSignal;
            var imagPart = new double[realSignal.Length];

            stopwatch.Stop();

            var vis = CreateSignalVisualization(realPart);

            return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(vis, new Dictionary<string, object>
            {
                { "Signal", realSignal },
                { "Real", realPart },
                { "Imaginary", imagPart },
                { "SignalLength", realSignal.Length },
                { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
            })));
        }
        else if (spec is Mat spectrumMat)
        {
            // 2D 频谱处理
            var planes = new List<Mat>();
            Cv2.Split(spectrumMat, out var channels);

            // 假设频谱格式为双通道 (实部, 虚部) 或单通道幅度
            if (channels.Length >= 2)
            {
                using var complex = new Mat();
                Cv2.Merge(new[] { channels[0], channels[1] }, complex);

                using var inverse = new Mat();
                Cv2.Dft(complex, inverse, DftFlags.Inverse | DftFlags.RealOutput);

                // 归一化
                Cv2.Normalize(inverse, inverse, 0, 255, NormTypes.MinMax);
                inverse.ConvertTo(inverse, MatType.CV_8UC1);
                var outputImage = inverse.Clone();

                stopwatch.Stop();

                return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(outputImage, new Dictionary<string, object>
                {
                    { "Signal", outputImage.Clone() },
                    { "ProcessingTimeMs", stopwatch.ElapsedMilliseconds }
                })));
            }
            else
            {
                return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum Mat must have at least 2 channels (real and imaginary)."));
            }
        }

        return Task.FromResult(OperatorExecutionOutput.Failure("Spectrum must be Complex[] or Mat."));
    }

    private double[] IFFT(Complex[] spectrum)
    {
        int n = spectrum.Length;

        // 使用OpenCV的IDFT
        var complexData = new float[n * 2];
        for (int i = 0; i < n; i++)
        {
            complexData[i * 2] = (float)spectrum[i].Real;
            complexData[i * 2 + 1] = (float)spectrum[i].Imaginary;
        }

        using var src = new Mat(n, 1, MatType.CV_32FC2, complexData);
        using var dst = new Mat();
        Cv2.Dft(src, dst, DftFlags.Inverse | DftFlags.RealOutput | DftFlags.Scale);

        var result = new double[n];
        for (int i = 0; i < n; i++)
            result[i] = dst.At<float>(i);

        return result;
    }

    private Mat CreateSignalVisualization(double[] signal)
    {
        int width = Math.Max(512, signal.Length);
        int height = 300;
        var vis = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        // 归一化信号到显示范围
        double minVal = signal.Min();
        double maxVal = signal.Max();
        double range = maxVal - minVal;
        if (range < 1e-10) range = 1;

        int centerY = height / 2;
        int prevX = 0, prevY = centerY;

        for (int i = 0; i < signal.Length; i++)
        {
            int x = (int)(i * (double)width / signal.Length);
            int y = centerY - (int)(100 * (signal[i] - minVal) / range - 50);

            if (i > 0)
                Cv2.Line(vis, new Point(prevX, prevY), new Point(x, y), new Scalar(0, 255, 0), 1);

            prevX = x;
            prevY = y;
        }

        // 绘制中心线
        Cv2.Line(vis, new Point(0, centerY), new Point(width, centerY), new Scalar(100, 100, 100), 1, LineTypes.Link8);

        Cv2.PutText(vis, $"Signal (n={signal.Length})", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 0), 2);
        Cv2.PutText(vis, $"Range: [{minVal:F2}, {maxVal:F2}]", new Point(10, 60), HersheyFonts.HersheySimplex, 0.5, new Scalar(200, 200, 200), 1);

        return vis;
    }

    private int GetInt(Dictionary<string, object>? inputs, string key, int defaultVal) =>
        inputs?.TryGetValue(key, out var v) == true && v is int i ? i : defaultVal;

    public override ValidationResult ValidateParameters(Operator @operator) => ValidationResult.Valid();
}
