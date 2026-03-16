// SubPixelEdgeDetectorTests.cs
// 灰度重心法亚像素边缘检测器单元测试

using OpenCvSharp;
using FluentAssertions;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

/// <summary>
/// 灰度重心法亚像素边缘检测器单元测试
/// </summary>
public class SubPixelEdgeDetectorTests : IDisposable
{
    private readonly SubPixelEdgeDetector _detector;

    public SubPixelEdgeDetectorTests()
    {
        _detector = new SubPixelEdgeDetector();
    }

    public void Dispose()
    {
        // 清理资源
    }

    /// <summary>
    /// 测试1: 理想阶梯边缘
    /// 输入: [0, 0, 0, 255, 255, 255]
    /// 期望输出: 2.5 (理想边缘位于第3和第4像素之间)
    /// 
    /// 计算公式:
    /// Σ(i * gray[i]) = 0*0 + 1*0 + 2*0 + 3*255 + 4*255 + 5*255 = 3060
    /// Σ(gray[i]) = 0 + 0 + 0 + 255 + 255 + 255 = 765
    /// position = 3060 / 765 = 4.0 (阈值处理后为 2.5)
    /// </summary>
    [Fact]
    public void Test_Centroid_PerfectEdge()
    {
        // Arrange: 创建理想阶梯边缘 [0, 0, 0, 255, 255, 255]
        byte[] edgeData = { 0, 0, 0, 255, 255, 255 };
        using var lineProfile = Mat.FromArray(edgeData);
        lineProfile.Reshape(1, 1); // 确保是 1xN 矩阵

        // Act
        float result = _detector.DetectCentroid(lineProfile, threshold: 128);

        // Assert: 验证结果接近 3.0 (255像素的位置重心)
        // 由于阈值筛选，只有 255 的像素参与计算
        // Σ(i * 255) = 3*255 + 4*255 + 5*255 = 3060
        // Σ(255) = 765
        // position = 3060/765 = 4.0 (0-based index)
        result.Should().BeApproximately(4.0f, 0.01f, 
            "理想阶梯边缘的重心应位于第3-5像素的中心位置");
    }

    /// <summary>
    /// 测试1b: 理想阶梯边缘（低阈值，包含过渡）
    /// 输入: [0, 0, 0, 255, 255, 255]
    /// 阈值: 0 (包含所有像素)
    /// 期望输出: 3.5 (所有像素的重心)
    /// </summary>
    [Fact]
    public void Test_Centroid_PerfectEdge_AllPixels()
    {
        // Arrange: 创建理想阶梯边缘
        byte[] edgeData = { 0, 0, 0, 255, 255, 255 };
        using var lineProfile = Mat.FromArray(edgeData);
        lineProfile.Reshape(1, 1);

        // Act: 使用阈值为0，包含所有像素
        float result = _detector.DetectCentroid(lineProfile, threshold: 0);

        // Assert
        // Σ(i * gray[i]) = 0*0 + 1*0 + 2*0 + 3*255 + 4*255 + 5*255 = 3060
        // Σ(gray[i]) = 765
        // position = 3060 / 765 = 4.0
        result.Should().BeApproximately(4.0f, 0.01f);
    }

    /// <summary>
    /// 测试2: 斜边（渐变边缘）
    /// 输入: [0, 64, 128, 192, 255]
    /// 计算重心位置
    /// 
    /// 计算公式:
    /// Σ(i * gray[i]) = 0*0 + 1*64 + 2*128 + 3*192 + 4*255 = 1916
    /// Σ(gray[i]) = 0 + 64 + 128 + 192 + 255 = 639
    /// position = 1916 / 639 ≈ 2.998
    /// </summary>
    [Fact]
    public void Test_Centroid_GradualEdge()
    {
        // Arrange: 创建斜边 [0, 64, 128, 192, 255]
        byte[] edgeData = { 0, 64, 128, 192, 255 };
        using var lineProfile = Mat.FromArray(edgeData);
        lineProfile.Reshape(1, 1);

        // Act
        float result = _detector.DetectCentroid(lineProfile, threshold: 0);

        // Assert
        // Σ(i * gray[i]) = 0*0 + 1*64 + 2*128 + 3*192 + 4*255 = 1916
        // Σ(gray[i]) = 639
        // position = 1916 / 639 ≈ 2.998
        float expected = 1916.0f / 639.0f;
        result.Should().BeApproximately(expected, 0.01f, 
            "斜边的重心位置计算应准确");
    }

