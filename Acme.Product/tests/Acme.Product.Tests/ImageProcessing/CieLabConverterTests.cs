using Acme.Product.Infrastructure.ImageProcessing;
using FluentAssertions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Infrastructure.ImageProcessing.Tests;

public sealed class CieLabConverterTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(255, 0, 0)]
    [InlineData(0, 255, 0)]
    [InlineData(0, 0, 255)]
    [InlineData(30, 30, 200)]
    public void RgbToLab_ShouldApproximatelyMatchOpenCv(byte r, byte g, byte b)
    {
        var lab = CieLabConverter.RgbToLab(r, g, b);

        using var bgr = new Mat(1, 1, MatType.CV_8UC3, new Scalar(b, g, r));
        using var cvLab = new Mat();
        Cv2.CvtColor(bgr, cvLab, ColorConversionCodes.BGR2Lab);
        var v = cvLab.At<Vec3b>(0, 0);

        // OpenCV Lab scaling:
        // L is 0..255 for 0..100, a and b are 0..255 with 128 as zero.
        var cvL = v.Item0 * (100.0 / 255.0);
        var cvA = (double)v.Item1 - 128.0;
        var cvB = (double)v.Item2 - 128.0;

        lab.L.Should().BeApproximately(cvL, 1.0);
        lab.A.Should().BeApproximately(cvA, 2.0);
        lab.B.Should().BeApproximately(cvB, 2.0);
    }
}

