using System.Reflection;
using Acme.Product.Infrastructure.Operators;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ImageWrapperMemoryBehaviorTests
{
    [Fact]
    public void ByteSource_AfterDecode_ShouldReleaseEncodedCache()
    {
        using var source = new Mat(32, 48, MatType.CV_8UC3, new Scalar(10, 20, 30));
        var jpegBytes = source.ToBytes(".jpg");

        using var wrapper = new ImageWrapper(jpegBytes);
        Assert.False(wrapper.IsDecoded);
        Assert.NotNull(GetInternalBytes(wrapper));

        var decoded = wrapper.GetMat();
        Assert.True(wrapper.IsDecoded);
        Assert.Equal(48, decoded.Width);
        Assert.Equal(32, decoded.Height);
        Assert.Null(GetInternalBytes(wrapper));

        var roundTripBytes = wrapper.GetBytes();
        Assert.NotEmpty(roundTripBytes);
        Assert.Null(GetInternalBytes(wrapper));
    }

    [Fact]
    public void MatSource_GetBytesRepeatedly_ShouldNotKeepByteCache()
    {
        using var source = new Mat(20, 20, MatType.CV_8UC3, new Scalar(100, 150, 200));
        using var wrapper = new ImageWrapper(source.Clone());

        var bytes1 = wrapper.GetBytes();
        var bytes2 = wrapper.GetBytes();

        Assert.NotEmpty(bytes1);
        Assert.NotEmpty(bytes2);
        Assert.False(ReferenceEquals(bytes1, bytes2));
        Assert.Null(GetInternalBytes(wrapper));

        var decoded = wrapper.GetMat();
        Assert.Equal(20, decoded.Width);
        Assert.Equal(20, decoded.Height);
    }

    [Fact]
    public void LongLived_RepeatedRoundTrip_ShouldStayFunctional_AndAvoidDualCache()
    {
        using var source = new Mat(40, 60, MatType.CV_8UC3, new Scalar(1, 2, 3));
        var pngBytes = source.ToBytes(".png");

        using var wrapper = new ImageWrapper(pngBytes);
        for (int i = 0; i < 30; i++)
        {
            var mat = wrapper.GetMat();
            Assert.Equal(60, mat.Width);
            Assert.Equal(40, mat.Height);

            var bytes = wrapper.GetBytes();
            using var reDecoded = Cv2.ImDecode(bytes, ImreadModes.Color);
            Assert.False(reDecoded.Empty());
            Assert.Equal(60, reDecoded.Width);
            Assert.Equal(40, reDecoded.Height);

            Assert.True(wrapper.IsDecoded);
            Assert.Null(GetInternalBytes(wrapper));
        }
    }

    [Fact]
    public void GetWritableMat_FromBytes_SingleHolder_ShouldSupportInPlaceWrite()
    {
        using var source = new Mat(16, 16, MatType.CV_8UC3, new Scalar(0, 0, 0));
        var bytes = source.ToBytes(".png");
        using var wrapper = new ImageWrapper(bytes);

        using var writable = wrapper.GetWritableMat();
        writable.Set(3, 4, new Vec3b(9, 8, 7));

        var mat = wrapper.GetMat();
        var pixel = mat.At<Vec3b>(3, 4);
        Assert.Equal((byte)9, pixel.Item0);
        Assert.Equal((byte)8, pixel.Item1);
        Assert.Equal((byte)7, pixel.Item2);
    }

    private static byte[]? GetInternalBytes(ImageWrapper wrapper)
    {
        var field = typeof(ImageWrapper).GetField("_bytes", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (byte[]?)field!.GetValue(wrapper);
    }
}