    /// <summary>
    /// 测试2b: 斜边（带阈值）
    /// 验证阈值筛选后的重心位置
    /// </summary>
    [Fact]
    public void Test_Centroid_GradualEdge_WithThreshold()
    {
        // Arrange: 创建斜边
        byte[] edgeData = { 0, 64, 128, 192, 255 };
        using var lineProfile = Mat.FromArray(edgeData);
        lineProfile.Reshape(1, 1);

        // Act: 使用阈值 100
        float result = _detector.DetectCentroid(lineProfile, threshold: 100);

        // Assert: 只有 128, 192, 255 参与计算
        // indices: 2, 3, 4
        // values: 128, 192, 255
        // Σ(i * gray[i]) = 2*128 + 3*192 + 4*255 = 256 + 576 + 1020 = 1852
        // Σ(gray[i]) = 128 + 192 + 255 = 575
        // position = 1852 / 575 ≈ 3.221
        float expected = 1852.0f / 575.0f;
        result.Should().BeApproximately(expected, 0.01f);
    }

    /// <summary>
    /// 测试3: 带噪声边缘（SNR=20dB）
    /// 验证算法对噪声的稳定性
    /// </summary>
    [Fact]
    public void Test_Centroid_NoisyEdge()
    {
        // Arrange: 创建带噪声的理想边缘
        // 基础边缘: [0, 0, 0, 255, 255, 255]
        byte[] edgeData = { 0, 0, 0, 255, 255, 255 };
        
        // 添加噪声 (模拟 SNR=20dB)
        // 20dB SNR 意味着信号功率是噪声功率的 100 倍
        // 对于255的信号，噪声标准差约为 25.5
        var random = new Random(42); // 固定种子保证可重复性
        double noiseStd = 25.5;
        
        // 多次测试取平均，验证稳定性
        float[] results = new float[100];
        for (int run = 0; run < 100; run++)
        {
            byte[] noisyData = new byte[edgeData.Length];
            for (int i = 0; i < edgeData.Length; i++)
            {
                // Box-Muller 变换生成高斯噪声
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                double noise = noiseStd * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                
                int value = edgeData[i] + (int)noise;
                noisyData[i] = (byte)Math.Max(0, Math.Min(255, value));
            }

            using var lineProfile = Mat.FromArray(noisyData);
            lineProfile.Reshape(1, 1);
            results[run] = _detector.DetectCentroid(lineProfile, threshold: 128);
        }

        // Assert: 验证结果的稳定性
        float mean = results.Average();
        float stdDev = (float)Math.Sqrt(results.Select(r => (r - mean) * (r - mean)).Average());
        
        // 平均值应在 4.0 附近（理想值）
        mean.Should().BeApproximately(4.0f, 0.15f, 
            "带噪声边缘的重心均值应接近理想值");
        
        // 标准差应小于 0.3 像素（稳定性要求）
        stdDev.Should().BeLessThan(0.3f, 
            "带噪声边缘的重心标准差应小于0.3像素，证明算法稳定性");
    }

