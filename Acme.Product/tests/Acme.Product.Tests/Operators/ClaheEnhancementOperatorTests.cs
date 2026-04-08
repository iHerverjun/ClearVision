using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class ClaheEnhancementOperatorTests
{
    private readonly ClaheEnhancementOperator _operator;

    public ClaheEnhancementOperatorTests()
    {
        _operator = new ClaheEnhancementOperator(Substitute.For<ILogger<ClaheEnhancementOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeClaheEnhancement()
    {
        _operator.OperatorType.Should().Be(OperatorType.ClaheEnhancement);
    }

    [Fact]
    public async Task ExecuteAsync_WithYChannelOverride_ShouldMatchManualYCrCbReference()
    {
        var op = new Operator("clahe", OperatorType.ClaheEnhancement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("ClipLimit", 2.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("TileWidth", 4, "int"));
        op.AddParameter(TestHelpers.CreateParameter("TileHeight", 4, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ColorSpace", "Lab", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Channel", "Y", "string"));

        using var image = CreateColorGradientImage();
        using var source = image.MatReadOnly.Clone();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["ResolvedColorSpace"].Should().Be("YCrCb");
        result.OutputData["ResolvedChannel"].Should().Be("Y");

        using var output = result.OutputData["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();
        using var expected = CreateManualYChannelReference(source, 2.0, 4, 4);
        using var diff = new Mat();
        Cv2.Absdiff(actual, expected, diff);
        using var grayDiff = new Mat();
        Cv2.CvtColor(diff, grayDiff, ColorConversionCodes.BGR2GRAY);
        Cv2.CountNonZero(grayDiff).Should().Be(0);
    }

    [Fact]
    public void ValidateParameters_WithInvalidChannel_ShouldReturnInvalid()
    {
        var op = new Operator("clahe", OperatorType.ClaheEnhancement, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Channel", "Hue", "string"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    private static ImageWrapper CreateColorGradientImage()
    {
        var mat = new Mat(48, 64, MatType.CV_8UC3);
        for (var y = 0; y < mat.Rows; y++)
        {
            for (var x = 0; x < mat.Cols; x++)
            {
                mat.Set(y, x, new Vec3b(
                    (byte)((x * 255) / mat.Cols),
                    (byte)((y * 255) / mat.Rows),
                    (byte)(((x + y) * 255) / (mat.Rows + mat.Cols))));
            }
        }

        return new ImageWrapper(mat);
    }

    private static Mat CreateManualYChannelReference(Mat src, double clipLimit, int tileWidth, int tileHeight)
    {
        using var ycrcb = new Mat();
        Cv2.CvtColor(src, ycrcb, ColorConversionCodes.BGR2YCrCb);
        Cv2.Split(ycrcb, out var channels);

        try
        {
            using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileWidth, tileHeight));
            using var enhanced = new Mat();
            clahe.Apply(channels[0], enhanced);
            channels[0].Dispose();
            channels[0] = enhanced.Clone();

            using var merged = new Mat();
            Cv2.Merge(channels, merged);

            var result = new Mat();
            Cv2.CvtColor(merged, result, ColorConversionCodes.YCrCb2BGR);
            return result;
        }
        finally
        {
            foreach (var mat in channels)
            {
                mat.Dispose();
            }
        }
    }
}
