// SubPixelEdgeDetector.cs
// 灰度重心法亚像素边缘检测器
// 基于灰度重心公式: position = Σ(i * gray[i]) / Σ(gray[i])

using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// 灰度重心法亚像素边缘检测器
/// 
/// 核心思想:
/// 利用灰度分布的重心位置来确定亚像素精度的边缘位置。
/// 对于理想的阶梯边缘，重心位置对应于边缘的精确位置。
/// 
/// 可达到 0.01-0.1 像素的定位精度
/// </summary>
public class SubPixelEdgeDetector
{
    /// <summary>
    /// 边缘强度阈值，用于确定参与计算的像素范围
    /// </summary>
    public byte EdgeThreshold { get; set; } = 20;

    /// <summary>
    /// 最小有效灰度和，低于此值认为检测失败
    /// </summary>
    public float MinValidSum { get; set; } = 1e-6f;

    /// <summary>
    /// 灰度重心法亚像素边缘定位
    /// 
    /// 算法步骤:
    /// 1. 对灰度轮廓进行阈值筛选，提取有效边缘区域
    /// 2. 计算灰度重心位置: position = Σ(i * gray[i]) / Σ(gray[i])
    /// 3. 返回亚像素精度的边缘位置
    /// </summary>
    /// <param name="lineProfile">1xN灰度轮廓线 (CV_8UC1 或 CV_32FC1)</param>
    /// <param name="threshold">边缘阈值，用于确定有效像素范围。若为0则使用 EdgeThreshold 属性值</param>
    /// <returns>亚像素位置（相对于轮廓起始位置的偏移），失败返回-1</returns>
    public float DetectCentroid(Mat lineProfile, byte threshold = 0)
    {
        if (lineProfile == null || lineProfile.Empty())
            return -1;

        // 确保是单行或单列轮廓
        if (lineProfile.Rows != 1 && lineProfile.Cols != 1)
            return -1;

        byte effectiveThreshold = threshold > 0 ? threshold : EdgeThreshold;

        // 统一处理为行向量
        int length = lineProfile.Rows == 1 ? lineProfile.Cols : lineProfile.Rows;
        bool isRow = lineProfile.Rows == 1;

        // 提取灰度值到数组
        float[] grayValues = new float[length];
        ExtractGrayValues(lineProfile, grayValues, isRow);

        // 计算重心
        return CalculateCentroid(grayValues, effectiveThreshold);
    }

    /// <summary>
    /// 提取灰度值到数组
    /// </summary>
    private void ExtractGrayValues(Mat lineProfile, float[] grayValues, bool isRow)
    {
        int length = grayValues.Length;

        unsafe
        {
            if (lineProfile.Type() == MatType.CV_8UC1)
            {
                byte* ptr = (byte*)lineProfile.DataPointer;
                int step = (int)lineProfile.Step();

                for (int i = 0; i < length; i++)
                {
                    grayValues[i] = isRow ? ptr[i] : ptr[i * step];
                }
            }
            else if (lineProfile.Type() == MatType.CV_32FC1)
            {
                float* ptr = (float*)lineProfile.DataPointer;
                int step = (int)lineProfile.Step() / sizeof(float);

                for (int i = 0; i < length; i++)
                {
                    grayValues[i] = isRow ? ptr[i] : ptr[i * step];
                }
            }
            else if (lineProfile.Type() == MatType.CV_64FC1)
            {
                double* ptr = (double*)lineProfile.DataPointer;
                int step = (int)lineProfile.Step() / sizeof(double);

                for (int i = 0; i < length; i++)
                {
                    grayValues[i] = (float)(isRow ? ptr[i] : ptr[i * step]);
                }
            }
            else
            {
                // 不支持的类型，返回空值
                Array.Clear(grayValues, 0, length);
            }
        }
    }

    /// <summary>
    /// 计算灰度重心位置
    /// </summary>
    /// <param name="grayValues">灰度值数组</param>
    /// <param name="threshold">阈值，只计算大于等于阈值的像素</param>
    /// <returns>亚像素位置</returns>
    private float CalculateCentroid(float[] grayValues, byte threshold)
    {
        if (grayValues == null || grayValues.Length == 0)
            return -1;

        int length = grayValues.Length;
        double weightedSum = 0;
        double graySum = 0;

        // 计算重心: position = Σ(i * gray[i]) / Σ(gray[i])
        for (int i = 0; i < length; i++)
        {
            float gray = grayValues[i];
            
            // 只考虑大于等于阈值的像素
            if (gray >= threshold)
            {
                weightedSum += i * gray;
                graySum += gray;
            }
        }

        // 检查有效性
        if (graySum < MinValidSum)
            return -1;

        return (float)(weightedSum / graySum);
    }

    /// <summary>
    /// 检测亚像素边缘位置（使用自动阈值）
    /// 
    /// 自动根据灰度分布计算合适的阈值
    /// </summary>
    /// <param name="lineProfile">1xN灰度轮廓线</param>
    /// <param name="useAdaptiveThreshold">是否使用自适应阈值</param>
    /// <returns>亚像素位置，失败返回-1</returns>
    public float DetectCentroidAdaptive(Mat lineProfile, bool useAdaptiveThreshold = true)
    {
        if (lineProfile == null || lineProfile.Empty())
            return -1;

        int length = lineProfile.Rows == 1 ? lineProfile.Cols : lineProfile.Rows;
        float[] grayValues = new float[length];
        ExtractGrayValues(lineProfile, grayValues, lineProfile.Rows == 1);

        if (!useAdaptiveThreshold)
            return CalculateCentroid(grayValues, EdgeThreshold);

        // 计算自适应阈值: (max + min) / 2 * 0.5
        float maxGray = grayValues.Max();
        float minGray = grayValues.Min();
        byte adaptiveThreshold = (byte)((maxGray + minGray) * 0.25f);

        // 确保阈值不会过高或过低
        adaptiveThreshold = Math.Max((byte)10, Math.Min((byte)200, adaptiveThreshold));

        return CalculateCentroid(grayValues, adaptiveThreshold);
    }

