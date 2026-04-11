using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Operators;

[Trait("Category", "Sprint5_Phase2")]
public class GapMeasurementOperatorTests
{
    [Fact]
    public void OperatorType_ShouldBeGapMeasurement()
    {
        var sut = CreateSut();
        Assert.Equal(OperatorType.GapMeasurement, sut.OperatorType);
    }

    [Fact]
    public async Task ExecuteAsync_WithPointList_ShouldReturnGapStatistics()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object> { { "Direction", "Horizontal" } });
        var points = new List<Position>
        {
            new(10, 20),
            new(20, 20),
            new(35, 20),
            new(50, 20)
        };

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { { "Points", points } });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);

        var gaps = Assert.IsType<List<double>>(result.OutputData!["Gaps"]);
        Assert.Equal(3, gaps.Count);
        Assert.Equal(10.0, gaps[0], 6);
        Assert.Equal(15.0, gaps[1], 6);
        Assert.Equal(15.0, gaps[2], 6);
        Assert.Equal(13.333333, Convert.ToDouble(result.OutputData["MeanGap"]), 3);
        Assert.Equal(3, Convert.ToInt32(result.OutputData["Count"]));
        Assert.Equal("OK", result.OutputData["StatusCode"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutImageOrPoints_ShouldReturnFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator();

        var result = await sut.ExecuteAsync(op, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("Image or Points", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void ValidateParameters_WithInvalidRange_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "MinGap", 20.0 },
            { "MaxGap", 10.0 }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateParameters_WithInvalidOutlierSigma_ShouldReturnInvalid()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "OutlierSigmaK", 0.1 }
        });

        var validation = sut.ValidateParameters(op);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_WithOutlierDominatedProjection_ShouldKeepRegularGapPeaks()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "ExpectedCount", 2 }
        });

        using var profileImage = new Mat(60, 200, MatType.CV_8UC1, Scalar.Black);

        // Regular weak peaks that should still be detected.
        Cv2.Line(profileImage, new Point(30, 0), new Point(30, 59), new Scalar(30), 1);
        Cv2.Line(profileImage, new Point(70, 0), new Point(70, 59), new Scalar(30), 1);
        Cv2.Line(profileImage, new Point(110, 0), new Point(110, 59), new Scalar(30), 1);

        // A wide bright outlier simulating highlight/scratch.
        Cv2.Rectangle(profileImage, new Rect(160, 0, 20, 60), new Scalar(255), -1);

        using var image = new ImageWrapper(profileImage.Clone());
        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { { "Image", image } });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputData);

        var gaps = Assert.IsType<List<double>>(result.OutputData!["Gaps"]);
        Assert.Equal(2, gaps.Count);
        Assert.InRange(gaps[0], 35.0, 45.0);
        Assert.InRange(gaps[1], 35.0, 45.0);
        Assert.True(result.OutputData.ContainsKey("P95Gap"));
        Assert.True(result.OutputData.ContainsKey("StdDev"));
        Assert.True(result.OutputData.ContainsKey("ValidSampleRate"));
    }

    [Fact]
    public async Task ExecuteAsync_WithLowContrastImage_ShouldReturnLowContrastFailure()
    {
        var sut = CreateSut();
        var op = CreateOperator(new Dictionary<string, object>
        {
            { "Direction", "Horizontal" },
            { "RobustMode", true }
        });

        using var lowContrast = new Mat(120, 200, MatType.CV_8UC1, new Scalar(120));
        using var image = new ImageWrapper(lowContrast.Clone());
        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { { "Image", image } });

        Assert.False(result.IsSuccess);
        Assert.Contains("LowContrast", result.ErrorMessage ?? string.Empty);
    }

    private static GapMeasurementOperator CreateSut()
    {
        return new GapMeasurementOperator(Substitute.For<ILogger<GapMeasurementOperator>>());
    }

    private static Operator CreateOperator(Dictionary<string, object>? parameters = null)
    {
        var op = new Operator("GapMeasurement", OperatorType.GapMeasurement, 0, 0);

        if (parameters != null)
        {
            foreach (var (name, value) in parameters)
            {
                op.AddParameter(new Parameter(Guid.NewGuid(), name, name, string.Empty, "string", value));
            }
        }

        return op;
    }
}
