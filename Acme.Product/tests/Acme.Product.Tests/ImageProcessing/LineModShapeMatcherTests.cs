// LineModShapeMatcherTests.cs
// LINEMOD 形状匹配算法单元测试
// 作者：蘅芜君

using Acme.Product.Infrastructure.ImageProcessing;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.ImageProcessing;

/// <summary>
/// LINEMOD 形状匹配算法单元测试
/// </summary>
public class LineModShapeMatcherTests : IDisposable
{
    private readonly LineModShapeMatcher _matcher;

    public LineModShapeMatcherTests()
    {
        _matcher = new LineModShapeMatcher
        {
            WeakThreshold = 30.0f,
            StrongThreshold = 60.0f,
            NumFeatures = 150,
            SpreadT = 4,
            PyramidLevels = 3
        };
    }

    public void Dispose()
    {
        _matcher.Dispose();
    }

    #region 训练测试

    [Fact]
    public void Train_WithValidImage_ShouldExtractFeatures()
    {
        // 创建测试图像 (带有一些边缘特征)
        using var templateImage = CreateTestTemplateImage(200, 200);

        // 训练
        var templates = _matcher.Train(templateImage);

        // 验证
        Assert.NotNull(templates);
        Assert.True(templates.Count > 0, "应该至少有一个金字塔层");
        Assert.True(templates[0].Features.Count > 0, "第一层应该有特征点");
        Assert.True(templates[0].Features.Count <= 150, "特征点数量不应超过设置值");
    }

    [Fact]
    public void Train_WithEmptyImage_ShouldThrowException()
    {
        using var emptyImage = new Mat();

        Assert.Throws<ArgumentException>(() => _matcher.Train(emptyImage));
    }

    [Fact]
    public void Train_WithMultiplePyramidLevels_ShouldCreateCorrectHierarchy()
    {
        using var templateImage = CreateTestTemplateImage(400, 400);
        _matcher.PyramidLevels = 3;

        var templates = _matcher.Train(templateImage);

        Assert.Equal(3, templates.Count);

        // 验证金字塔层级尺寸递减
        for (int i = 1; i < templates.Count; i++)
        {
            Assert.True(templates[i].Width <= templates[i - 1].Width / 2 + 1);
            Assert.True(templates[i].Height <= templates[i - 1].Height / 2 + 1);
        }
    }

    #endregion

    #region 匹配测试

    [Fact]
    public void Match_WithSameImage_ShouldFindHighScoreMatch()
    {
        // 创建模板
        using var templateImage = CreateTestTemplateImage(200, 200);
        _matcher.Train(templateImage);

        // 在同一图像上匹配
        var matches = _matcher.Match(templateImage, 0.7f, 5);

        // 验证
        Assert.NotNull(matches);
        Assert.True(matches.Count > 0, "应该找到匹配");
        Assert.True(matches[0].Score > 70, "匹配分数应该很高");
    }

    [Fact]
    public void Match_WithTranslatedImage_ShouldFindMatch()
    {
        // 创建模板
        using var templateImage = CreateTestTemplateImage(200, 200);
        _matcher.Train(templateImage);

        // 创建平移后的场景
        using var sceneImage = new Mat(400, 400, MatType.CV_8UC3, Scalar.All(255));
        var roi = new Rect(100, 100, 200, 200);
        using var templateGray = new Mat();
        Cv2.CvtColor(templateImage, templateGray, ColorConversionCodes.BGR2GRAY);
        using var sceneGray = new Mat(sceneImage.Size(), MatType.CV_8UC1, Scalar.All(255));
        templateGray.CopyTo(new Mat(sceneGray, roi));
        Cv2.CvtColor(sceneGray, sceneImage, ColorConversionCodes.GRAY2BGR);

        // 匹配
        var matches = _matcher.Match(sceneImage, 0.6f, 5);

        // 验证
        Assert.NotNull(matches);
        Assert.True(matches.Count > 0, "应该找到平移后的匹配");
    }

    [Fact]
    public void Match_WithoutTraining_ShouldThrowException()
    {
        using var sceneImage = CreateTestTemplateImage(200, 200);

        Assert.Throws<InvalidOperationException>(() => _matcher.Match(sceneImage));
    }

    [Fact]
    public void Match_WithThreshold_ShouldFilterResults()
    {
        // 创建模板
        using var templateImage = CreateTestTemplateImage(200, 200);
        _matcher.Train(templateImage);

        // 使用不同阈值匹配
        var highThresholdMatches = _matcher.Match(templateImage, 0.9f, 10);
        var lowThresholdMatches = _matcher.Match(templateImage, 0.5f, 10);

        // 高阈值应该返回较少结果
        Assert.True(highThresholdMatches.Count <= lowThresholdMatches.Count);
    }

    #endregion

    #region 参数测试

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void PyramidLevels_SetValidValue_ShouldWork(int levels)
    {
        _matcher.PyramidLevels = levels;
        Assert.Equal(levels, _matcher.PyramidLevels);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(150)]
    [InlineData(500)]
    public void NumFeatures_SetValidValue_ShouldWork(int numFeatures)
    {
        _matcher.NumFeatures = numFeatures;
        Assert.Equal(numFeatures, _matcher.NumFeatures);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void SpreadT_SetValidValue_ShouldWork(int spreadT)
    {
        _matcher.SpreadT = spreadT;
        Assert.Equal(spreadT, _matcher.SpreadT);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 创建测试模板图像 - 带有清晰的几何特征
    /// </summary>
    private static Mat CreateTestTemplateImage(int width, int height)
    {
        var image = new Mat(height, width, MatType.CV_8UC3, Scalar.All(255));

        // 绘制一个黑色的矩形框作为测试特征
        int margin = 40;
        Cv2.Rectangle(image,
            new Point(margin, margin),
            new Point(width - margin, height - margin),
            new Scalar(0, 0, 0), 3);

        // 绘制内部十字
        Cv2.Line(image,
            new Point(width / 2, margin),
            new Point(width / 2, height - margin),
            new Scalar(0, 0, 0), 2);
        Cv2.Line(image,
            new Point(margin, height / 2),
            new Point(width - margin, height / 2),
            new Scalar(0, 0, 0), 2);

        // 绘制中心圆
        Cv2.Circle(image, new Point(width / 2, height / 2), 30, new Scalar(0, 0, 0), 2);

        return image;
    }

    #endregion
}
