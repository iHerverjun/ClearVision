using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class HistogramEqualizationOperatorTests
{
    private readonly HistogramEqualizationOperator _operator;

    public HistogramEqualizationOperatorTests()
    {
        _operator = new HistogramEqualizationOperator(Substitute.For<ILogger<HistogramEqualizationOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeHistogramEqualization()
    {
        _operator.OperatorType.Should().Be(OperatorType.HistogramEqualization);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);

        var result = await _operator.ExecuteAsync(op, null);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidImage_ShouldReturnSuccess()
    {
        var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);
        using var image = TestHelpers.CreateTestImage();

        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Image");
    }

    [Fact]
    public void ValidateParameters_Default_ShouldBeValid()
    {
        var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Metadata_ShouldExpose_ReconciledHistogramParameters()
    {
        var metadata = new OperatorFactory().GetMetadata(OperatorType.HistogramEqualization)!;

        metadata.Parameters.Select(p => p.Name).Should().Contain(new[] { "TileGridSize", "ApplyToEachChannel" });
    }

    [Fact]
    public async Task ExecuteAsync_WithTileGridSizeAndApplyToEachChannel_ShouldMatchManualClaheReference()
    {
        var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Method", "CLAHE", "string"));
        op.AddParameter(TestHelpers.CreateParameter("ClipLimit", 2.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("TileGridSize", 4, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ApplyToEachChannel", true, "bool"));

        using var image = CreateColorGradientImage();
        using var source = image.MatReadOnly.Clone();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["TileGridSize"].Should().Be(4);
        result.OutputData["ApplyToEachChannel"].Should().Be(true);

        using var output = result.OutputData["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();
        using var expected = CreateManualPerChannelClaheReference(source, 2.0, 4);
        using var diff = new Mat();
        Cv2.Absdiff(actual, expected, diff);
        using var grayDiff = new Mat();
        Cv2.CvtColor(diff, grayDiff, ColorConversionCodes.BGR2GRAY);
        Cv2.CountNonZero(grayDiff).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithLegacyTileSizeAndSeededTileGridSize_ShouldPreferLegacyValue()
    {
        var op = new OperatorFactory().CreateOperator(OperatorType.HistogramEqualization, "hist", 0, 0);
        op.UpdateParameter("Method", "CLAHE");
        op.UpdateParameter("ClipLimit", 2.0);
        op.AddParameter(TestHelpers.CreateParameter("TileSize", 4, "int"));

        using var image = CreateColorGradientImage();
        using var source = image.MatReadOnly.Clone();
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["TileGridSize"].Should().Be(4);

        using var output = result.OutputData["Image"].Should().BeOfType<ImageWrapper>().Subject;
        using var actual = output.GetMat();
        using var expected = CreateManualLabClaheReference(source, 2.0, 4);
        using var diff = new Mat();
        Cv2.Absdiff(actual, expected, diff);
        using var grayDiff = new Mat();
        Cv2.CvtColor(diff, grayDiff, ColorConversionCodes.BGR2GRAY);
        Cv2.CountNonZero(grayDiff).Should().Be(0);
    }

    [Fact]
    public void ValidateParameters_WithOutOfRangeTileGridSize_ShouldReturnInvalid()
    {
        var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("TileGridSize", 0, "int"));

        _operator.ValidateParameters(op).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithLegacyTileSizeOnly_ShouldRemainValid()
    {
        var op = new Operator("hist", OperatorType.HistogramEqualization, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("TileSize", 6, "int"));

        _operator.ValidateParameters(op).IsValid.Should().BeTrue();
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
                    (byte)(((x * 2 + y) * 255) / (mat.Rows + mat.Cols * 2))));
            }
        }

        return new ImageWrapper(mat);
    }

    private static Mat CreateManualPerChannelClaheReference(Mat src, double clipLimit, int tileGridSize)
    {
        using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize));
        Cv2.Split(src, out var channels);
        var processed = new Mat[channels.Length];

        try
        {
            for (var i = 0; i < channels.Length; i++)
            {
                processed[i] = new Mat();
                clahe.Apply(channels[i], processed[i]);
            }

            var result = new Mat();
            Cv2.Merge(processed, result);
            return result;
        }
        finally
        {
            foreach (var mat in channels)
            {
                mat.Dispose();
            }

            foreach (var mat in processed)
            {
                mat?.Dispose();
            }
        }
    }

    private static Mat CreateManualLabClaheReference(Mat src, double clipLimit, int tileGridSize)
    {
        using var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize));
        using var lab = new Mat();
        Cv2.CvtColor(src, lab, ColorConversionCodes.BGR2Lab);
        Cv2.Split(lab, out var channels);

        try
        {
            using var enhanced = new Mat();
            clahe.Apply(channels[0], enhanced);
            channels[0].Dispose();
            channels[0] = enhanced.Clone();

            using var merged = new Mat();
            Cv2.Merge(channels, merged);

            var result = new Mat();
            Cv2.CvtColor(merged, result, ColorConversionCodes.Lab2BGR);
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
