using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

public class SubpixelEdgePoint
{
    public double X { get; set; }

    public double Y { get; set; }

    public double NormalX { get; set; }

    public double NormalY { get; set; }

    public double Strength { get; set; }

    public override string ToString() =>
        $"({X:F4}, {Y:F4}) N=({NormalX:F4}, {NormalY:F4}) S={Strength:F4}";
}

public class StegerSubpixelEdgeDetector : IDisposable
{
    private const double NumericalEpsilon = 1e-10;

    public double EdgeThreshold { get; set; } = 10.0;

    public double MaxOffset { get; set; } = 0.5;

    public double Sigma { get; set; } = 1.0;

    public List<SubpixelEdgePoint> DetectEdges(Mat image, double cannyLow = 50, double cannyHigh = 150)
    {
        var edgePoints = new List<SubpixelEdgePoint>();

        using var gray = new Mat();
        if (image.Channels() > 1)
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            image.CopyTo(gray);
        }

        using var cannySource = new Mat();
        PrepareCannySource(gray, cannySource);

        using var edges = new Mat();
        Cv2.Canny(cannySource, edges, cannyLow, cannyHigh);

        using var dx = new Mat();
        using var dy = new Mat();
        using var dxx = new Mat();
        using var dyy = new Mat();
        using var dxy = new Mat();
        ComputeDerivatives(gray, dx, dy, dxx, dyy, dxy);

        unsafe
        {
            var edgePtr = (byte*)edges.DataPointer;
            var dxPtr = (double*)dx.DataPointer;
            var dyPtr = (double*)dy.DataPointer;
            var dxxPtr = (double*)dxx.DataPointer;
            var dyyPtr = (double*)dyy.DataPointer;
            var dxyPtr = (double*)dxy.DataPointer;

            var edgeStep = (int)edges.Step();
            var derivStep = (int)dx.Step() / sizeof(double);
            var width = edges.Cols;
            var height = edges.Rows;
            var margin = GetKernelRadius();

            for (var y = margin; y < height - margin; y++)
            {
                for (var x = margin; x < width - margin; x++)
                {
                    if (edgePtr[(y * edgeStep) + x] == 0)
                    {
                        continue;
                    }

                    var idx = (y * derivStep) + x;
                    var point = ComputeSubpixelPoint(
                        x,
                        y,
                        dxPtr[idx],
                        dyPtr[idx],
                        dxxPtr[idx],
                        dyyPtr[idx],
                        dxyPtr[idx]);

                    if (point != null)
                    {
                        edgePoints.Add(point);
                    }
                }
            }
        }

