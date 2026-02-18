// StegerSubpixelEdgeDetector.cs
// Steger 亚像素边缘检测算法
// 参考论文: C. Steger, "An unbiased detector of curvilinear structures", IEEE TPAMI, 20(2):113-125, 1998
// 参考论文: H. Farid and E. Simoncelli, "Differentiation of Discrete Multi-Dimensional Signals", IEEE Trans. IP, 13(4):496-508, 2004
// 作者：蘅芜君

using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// 亚像素边缘点
/// </summary>
public class SubpixelEdgePoint
{
    /// <summary>亚像素 X 坐标</summary>
    public double X { get; set; }

    /// <summary>亚像素 Y 坐标</summary>
    public double Y { get; set; }

    /// <summary>边缘法向 X 分量</summary>
    public double NormalX { get; set; }

    /// <summary>边缘法向 Y 分量</summary>
    public double NormalY { get; set; }

    /// <summary>梯度幅值 (边缘强度)</summary>
    public double Strength { get; set; }

    public override string ToString() =>
        $"({X:F4}, {Y:F4}) N=({NormalX:F4}, {NormalY:F4}) S={Strength:F4}";
}

/// <summary>
/// Steger 亚像素边缘检测器
/// 
/// 核心思想:
/// 1. 使用 Farid & Simoncelli 7-tap 滤波器计算图像导数
/// 2. 构建 Hessian 矩阵并求特征向量 (边缘法向)
/// 3. 二阶泰勒展开求极值点偏移量 (亚像素精度)
/// 
/// 可达到 0.01-0.1 像素的定位精度
/// </summary>
public class StegerSubpixelEdgeDetector : IDisposable
{
    #region Farid & Simoncelli 7-tap 滤波器系数

    /// <summary>插值系数 (用于平滑)</summary>
    private static readonly double[] P_VEC = {
        0.004711, 0.069321, 0.245410, 0.361117,
        0.245410, 0.069321, 0.004711
    };

    /// <summary>一阶微分系数</summary>
    private static readonly double[] D1_VEC = {
        -0.018708, -0.125376, -0.193091, 0.000000,
        0.193091, 0.125376, 0.018708
    };

    /// <summary>二阶微分系数</summary>
    private static readonly double[] D2_VEC = {
        0.055336, 0.137778, -0.056554, -0.273118,
        -0.056554, 0.137778, 0.055336
    };

    private readonly Mat _pMat;   // 插值核
    private readonly Mat _d1Mat;  // 一阶微分核
    private readonly Mat _d2Mat;  // 二阶微分核

    #endregion

    /// <summary>边缘强度阈值 (默认 10)</summary>
    public double EdgeThreshold { get; set; } = 10.0;

    /// <summary>最大亚像素偏移 (默认 0.5 像素)</summary>
    public double MaxOffset { get; set; } = 0.5;

    public StegerSubpixelEdgeDetector()
    {
        // 初始化滤波器核
        _pMat = Mat.FromArray(P_VEC);
        _d1Mat = Mat.FromArray(D1_VEC);
        _d2Mat = Mat.FromArray(D2_VEC);
    }

    /// <summary>
    /// 检测亚像素边缘点
    /// </summary>
    /// <param name="image">输入灰度或彩色图像</param>
    /// <param name="cannyLow">Canny 低阈值</param>
    /// <param name="cannyHigh">Canny 高阈值</param>
    /// <returns>亚像素边缘点列表</returns>
    public List<SubpixelEdgePoint> DetectEdges(Mat image, double cannyLow = 50, double cannyHigh = 150)
    {
        var edgePoints = new List<SubpixelEdgePoint>();

        // Step 1: 转换为灰度图
        using var gray = new Mat();
        if (image.Channels() > 1)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else
            image.CopyTo(gray);

        // Step 2: Canny 边缘检测获取像素级边缘
        using var edges = new Mat();
        Cv2.Canny(gray, edges, cannyLow, cannyHigh);

        // Step 3: 计算所有导数图像
        using var dx = new Mat();
        using var dy = new Mat();
        using var dxx = new Mat();
        using var dyy = new Mat();
        using var dxy = new Mat();

        ComputeDerivatives(gray, dx, dy, dxx, dyy, dxy);

        // Step 4: 对每个边缘像素计算亚像素位置
        unsafe
        {
            byte* edgePtr = (byte*)edges.DataPointer;
            double* dxPtr = (double*)dx.DataPointer;
            double* dyPtr = (double*)dy.DataPointer;
            double* dxxPtr = (double*)dxx.DataPointer;
            double* dyyPtr = (double*)dyy.DataPointer;
            double* dxyPtr = (double*)dxy.DataPointer;

            int edgeStep = (int)edges.Step();
            int derivStep = (int)dx.Step() / sizeof(double);
            int width = edges.Cols;
            int height = edges.Rows;

            // 遍历边缘图像 (跳过边界)
            for (int y = 3; y < height - 3; y++)
            {
                for (int x = 3; x < width - 3; x++)
                {
                    if (edgePtr[y * edgeStep + x] == 0) continue;

                    int idx = y * derivStep + x;

                    // 获取导数值
                    double gx = dxPtr[idx];
                    double gy = dyPtr[idx];
                    double gxx = dxxPtr[idx];
                    double gyy = dyyPtr[idx];
                    double gxy = dxyPtr[idx];

                    // 计算亚像素边缘点
                    var point = ComputeSubpixelPoint(x, y, gx, gy, gxx, gyy, gxy);
                    if (point != null)
                    {
                        edgePoints.Add(point);
                    }
                }
            }
        }

        return edgePoints;
    }