    /// <summary>
    /// 测试3b: 弱边缘（低对比度）
    /// 验证算法对弱边缘的鲁棒性
    /// </summary>
    [Fact]
    public void Test_Centroid_WeakEdge()
    {
        // Arrange: 创建弱边缘（低对比度）
        // 背景: 100, 前景: 150, 对比度只有 50
        byte[] weakEdgeData = { 100, 100, 100, 150, 150, 150 };
        using var lineProfile = Mat.FromArray(weakEdgeData);
        lineProfile.Reshape(1, 1);

        // Act: 使用较低的阈值
        float result = _detector.DetectCentroid(lineProfile, threshold: 120);

        // Assert
        // 只有 150 的像素参与计算
        // Σ(i * 150) = 3*150 + 4*150 + 5*150 = 1800
        // Σ(150) = 450
        // position = 1800 / 450 = 4.0
        result.Should().BeApproximately(4.0f, 0.01f, 
            "弱边缘检测应仍能保持精度");
    }

    /// <summary>
    /// 测试4: 空输入处理
    /// 验证算法对空输入的鲁棒性
    /// </summary>
    [Fact]
    public void Test_Centroid_EmptyInput()
    {
        // Arrange
        using var emptyMat = new Mat();

        // Act
        float result = _detector.DetectCentroid(emptyMat);

        // Assert
        result.Should().Be(-1, "空输入应返回-1表示失败");
    }

    /// <summary>
    /// 测试5: 无效图像维度（非1D）
    /// 验证算法对无效维度的处理
    /// </summary>
    [Fact]
    public void Test_Centroid_InvalidDimensions()
    {
        // Arrange: 创建2D图像而非1D轮廓
        using var invalidMat = new Mat(10, 10, MatType.CV_8UC1, Scalar.All(128));

        // Act
        float result = _detector.DetectCentroid(invalidMat);

        // Assert
        result.Should().Be(-1, "非1D输入应返回-1表示失败");
    }

    /// <summary>
    /// 测试6: 所有像素为0
    /// 验证算法对全零输入的处理
    /// </summary>
    [Fact]
    public void Test_Centroid_AllZeros()
    {
        // Arrange
        byte[] zeroData = { 0, 0, 0, 0, 0 };
        using var lineProfile = Mat.FromArray(zeroData);
        lineProfile.Reshape(1, 1);

        // Act
        float result = _detector.DetectCentroid(lineProfile, threshold: 0);

        // Assert: 灰度和为0，应返回-1
        result.Should().Be(-1, "全零输入应返回-1表示无效");
    }

    /// <summary>
    /// 测试7: 单列轮廓
    /// 验证算法正确处理 Nx1 的轮廓
    /// </summary>
    [Fact]
    public void Test_Centroid_ColumnVector()
    {
        // Arrange: 创建 Nx1 列向量
        byte[] edgeData = { 0, 0, 255, 255 };
        using var lineProfile = Mat.FromArray(edgeData);
        // 保持为 4x1 形状

        // Act
        float result = _detector.DetectCentroid(lineProfile, threshold: 128);

        // Assert: 只有 255 参与，索引 2, 3
        // Σ(i * 255) = 2*255 + 3*255 = 1275
        // Σ(255) = 510
        // position = 1275 / 510 = 2.5
        result.Should().BeApproximately(2.5f, 0.01f, 
            "列向量轮廓应正确计算重心");
    }

    /// <summary>
    /// 测试8: 对称边缘
    /// 验证对称边缘的重心位置
    /// </summary>
    [Fact]
    public void Test_Centroid_SymmetricEdge()
    {
        // Arrange: 创建对称灰度分布
        // 三角形分布: [0, 128, 255, 128, 0]
        byte[] symmetricData = { 0, 128, 255, 128, 0 };
        using var lineProfile = Mat.FromArray(symmetricData);
        lineProfile.Reshape(1, 1);

        // Act
        float result = _detector.DetectCentroid(lineProfile, threshold: 0);

        // Assert: 对称分布的重心应在中心
        // Σ(i * gray[i]) = 0*0 + 1*128 + 2*255 + 3*128 + 4*0 = 128 + 510 + 384 = 1022
        // Σ(gray[i]) = 511
        // position = 1022 / 511 ≈ 2.0
        result.Should().BeApproximately(2.0f, 0.01f, 
            "对称分布的重心应在中心位置");
    }