        return edgePoints;
    }

    private void PrepareCannySource(Mat gray, Mat cannySource)
    {
        var kernelSize = Math.Max(3, ((int)Math.Ceiling(EffectiveSigma * 6.0)) | 1);
        Cv2.GaussianBlur(gray, cannySource, new Size(kernelSize, kernelSize), EffectiveSigma, EffectiveSigma);
    }

    private void ComputeDerivatives(Mat gray, Mat dx, Mat dy, Mat dxx, Mat dyy, Mat dxy)
    {
        using var gray64F = new Mat();
        gray.ConvertTo(gray64F, MatType.CV_64F);

        using var smoothingKernel = Mat.FromArray(CreateGaussianKernel());
        using var firstDerivativeKernel = Mat.FromArray(CreateFirstDerivativeKernel());
        using var secondDerivativeKernel = Mat.FromArray(CreateSecondDerivativeKernel());

        Cv2.SepFilter2D(gray64F, dx, MatType.CV_64F, firstDerivativeKernel, smoothingKernel);
        Cv2.SepFilter2D(gray64F, dy, MatType.CV_64F, smoothingKernel, firstDerivativeKernel);
        Cv2.SepFilter2D(gray64F, dxx, MatType.CV_64F, secondDerivativeKernel, smoothingKernel);
        Cv2.SepFilter2D(gray64F, dyy, MatType.CV_64F, smoothingKernel, secondDerivativeKernel);
        Cv2.SepFilter2D(gray64F, dxy, MatType.CV_64F, firstDerivativeKernel, firstDerivativeKernel);
    }

    private SubpixelEdgePoint? ComputeSubpixelPoint(
        int x,
        int y,
        double gx,
        double gy,
        double gxx,
        double gyy,
        double gxy)
    {
        var trace = gxx + gyy;
        var discriminant = ((gxx - gyy) * (gxx - gyy)) + (4.0 * gxy * gxy);
        if (discriminant < 0)
        {
            return null;
        }

        var sqrtDiscriminant = Math.Sqrt(discriminant);
        var lambda1 = (trace + sqrtDiscriminant) / 2.0;
        var lambda2 = (trace - sqrtDiscriminant) / 2.0;
        var lambda = Math.Abs(lambda1) >= Math.Abs(lambda2) ? lambda1 : lambda2;
        if (Math.Abs(lambda) < NumericalEpsilon)
        {
            return null;
        }

        if (!TryComputeNormal(gxx, gyy, gxy, lambda, out var nx, out var ny))
        {
            return null;
        }

        var projectedGradient = (gx * nx) + (gy * ny);
        if (projectedGradient < 0)
        {
            nx = -nx;
            ny = -ny;
            projectedGradient = -projectedGradient;
        }

        var t = -projectedGradient / lambda;
        if (Math.Abs(t) > MaxOffset)
        {
            return null;
        }

        if (projectedGradient < EdgeThreshold)
        {
            return null;
        }

        return new SubpixelEdgePoint
        {
            X = x + (t * nx),
            Y = y + (t * ny),
            NormalX = nx,
            NormalY = ny,
            Strength = projectedGradient
        };
    }

    private static bool TryComputeNormal(
        double gxx,
        double gyy,
        double gxy,
        double lambda,
        out double nx,
        out double ny)
    {
        if (Math.Abs(gxy) > NumericalEpsilon)
        {
            nx = lambda - gyy;
            ny = gxy;

            if ((Math.Abs(nx) + Math.Abs(ny)) < NumericalEpsilon)
            {
                nx = gxy;
                ny = lambda - gxx;
            }
        }
        else if (Math.Abs(lambda - gxx) <= Math.Abs(lambda - gyy))
        {
            nx = 1.0;
            ny = 0.0;
        }
        else
        {
            nx = 0.0;
            ny = 1.0;
        }

        var norm = Math.Sqrt((nx * nx) + (ny * ny));
        if (norm < NumericalEpsilon)
        {
            return false;
        }

        nx /= norm;
        ny /= norm;
        return true;
    }

    private double[] CreateGaussianKernel()
    {
        var radius = GetKernelRadius();
        var sigma = EffectiveSigma;
        var sigma2 = sigma * sigma;
        var kernel = new double[(radius * 2) + 1];
        double sum = 0;

        for (var i = -radius; i <= radius; i++)
        {
            var value = Math.Exp(-(i * i) / (2.0 * sigma2));
            kernel[i + radius] = value;
            sum += value;
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= sum;
        }

        return kernel;
    }

    private double[] CreateFirstDerivativeKernel()
    {
        var radius = GetKernelRadius();
        var sigma = EffectiveSigma;
        var sigma2 = sigma * sigma;
        var gaussian = CreateGaussianKernel();
        var kernel = new double[gaussian.Length];

        for (var i = -radius; i <= radius; i++)
        {
            kernel[i + radius] = -(i / sigma2) * gaussian[i + radius];
        }

        return kernel;
    }

    private double[] CreateSecondDerivativeKernel()
    {
        var radius = GetKernelRadius();
        var sigma = EffectiveSigma;
        var sigma2 = sigma * sigma;
        var sigma4 = sigma2 * sigma2;
        var gaussian = CreateGaussianKernel();
        var kernel = new double[gaussian.Length];

        for (var i = -radius; i <= radius; i++)
        {
            var ii = i * i;
            kernel[i + radius] = ((ii - sigma2) / sigma4) * gaussian[i + radius];
        }

        var mean = kernel.Average();
        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] -= mean;
        }

        return kernel;
    }

    private int GetKernelRadius() => Math.Max(1, (int)Math.Ceiling(EffectiveSigma * 3.0));

    private double EffectiveSigma => Math.Max(0.1, Sigma);

    public (double cx, double cy, double radius, double rmse) FitCircle(List<SubpixelEdgePoint> points)
    {
        if (points.Count < 3)
        {
            throw new ArgumentException("At least 3 points are required to fit a circle.", nameof(points));
        }

        double sumX = 0;
        double sumY = 0;
        double sumX2 = 0;
        double sumY2 = 0;
        double sumXY = 0;
        double sumX3 = 0;
        double sumY3 = 0;
        double sumXY2 = 0;
        double sumX2Y = 0;

        foreach (var p in points)
        {
            var x = p.X;
            var y = p.Y;
            var x2 = x * x;
            var y2 = y * y;

            sumX += x;
            sumY += y;
            sumX2 += x2;
            sumY2 += y2;
            sumXY += x * y;
            sumX3 += x2 * x;
            sumY3 += y2 * y;
            sumXY2 += x * y2;
            sumX2Y += x2 * y;
        }

        var n = points.Count;
        var a1 = 2 * sumX;
        var b1 = 2 * sumY;
        var c1 = n;
        var d1 = -(sumX2 + sumY2);

        var a2 = 2 * sumX2;
        var b2 = 2 * sumXY;
        var c2 = sumX;
        var d2 = -(sumX3 + sumXY2);

        var a3 = 2 * sumXY;
        var b3 = 2 * sumY2;
        var c3 = sumY;
        var d3 = -(sumX2Y + sumY3);

        var det = (a1 * ((b2 * c3) - (b3 * c2))) - (b1 * ((a2 * c3) - (a3 * c2))) + (c1 * ((a2 * b3) - (a3 * b2)));
        if (Math.Abs(det) < NumericalEpsilon)
        {
            return (0, 0, 0, double.MaxValue);
        }

        var detA = (d1 * ((b2 * c3) - (b3 * c2))) - (b1 * ((d2 * c3) - (d3 * c2))) + (c1 * ((d2 * b3) - (d3 * b2)));
        var detB = (a1 * ((d2 * c3) - (d3 * c2))) - (d1 * ((a2 * c3) - (a3 * c2))) + (c1 * ((a2 * d3) - (a3 * d2)));
        var detC = (a1 * ((b2 * d3) - (b3 * d2))) - (b1 * ((a2 * d3) - (a3 * d2))) + (d1 * ((a2 * b3) - (a3 * b2)));

        var cx = -detA / det;
        var cy = -detB / det;
        var c = detC / det;
        var radius = Math.Sqrt((cx * cx) + (cy * cy) - c);

        double rmse = 0;
        foreach (var p in points)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            var dist = Math.Abs(Math.Sqrt((dx * dx) + (dy * dy)) - radius);
            rmse += dist * dist;
        }

        rmse = Math.Sqrt(rmse / n);
        return (cx, cy, radius, rmse);
    }

    public (double a, double b, double c, double rmse) FitLine(List<SubpixelEdgePoint> points)
    {
        if (points.Count < 2)
        {
            throw new ArgumentException("At least 2 points are required to fit a line.", nameof(points));
        }

        double meanX = 0;
        double meanY = 0;
        foreach (var p in points)
        {
            meanX += p.X;
            meanY += p.Y;
        }

        meanX /= points.Count;
        meanY /= points.Count;

        double sxx = 0;
        double syy = 0;
        double sxy = 0;
        foreach (var p in points)
        {
            var dx = p.X - meanX;
            var dy = p.Y - meanY;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }

        var trace = sxx + syy;
        var det = (sxx * syy) - (sxy * sxy);
        var discriminant = (trace * trace) - (4 * det);
        if (discriminant < 0)
        {
            return (0, 0, 0, double.MaxValue);
        }

        var sqrtDiscriminant = Math.Sqrt(discriminant);
        var lambda = (trace - sqrtDiscriminant) / 2.0;

        double nx;
        double ny;
        if (Math.Abs(sxy) > NumericalEpsilon)
        {
            nx = lambda - sxx;
            ny = sxy;
        }
        else
        {
            nx = 0;
            ny = 1;
        }

        var norm = Math.Sqrt((nx * nx) + (ny * ny));
        if (norm > NumericalEpsilon)
        {
            nx /= norm;
            ny /= norm;
        }

        var a = nx;
        var b = ny;
        var c = -((a * meanX) + (b * meanY));

        double rmse = 0;
        foreach (var p in points)
        {
            var dist = Math.Abs((a * p.X) + (b * p.Y) + c);
            rmse += dist * dist;
        }

        rmse = Math.Sqrt(rmse / points.Count);
        return (a, b, c, rmse);
    }

    public void Dispose()
    {
    }
}
