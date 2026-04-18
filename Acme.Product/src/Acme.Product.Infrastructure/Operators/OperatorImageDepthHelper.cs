using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

internal static class OperatorImageDepthHelper
{
    public static Mat EnsureSingleChannelGray(Mat src)
    {
        if (src.Channels() == 1)
        {
            return src.Clone();
        }

        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    public static Mat ConvertSingleChannelToByte(Mat src, out double minValue, out double maxValue)
    {
        if (src.Channels() != 1)
        {
            throw new ArgumentException("Expected a single-channel image.", nameof(src));
        }

        Cv2.MinMaxLoc(src, out double minValueLocal, out double maxValueLocal);
        minValue = minValueLocal;
        maxValue = maxValueLocal;

        if (src.Depth() == MatType.CV_8U)
        {
            return src.Clone();
        }

        var converted = new Mat();
        if (!double.IsFinite(minValue) || !double.IsFinite(maxValue))
        {
            throw new InvalidOperationException("Image contains non-finite values and cannot be normalized to 8-bit.");
        }

        if (maxValue <= minValue)
        {
            src.ConvertTo(converted, MatType.CV_8UC1, 0.0, 0.0);
            return converted;
        }

        var scale = 255.0 / (maxValue - minValue);
        var shift = -minValue * scale;
        src.ConvertTo(converted, MatType.CV_8UC1, scale, shift);
        return converted;
    }

    public static Mat RestoreBinaryMaskToSourceDepth(Mat binaryMask8u, Mat sourceSingleChannel, double maxValue8Bit)
    {
        if (binaryMask8u.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Binary mask must be CV_8UC1.", nameof(binaryMask8u));
        }

        var targetType = GetMatchingSingleChannelType(sourceSingleChannel);
        var targetMaxValue = ResolveScaledMaxValue(sourceSingleChannel.Depth(), maxValue8Bit);

        if (targetType == MatType.CV_8UC1)
        {
            var restored8 = new Mat();
            binaryMask8u.ConvertTo(restored8, targetType, Math.Clamp(maxValue8Bit, 0.0, 255.0) / 255.0);
            return restored8;
        }

        var restored = new Mat();
        binaryMask8u.ConvertTo(restored, targetType, targetMaxValue / 255.0);
        return restored;
    }

    public static Mat RestoreByteImageToSourceDepth(Mat byteImage, Mat sourceSingleChannel)
    {
        if (byteImage.Type() != MatType.CV_8UC1)
        {
            throw new ArgumentException("Input must be a single-channel 8-bit image.", nameof(byteImage));
        }

        var targetType = GetMatchingSingleChannelType(sourceSingleChannel);
        if (targetType == MatType.CV_8UC1)
        {
            return byteImage.Clone();
        }

        var depthMaxValue = GetNominalMaxValue(sourceSingleChannel.Depth());
        var restored = new Mat();
        byteImage.ConvertTo(restored, targetType, depthMaxValue / 255.0);
        return restored;
    }

    public static double ResolveThresholdToNativeRange(Mat sourceSingleChannel, double byteDomainThreshold)
    {
        if (sourceSingleChannel.Channels() != 1)
        {
            throw new ArgumentException("Expected a single-channel image.", nameof(sourceSingleChannel));
        }

        if (sourceSingleChannel.Depth() == MatType.CV_8U)
        {
            return Math.Clamp(byteDomainThreshold, 0.0, 255.0);
        }

        Cv2.MinMaxLoc(sourceSingleChannel, out double minValue, out double maxValue);
        if (!double.IsFinite(minValue) || !double.IsFinite(maxValue))
        {
            throw new InvalidOperationException("Image contains non-finite values.");
        }

        if (maxValue <= minValue)
        {
            return minValue;
        }

        return minValue + (Math.Clamp(byteDomainThreshold, 0.0, 255.0) / 255.0 * (maxValue - minValue));
    }

    public static double GetNominalMaxValue(MatType depth)
    {
        if (depth == MatType.CV_8U)
        {
            return 255.0;
        }

        if (depth == MatType.CV_16U)
        {
            return ushort.MaxValue;
        }

        if (depth == MatType.CV_32F || depth == MatType.CV_64F)
        {
            return 1.0;
        }

        return 255.0;
    }

    private static double ResolveScaledMaxValue(MatType depth, double maxValue8Bit)
    {
        if (depth == MatType.CV_8U)
        {
            return Math.Clamp(maxValue8Bit, 0.0, 255.0);
        }

        if (depth == MatType.CV_16U)
        {
            return Math.Clamp(maxValue8Bit, 0.0, 255.0) / 255.0 * ushort.MaxValue;
        }

        if (depth == MatType.CV_32F || depth == MatType.CV_64F)
        {
            return Math.Clamp(maxValue8Bit, 0.0, 255.0) / 255.0;
        }

        return Math.Clamp(maxValue8Bit, 0.0, 255.0);
    }

    private static MatType GetMatchingSingleChannelType(Mat src)
    {
        return src.Depth() switch
        {
            MatType.CV_8U => MatType.CV_8UC1,
            MatType.CV_16U => MatType.CV_16UC1,
            MatType.CV_32F => MatType.CV_32FC1,
            MatType.CV_64F => MatType.CV_64FC1,
            _ => MatType.CV_8UC1
        };
    }
}