    /// <summary>
    /// 计算图像导数 (使用 Farid & Simoncelli 滤波器)
    /// </summary>
    private void ComputeDerivatives(Mat gray, Mat dx, Mat dy, Mat dxx, Mat dyy, Mat dxy)
    {
        // 转换为双精度
        using var gray64F = new Mat();
        gray.ConvertTo(gray64F, MatType.CV_64F);

        // 计算一阶导数: d/dx = d1 ⊗ p, d/dy = p ⊗ d1
        Cv2.SepFilter2D(gray64F, dx, MatType.CV_64F, _d1Mat, _pMat);
        Cv2.SepFilter2D(gray64F, dy, MatType.CV_64F, _pMat, _d1Mat);

        // 计算二阶导数
        Cv2.SepFilter2D(gray64F, dxx, MatType.CV_64F, _d2Mat, _pMat);
        Cv2.SepFilter2D(gray64F, dyy, MatType.CV_64F, _pMat, _d2Mat);
        Cv2.SepFilter2D(gray64F, dxy, MatType.CV_64F, _d1Mat, _d1Mat);
    }

    /// <summary>
    /// 计算单个像素的亚像素位置
    /// 
    /// 算法核心: Steger 方法
    /// 1. 构建 Hessian 矩阵
    /// 2. 求特征向量 (边缘法向)
    /// 3. 二阶泰勒展开求极值点偏移
    /// </summary>
    private SubpixelEdgePoint? ComputeSubpixelPoint(int x, int y,
        double gx, double gy, double gxx, double gyy, double gxy)
    {
        // 构建 Hessian 矩阵: H = [[gyy, gxy], [gxy, gxx]]
        // 注意: 这里使用梯度幅值的二阶导
        double h00 = gyy;
        double h01 = gxy;
        double h10 = gxy;
        double h11 = gxx;

        // 计算 Hessian 的特征值和特征向量
        // 特征值: λ = (h00 + h11 ± sqrt((h00-h11)^2 + 4*h01^2)) / 2
        double trace = h00 + h11;
        double det = h00 * h11 - h01 * h10;
        double discriminant = trace * trace - 4 * det;

        if (discriminant < 0) return null;

        double sqrtDisc = Math.Sqrt(discriminant);
        double lambda1 = (trace + sqrtDisc) / 2.0;
        double lambda2 = (trace - sqrtDisc) / 2.0;

        // 边缘法向对应于最大绝对值特征值的特征向量（沿梯度方向变化最大）
        double lambda = Math.Abs(lambda1) > Math.Abs(lambda2) ? lambda1 : lambda2;

        // 计算特征向量 (边缘法向)
        double nx, ny;
        if (Math.Abs(h01) > 1e-10)
        {
            nx = lambda - h00;
            ny = h01;
        }
        else
        {
            nx = h01;
            ny = lambda - h11;
        }

        // 归一化法向
        double norm = Math.Sqrt(nx * nx + ny * ny);
        if (norm < 1e-10) return null;

        nx /= norm;
        ny /= norm;

        // 计算偏移量 t
        // t = -(gx * nx + gy * ny) / (gxx * nx² + 2*gxy*nx*ny + gyy * ny²)
        double numerator = -(gx * nx + gy * ny);
        double denominator = gxx * nx * nx + 2 * gxy * nx * ny + gyy * ny * ny;

        if (Math.Abs(denominator) < 1e-10) return null;

        double t = numerator / denominator;

        // 检查偏移量是否在合理范围内
        if (Math.Abs(t) > MaxOffset) return null;

        // 检查边缘强度
        double strength = Math.Abs(gx * nx + gy * ny);
        if (strength < EdgeThreshold) return null;

        // 计算亚像素位置
        double subX = x + t * nx;
        double subY = y + t * ny;

        return new SubpixelEdgePoint
        {
            X = subX,
            Y = subY,
            NormalX = nx,
            NormalY = ny,
            Strength = strength
        };
    }