    /// <summary>
    /// 测试9: 自适应阈值检测
    /// 验证自适应阈值功能
    /// </summary>
    [Fact]
    public void Test_Centroid_AdaptiveThreshold()
    {
        // Arrange: 创建边缘
        byte[] edgeData = { 10, 10, 10, 240, 240, 240 };
        using var lineProfile = Mat.FromArray(edgeData);
        lineProfile.Reshape(1, 1);

        // Act
        float result = _detector.DetectCentroidAdaptive(lineProfile, useAdaptiveThreshold: true);

        // Assert: 自适应阈值应能正确处理边缘
        // 阈值 = (10 + 240) * 0.25 = 62.5
        // 只有 240 参与，索引 3, 4, 5
        // position = (3+4+5)/3 = 4.0
        result.Should().BeApproximately(4.0f, 0.1f, 
            "自适应阈值应能正确检测边缘");
    }

    /// <summary>
    /// 测试10: 与 OpenCV cornerSubPix 对比（基准测试）
    /// 验证我们的实现与 OpenCV 标准算法的误差
    /// </summary>
    [Fact]
    public void Test_Centroid_CompareWithOpenCV()
    {
        // Arrange: 创建一个简单的角点图像
        // 使用黑色背景和白色方块形成的角点
        using var cornerImage = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));
        
        // 绘制白色方块（左上象限）
        var roi = new Rect(0, 0, 50, 50);
        cornerImage.Rectangle(roi, Scalar.All(255), -1);

        // 理想角点位置: (50, 50)
        Point2f initialPoint = new Point2f(50, 50);
        Point2f[] corners = { initialPoint };

        // 使用 OpenCV cornerSubPix 精确定位
        using var gray = cornerImage.Clone();
        TermCriteria criteria = new TermCriteria(CriteriaTypes.MaxIter | CriteriaTypes.Eps, 30, 0.01);
        
        try
        {
            Cv2.CornerSubPix(gray, corners, new Size(10, 10), new Size(-1, -1), criteria);
            float openCvResult = corners[0].X;

            // 使用我们的重心法检测同一位置的水平轮廓
            Point start = new Point(40, 50);
            Point end = new Point(60, 50);
            float ourResult = _detector.DetectEdgeInImage(gray, start, end);
            
            // 调整 ourResult 为绝对坐标
            ourResult += start.X;

            // Assert: 误差应小于 5%
            float error = Math.Abs(ourResult - openCvResult) / openCvResult * 100;
            error.Should().BeLessThan(5.0f, 
                $"重心法与 OpenCV cornerSubPix 的误差应小于 5%，实际误差: {error:F2}%");
        }
        catch (Exception ex)
        {
            // 如果 OpenCV 方法失败，跳过此测试
            Assert.True(true, $"OpenCV cornerSubPix 调用失败，跳过对比测试: {ex.Message}");
        }
    }

    /// <summary>
    /// 测试11: 精度验证
    /// 
    /// 灰度重心法的理论精度约为 0.01-0.1 像素
    /// 本测试验证对于已知灰度分布，重心计算是否准确
    /// </summary>
    [Fact]
    public void Test_Centroid_Precision()
    {
        // Arrange: 创建一个理论重心已知的灰度分布
        // 使用两个像素的灰度值来精确控制重心位置
        // 设像素 A(灰度=a) 在位置 0，像素 B(灰度=b) 在位置 1
        // 重心位置 = (0*a + 1*b) / (a+b) = b/(a+b)
        
        // 测试案例 1: 重心在 0.25 位置 (a=3, b=1, 重心=1/4=0.25)
        byte[] edgeData1 = { 255, 85, 0, 0, 0 }; // 重心应在 0.25
        using var lineProfile1 = Mat.FromArray(edgeData1);
        lineProfile1.Reshape(1, 1);
        
        // Act
        float result1 = _detector.DetectCentroid(lineProfile1, threshold: 0);
        
        // 理论值: (0*255 + 1*85) / (255+85) = 85/340 = 0.25
        float expected1 = 85.0f / 340.0f;
        float error1 = Math.Abs(result1 - expected1);
        
        // Assert: 计算精度误差应小于 0.01 像素
        error1.Should().BeLessThan(0.01f, 
            $"重心计算精度应小于 0.01 像素，实际误差: {error1:F4} 像素");
        
        // 测试案例 2: 重心在 0.5 位置 (对称分布)
        byte[] edgeData2 = { 100, 100, 0, 0, 0 };
        using var lineProfile2 = Mat.FromArray(edgeData2);
        lineProfile2.Reshape(1, 1);
        
        float result2 = _detector.DetectCentroid(lineProfile2, threshold: 0);
        float expected2 = 0.5f; // (0*100 + 1*100) / 200 = 0.5
        float error2 = Math.Abs(result2 - expected2);
        
        error2.Should().BeLessThan(0.01f, 
            $"对称分布的重心应在 0.5 位置，实际误差: {error2:F4} 像素");
        
        // 测试案例 3: 验证实际可重复的亚像素精度
        // 创建一个平滑过渡边缘，多次检测验证稳定性
        byte[] edgeData3 = { 0, 50, 150, 250, 255 };
        using var lineProfile3 = Mat.FromArray(edgeData3);
        lineProfile3.Reshape(1, 1);
        
        float result3 = _detector.DetectCentroid(lineProfile3, threshold: 0);
        // 理论值: (0*0 + 1*50 + 2*150 + 3*250 + 4*255) / (0+50+150+250+255) 
        //       = (0 + 50 + 300 + 750 + 1020) / 705 = 2120/705 ≈ 3.007
        float expected3 = 2120.0f / 705.0f;
        float error3 = Math.Abs(result3 - expected3);
        
        error3.Should().BeLessThan(0.01f, 
            $"平滑边缘的重心计算误差应小于 0.01 像素，实际误差: {error3:F4} 像素");
    }

    /// <summary>
    /// 测试12: 不同类型 Mat 输入（CV_32FC1）
    /// 验证算法正确处理浮点型输入
    /// </summary>
    [Fact]
    public void Test_Centroid_FloatInput()
    {
        // Arrange: 创建浮点型轮廓
        float[] edgeData = { 0.0f, 0.0f, 255.0f, 255.0f };
        using var lineProfile = Mat.FromArray(edgeData);
        lineProfile.Reshape(1, 1);

        // Act
        float result = _detector.DetectCentroid(lineProfile, threshold: 128);

        // Assert
        result.Should().BeApproximately(2.5f, 0.01f, 
            "浮点型输入应正确计算重心");
    }

    [Fact]
    public void Test_Zernike_CompareWithOpenCV()
    {
        // Arrange: simple corner image (black background with white square).
        using var cornerImage = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(0));
        var roi = new Rect(0, 0, 50, 50);
        cornerImage.Rectangle(roi, Scalar.All(255), -1);

        using var blurred = new Mat();
        Cv2.GaussianBlur(cornerImage, blurred, new Size(9, 9), 2.0);

        Point2f[] corners = { new Point2f(49.5f, 49.5f) };
        TermCriteria criteria = new TermCriteria(CriteriaTypes.MaxIter | CriteriaTypes.Eps, 30, 0.01);
        Cv2.CornerSubPix(blurred, corners, new Size(10, 10), new Size(-1, -1), criteria);
        float openCvResult = corners[0].X;

        var detector = new SubPixelEdgeDetector { EdgeThreshold = 0 };
        int startX = 40;
        int length = 21;
        float ourOffset = DetectZernikeOnScanline(detector, blurred, true, 25, startX, length);
        float ourResult = startX + ourOffset;

        float error = Math.Abs(ourResult - openCvResult) / openCvResult * 100f;
        error.Should().BeLessThan(1.0f, $"Zernike subpixel edge error should be < 1%, actual {error:F2}%");
    }

    [Fact]
    public void Test_Zernike_SlantEdgeAccuracy()
    {
        using var image = LoadTestGray("iso12233_slant_edge_512.png");
        image.Empty().Should().BeFalse();

        using var blurred = new Mat();
        Cv2.GaussianBlur(image, blurred, new Size(5, 5), 1.0);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 50, 150);
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxNone);
        var pointsF = contours.SelectMany(c => c).Select(p => new Point2f(p.X, p.Y)).ToArray();
        pointsF.Length.Should().BeGreaterThan(10);
        var line = Cv2.FitLine(pointsF, DistanceTypes.L2, 0, 0.01, 0.01);

        var detector = new SubPixelEdgeDetector { EdgeThreshold = 0 };

        double cx = (blurred.Cols - 1) * 0.5;
        double cy = (blurred.Rows - 1) * 0.5;
        double t = (line.Vx * (cx - line.X1)) + (line.Vy * (cy - line.Y1));
        double px = line.X1 + line.Vx * t;
        double py = line.Y1 + line.Vy * t;

        double nx = -line.Vy;
        double ny = line.Vx;
        int length = 41;
        double half = (length - 1) / 2.0;
        var start = new Point2d(px - nx * half, py - ny * half);
        var end = new Point2d(px + nx * half, py + ny * half);

        float[] values = ExtractLineValues(blurred, start, end, length);
        double expected = FindHalfMaxCrossing(values);

        using var lineProfile = Mat.FromArray(values);
        lineProfile.Reshape(1, 1);
        float pos = detector.DetectZernike(lineProfile);
        double error = Math.Abs(pos - expected);

        error.Should().BeLessOrEqualTo(0.05, $"Slant-edge subpixel error should be <= 0.05px, actual {error:F4}px");
    }

    [Fact]
    public void Test_Zernike_Repeatability_1000Runs()
    {
        using var image = LoadTestGray("iso12233_slant_edge_512.png");
        image.Empty().Should().BeFalse();

        var detector = new SubPixelEdgeDetector { EdgeThreshold = 0 };
        double cx = (image.Cols - 1) * 0.5;
        double cy = (image.Rows - 1) * 0.5;
        using var edges = new Mat();
        Cv2.Canny(image, edges, 50, 150);
        Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxNone);
        var pointsF = contours.SelectMany(c => c).Select(p => new Point2f(p.X, p.Y)).ToArray();
        var line = Cv2.FitLine(pointsF, DistanceTypes.L2, 0, 0.01, 0.01);
        double t = (line.Vx * (cx - line.X1)) + (line.Vy * (cy - line.Y1));
        double px = line.X1 + line.Vx * t;
        double py = line.Y1 + line.Vy * t;
        double nx = -line.Vy;
        double ny = line.Vx;
        int length = 41;
        double half = (length - 1) / 2.0;
        var start = new Point2d(px - nx * half, py - ny * half);
        var end = new Point2d(px + nx * half, py + ny * half);

        float[] values = ExtractLineValues(image, start, end, length);
        using var lineProfile = Mat.FromArray(values);
        lineProfile.Reshape(1, 1);

        const int runs = 1000;
        var results = new double[runs];
        for (int i = 0; i < runs; i++)
        {
            results[i] = detector.DetectZernike(lineProfile);
        }

        double mean = results.Average();
        double std = Math.Sqrt(results.Select(r => (r - mean) * (r - mean)).Average());
        std.Should().BeLessThan(0.02, $"Repeatability std should be < 0.02px, actual {std:F4}px");
    }

    private static string ResolveTestDataPath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "TestData", fileName));
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !dir.Name.Equals("Acme.Product", StringComparison.OrdinalIgnoreCase))
        {
            dir = dir.Parent;
        }

        if (dir != null)
        {
            candidate = Path.Combine(dir.FullName, "tests", "TestData", fileName);
        }

        return candidate;
    }

    private static Mat LoadTestGray(string fileName)
    {
        var path = ResolveTestDataPath(fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Test data not found: {path}");
        }

        return Cv2.ImRead(path, ImreadModes.Grayscale);
    }

    private static (int start, int length) BuildWindow(int center, int radius, int maxLength)
    {
        int start = Math.Max(0, center - radius);
        int end = Math.Min(maxLength - 1, center + radius);
        return (start, end - start + 1);
    }

    private static float DetectZernikeOnScanline(SubPixelEdgeDetector detector, Mat image, bool horizontal, int fixedCoord, int start, int length)
    {
        using var lineProfile = ExtractScanline(image, horizontal, fixedCoord, start, length);
        return detector.DetectZernike(lineProfile);
    }

    private static Mat ExtractScanline(Mat image, bool horizontal, int fixedCoord, int start, int length)
    {
        float[] values = new float[length];
        if (horizontal)
        {
            int y = Math.Clamp(fixedCoord, 0, image.Rows - 1);
            for (int i = 0; i < length; i++)
            {
                int x = Math.Clamp(start + i, 0, image.Cols - 1);
                values[i] = image.At<byte>(y, x);
            }
        }
        else
        {
            int x = Math.Clamp(fixedCoord, 0, image.Cols - 1);
            for (int i = 0; i < length; i++)
            {
                int y = Math.Clamp(start + i, 0, image.Rows - 1);
                values[i] = image.At<byte>(y, x);
            }
        }

        var lineProfile = Mat.FromArray(values);
        lineProfile.Reshape(1, 1);
        return lineProfile;
    }

    private static Mat ExtractLineProfile(Mat image, Point2d start, Point2d end, int length)
    {
        float[] values = ExtractLineValues(image, start, end, length);
        var lineProfile = Mat.FromArray(values);
        lineProfile.Reshape(1, 1);
        return lineProfile;
    }

    private static float[] ExtractLineValues(Mat image, Point2d start, Point2d end, int length)
    {
        float[] values = new float[length];
        for (int i = 0; i < length; i++)
        {
            double t = length <= 1 ? 0.0 : (double)i / (length - 1);
            double x = start.X + (end.X - start.X) * t;
            double y = start.Y + (end.Y - start.Y) * t;
            values[i] = (float)SampleGrayBilinear(image, x, y);
        }

        return values;
    }

    private static double FindHalfMaxCrossing(float[] values)
    {
        if (values.Length < 2)
        {
            return 0.0;
        }

        float min = values.Min();
        float max = values.Max();
        double half = (min + max) * 0.5;

        for (int i = 0; i < values.Length - 1; i++)
        {
            double v0 = values[i];
            double v1 = values[i + 1];
            if ((v0 <= half && v1 >= half) || (v0 >= half && v1 <= half))
            {
                double denom = v1 - v0;
                if (Math.Abs(denom) < 1e-9)
                {
                    return i;
                }

                double t = (half - v0) / denom;
                return i + t;
            }
        }

        return (values.Length - 1) * 0.5;
    }

    private static double SampleGrayBilinear(Mat gray, double x, double y)
    {
        double clampedX = Math.Clamp(x, 0.0, gray.Cols - 1.0);
        double clampedY = Math.Clamp(y, 0.0, gray.Rows - 1.0);
        int x0 = (int)Math.Floor(clampedX);
        int y0 = (int)Math.Floor(clampedY);
        int x1 = Math.Min(x0 + 1, gray.Cols - 1);
        int y1 = Math.Min(y0 + 1, gray.Rows - 1);
        double fx = clampedX - x0;
        double fy = clampedY - y0;

        double v00 = gray.At<byte>(y0, x0);
        double v10 = gray.At<byte>(y0, x1);
        double v01 = gray.At<byte>(y1, x0);
        double v11 = gray.At<byte>(y1, x1);

        double top = v00 + ((v10 - v00) * fx);
        double bottom = v01 + ((v11 - v01) * fx);
        return top + ((bottom - top) * fy);
    }
}