    /// <summary>
    /// Zernike-style moment based subpixel edge localization.
    /// Uses the first-order moment of the edge response (gradient magnitude) to estimate position.
    /// </summary>
    /// <param name="roi">1xN line profile or a small 2D ROI that crosses the edge.</param>
    /// <param name="maskSize">Optional ROI window size (odd). If <=0, uses the full ROI.</param>
    /// <returns>Subpixel position within the ROI (x for 2D, index for 1D). Returns -1 on failure.</returns>
    public float DetectZernike(Mat roi, int maskSize = 5)
    {
        if (roi == null || roi.Empty())
            return -1;

        if (roi.Rows == 1 || roi.Cols == 1)
        {
            return DetectZernikeOnLine(roi);
        }

        return DetectZernikeOnPatch(roi, maskSize);
    }

    private float DetectZernikeOnLine(Mat lineProfile)
    {
        int length = lineProfile.Rows == 1 ? lineProfile.Cols : lineProfile.Rows;
        if (length < 2)
            return -1;

        float[] values = new float[length];
        ExtractGrayValues(lineProfile, values, lineProfile.Rows == 1);

        int gradLength = length - 1;
        double center = (gradLength - 1) / 2.0;
        double r = Math.Max(center, 1.0);
        double z00 = 0.0;
        double z11 = 0.0;
        double threshold = EdgeThreshold;

        for (int i = 0; i < gradLength; i++)
        {
            double grad = Math.Abs(values[i + 1] - values[i]);
            if (grad < threshold)
            {
                continue;
            }

            z00 += grad;
            z11 += grad * ((i - center) / r);
        }

        if (z00 < MinValidSum)
            return -1;

        // l = Z11 / Z00 * 2.0, then convert to pixel offset.
        double l = (z11 / z00) * 2.0;
        double offset = l * (r / 2.0);
        double position = center + offset + 0.5;

        return (float)Math.Clamp(position, 0.0, length - 1.0);
    }

    private float DetectZernikeOnPatch(Mat roi, int maskSize)
    {
        int minDim = Math.Min(roi.Rows, roi.Cols);
        if (minDim < 3)
            return -1;

        int size = maskSize <= 0 ? minDim : Math.Min(maskSize, minDim);
        if (size % 2 == 0)
            size -= 1;
        if (size < 3)
            size = Math.Min(minDim, 3);

        int offsetX = (roi.Cols - size) / 2;
        int offsetY = (roi.Rows - size) / 2;
        offsetX = Math.Clamp(offsetX, 0, Math.Max(roi.Cols - size, 0));
        offsetY = Math.Clamp(offsetY, 0, Math.Max(roi.Rows - size, 0));

        using var patch = new Mat(roi, new Rect(offsetX, offsetY, size, size));
        using var patchFloat = new Mat();
        patch.ConvertTo(patchFloat, MatType.CV_32FC1);

        using var gradX = new Mat();
        Cv2.Sobel(patchFloat, gradX, MatType.CV_32FC1, 1, 0, 3);

        int center = size / 2;
        double r = Math.Max(center, 1.0);
        double z00 = 0.0;
        double z11 = 0.0;
        double threshold = EdgeThreshold;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double grad = Math.Abs(gradX.At<float>(y, x));
                if (grad < threshold)
                {
                    continue;
                }

                z00 += grad;
                z11 += grad * ((x - center) / r);
            }
        }

        if (z00 < MinValidSum)
            return -1;

        double l = (z11 / z00) * 2.0;
        double offset = l * (r / 2.0);
        double position = offsetX + center + offset;

        return (float)Math.Clamp(position, 0.0, roi.Cols - 1.0);
    }

    /// <summary>
    /// 从图像中提取轮廓线并检测亚像素边缘
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <param name="start">起始点</param>
    /// <param name="end">结束点</param>
    /// <returns>亚像素位置（相对于起始点的距离）</returns>
    public float DetectEdgeInImage(Mat image, Point start, Point end)
    {
        if (image == null || image.Empty())
            return -1;

        // 转换为灰度图
        using var gray = new Mat();
        if (image.Channels() > 1)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else
            image.CopyTo(gray);

        // 获取轮廓线
        using var lineProfile = ExtractLineProfile(gray, start, end);
        if (lineProfile == null || lineProfile.Empty())
            return -1;

        return DetectCentroid(lineProfile);
    }

    /// <summary>
    /// 从图像中提取线段上的灰度轮廓
    /// </summary>
    private Mat ExtractLineProfile(Mat image, Point start, Point end)
    {
        // 计算线段长度
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        int length = (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy));

        if (length < 2)
            return new Mat();

        // 创建输出图像
        Mat lineProfile = new Mat(1, length, MatType.CV_8UC1);

        unsafe
        {
            byte* imgPtr = (byte*)image.DataPointer;
            byte* linePtr = (byte*)lineProfile.DataPointer;
            int step = (int)image.Step();
            int width = image.Cols;
            int height = image.Rows;

            for (int i = 0; i < length; i++)
            {
                double t = (double)i / (length - 1);
                int x = (int)(start.X + dx * t);
                int y = (int)(start.Y + dy * t);

                // 边界检查
                x = Math.Max(0, Math.Min(width - 1, x));
                y = Math.Max(0, Math.Min(height - 1, y));

                linePtr[i] = imgPtr[y * step + x];
            }
        }

        return lineProfile;
    }
}