    /// <summary>
    /// 从边缘点拟合圆 (使用最小二乘法)
    /// </summary>
    public (double cx, double cy, double radius, double rmse) FitCircle(List<SubpixelEdgePoint> points)
    {
        if (points.Count < 3)
            throw new ArgumentException("至少需要 3 个点来拟合圆", nameof(points));

        // Kasa 圆拟合算法 (简化版)
        double sumX = 0, sumY = 0;
        double sumX2 = 0, sumY2 = 0;
        double sumXY = 0;
        double sumX3 = 0, sumY3 = 0;
        double sumXY2 = 0, sumX2Y = 0;

        foreach (var p in points)
        {
            double x = p.X;
            double y = p.Y;
            double x2 = x * x;
            double y2 = y * y;

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

        int n = points.Count;

        // 构建线性方程组
        double a1 = 2 * sumX;
        double b1 = 2 * sumY;
        double c1 = n;
        double d1 = -(sumX2 + sumY2);

        double a2 = 2 * sumX2;
        double b2 = 2 * sumXY;
        double c2 = sumX;
        double d2 = -(sumX3 + sumXY2);

        double a3 = 2 * sumXY;
        double b3 = 2 * sumY2;
        double c3 = sumY;
        double d3 = -(sumX2Y + sumY3);

        // 使用克莱姆法则求解
        double det = a1 * (b2 * c3 - b3 * c2) - b1 * (a2 * c3 - a3 * c2) + c1 * (a2 * b3 - a3 * b2);

        if (Math.Abs(det) < 1e-10)
            return (0, 0, 0, double.MaxValue);

        double detA = d1 * (b2 * c3 - b3 * c2) - b1 * (d2 * c3 - d3 * c2) + c1 * (d2 * b3 - d3 * b2);
        double detB = a1 * (d2 * c3 - d3 * c2) - d1 * (a2 * c3 - a3 * c2) + c1 * (a2 * d3 - a3 * d2);
        double detC = a1 * (b2 * d3 - b3 * d2) - b1 * (a2 * d3 - a3 * d2) + d1 * (a2 * b3 - a3 * b2);

        double cx = -detA / det;
        double cy = -detB / det;
        double c = detC / det;

        double radius = Math.Sqrt(cx * cx + cy * cy - c);

        // 计算 RMSE
        double rmse = 0;
        foreach (var p in points)
        {
            double dx = p.X - cx;
            double dy = p.Y - cy;
            double dist = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - radius);
            rmse += dist * dist;
        }
        rmse = Math.Sqrt(rmse / n);

        return (cx, cy, radius, rmse);
    }

    /// <summary>
    /// 拟合直线 (使用最小二乘法)
    /// </summary>
    public (double a, double b, double c, double rmse) FitLine(List<SubpixelEdgePoint> points)
    {
        if (points.Count < 2)
            throw new ArgumentException("至少需要 2 个点来拟合直线", nameof(points));

        // 计算质心
        double meanX = 0, meanY = 0;
        foreach (var p in points)
        {
            meanX += p.X;
            meanY += p.Y;
        }
        meanX /= points.Count;
        meanY /= points.Count;

        // 计算协方差矩阵
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in points)
        {
            double dx = p.X - meanX;
            double dy = p.Y - meanY;
            sxx += dx * dx;
            syy += dy * dy;
            sxy += dx * dy;
        }

        // 直线方向对应于较小特征值的特征向量
        double trace = sxx + syy;
        double det = sxx * syy - sxy * sxy;
        double discriminant = trace * trace - 4 * det;

        if (discriminant < 0)
            return (0, 0, 0, double.MaxValue);

        double sqrtDisc = Math.Sqrt(discriminant);
        double lambda = (trace - sqrtDisc) / 2.0;  // 较小特征值

        double nx, ny;
        if (Math.Abs(sxy) > 1e-10)
        {
            nx = lambda - sxx;
            ny = sxy;
        }
        else
        {
            nx = 0;
            ny = 1;
        }

        double norm = Math.Sqrt(nx * nx + ny * ny);
        if (norm > 1e-10)
        {
            nx /= norm;
            ny /= norm;
        }

        // 直线方程: ax + by + c = 0
        double a = nx;
        double b = ny;
        double c = -(a * meanX + b * meanY);

        // 计算 RMSE
        double rmse = 0;
        foreach (var p in points)
        {
            double dist = Math.Abs(a * p.X + b * p.Y + c);
            rmse += dist * dist;
        }
        rmse = Math.Sqrt(rmse / points.Count);

        return (a, b, c, rmse);
    }

    public void Dispose()
    {
        _pMat?.Dispose();
        _d1Mat?.Dispose();
        _d2Mat?.Dispose();
    }
}
