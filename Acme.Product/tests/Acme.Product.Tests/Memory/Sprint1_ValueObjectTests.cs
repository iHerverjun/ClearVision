// Sprint1_ValueObjectTests.cs
// Sprint 1 Task 1.2 端口类型扩展单元测试
// 测试新的值对象类型：DetectionResult, DetectionList, CircleData, LineData
// 作者：蘅芜君

using Acme.Product.Core.ValueObjects;
using Xunit;

namespace Acme.Product.Tests.Memory;

/// <summary>
/// Sprint 1 Task 1.2: 端口类型扩展单元测试
/// </summary>
public class Sprint1_ValueObjectTests
{
    #region DetectionResult Tests

    [Fact]
    public void DetectionResult_Create_SetsProperties()
    {
        var detection = new DetectionResult
        {
            Label = "Defect",
            Confidence = 0.95f,
            X = 100,
            Y = 200,
            Width = 50,
            Height = 60
        };

        Assert.Equal("Defect", detection.Label);
        Assert.Equal(0.95f, detection.Confidence);
        Assert.Equal(100, detection.X);
        Assert.Equal(200, detection.Y);
        Assert.Equal(50, detection.Width);
        Assert.Equal(60, detection.Height);
    }

    [Fact]
    public void DetectionResult_CalculatedProperties_AreCorrect()
    {
        var detection = new DetectionResult("Test", 0.8f, 100, 100, 50, 40);

        // CenterX = X + Width / 2 = 100 + 25 = 125
        Assert.Equal(125, detection.CenterX);

        // CenterY = Y + Height / 2 = 100 + 20 = 120
        Assert.Equal(120, detection.CenterY);

        // Area = Width * Height = 50 * 40 = 2000
        Assert.Equal(2000, detection.Area);
    }

    [Fact]
    public void DetectionResult_Constructor_SetsProperties()
    {
        var detection = new DetectionResult("Scratch", 0.87f, 10, 20, 30, 40);

        Assert.Equal("Scratch", detection.Label);
        Assert.Equal(0.87f, detection.Confidence);
        Assert.Equal(10, detection.X);
        Assert.Equal(20, detection.Y);
        Assert.Equal(30, detection.Width);
        Assert.Equal(40, detection.Height);
    }

    [Fact]
    public void DetectionResult_ToString_ContainsKeyInfo()
    {
        var detection = new DetectionResult("Defect", 0.95f, 100, 200, 50, 60);
        var str = detection.ToString();

        Assert.Contains("Detection", str);
        Assert.Contains("Defect", str);
        Assert.Contains("0.95", str);
    }

    #endregion

    #region DetectionList Tests

    [Fact]
    public void DetectionList_CreateEmpty_HasZeroCount()
    {
        var list = new DetectionList();

        Assert.Equal(0, list.Count);
        Assert.Empty(list.Detections);
    }

    [Fact]
    public void DetectionList_CreateFromEnumerable_SetsDetections()
    {
        var detections = new[]
        {
            new DetectionResult("A", 0.9f, 0, 0, 10, 10),
            new DetectionResult("B", 0.8f, 10, 10, 20, 20),
            new DetectionResult("C", 0.7f, 20, 20, 30, 30)
        };

        var list = new DetectionList(detections);

        Assert.Equal(3, list.Count);
        Assert.Equal(3, list.Detections.Count);
    }

    [Fact]
    public void DetectionList_Add_IncreasesCount()
    {
        var list = new DetectionList();
        list.Add(new DetectionResult("A", 0.9f, 0, 0, 10, 10));

        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void DetectionList_GetBestByConfidence_ReturnsHighest()
    {
        var list = new DetectionList(new[]
        {
            new DetectionResult("A", 0.7f, 0, 0, 10, 10),
            new DetectionResult("B", 0.95f, 10, 10, 20, 20),
            new DetectionResult("C", 0.8f, 20, 20, 30, 30)
        });

        var best = list.GetBestByConfidence();

        Assert.NotNull(best);
        Assert.Equal("B", best!.Label);
        Assert.Equal(0.95f, best.Confidence);
    }

    [Fact]
    public void DetectionList_GetByLabel_ReturnsMatching()
    {
        var list = new DetectionList(new[]
        {
            new DetectionResult("Scratch", 0.9f, 0, 0, 10, 10),
            new DetectionResult("Dent", 0.8f, 10, 10, 20, 20)
        });

        var result = list.GetByLabel("Scratch");

        Assert.NotNull(result);
        Assert.Equal("Scratch", result!.Label);
    }

    [Fact]
    public void DetectionList_GetByLabel_NotFound_ReturnsNull()
    {
        var list = new DetectionList(new[]
        {
            new DetectionResult("A", 0.9f, 0, 0, 10, 10)
        });

        var result = list.GetByLabel("NonExistent");

        Assert.Null(result);
    }

    [Fact]
    public void DetectionList_GetMaxArea_ReturnsLargest()
    {
        var list = new DetectionList(new[]
        {
            new DetectionResult("Small", 0.9f, 0, 0, 10, 10),    // Area = 100
            new DetectionResult("Large", 0.8f, 10, 10, 50, 50),  // Area = 2500
            new DetectionResult("Medium", 0.7f, 20, 20, 20, 20)  // Area = 400
        });

        var max = list.GetMaxArea();

        Assert.NotNull(max);
        Assert.Equal("Large", max!.Label);
    }

    [Fact]
    public void DetectionList_AverageConfidence_EmptyList_ReturnsZero()
    {
        var list = new DetectionList();

        Assert.Equal(0, list.AverageConfidence);
    }

    [Fact]
    public void DetectionList_AverageConfidence_CalculatesCorrectly()
    {
        var list = new DetectionList(new[]
        {
            new DetectionResult("A", 0.9f, 0, 0, 10, 10),
            new DetectionResult("B", 0.7f, 10, 10, 20, 20)
        });

        // Average = (0.9 + 0.7) / 2 = 0.8
        Assert.Equal(0.8f, list.AverageConfidence);
    }

    [Fact]
    public void DetectionList_ToString_ContainsCount()
    {
        var list = new DetectionList(new[]
        {
            new DetectionResult("A", 0.9f, 0, 0, 10, 10),
            new DetectionResult("B", 0.8f, 10, 10, 20, 20)
        });

        var str = list.ToString();

        Assert.Contains("DetectionList", str);
        Assert.Contains("2", str);
    }

    #endregion

    #region CircleData Tests

    [Fact]
    public void CircleData_Create_SetsProperties()
    {
        var circle = new CircleData(100, 200, 50);

        Assert.Equal(100, circle.CenterX);
        Assert.Equal(200, circle.CenterY);
        Assert.Equal(50, circle.Radius);
    }

    [Fact]
    public void CircleData_CalculatedProperties_AreCorrect()
    {
        var circle = new CircleData(0, 0, 10);

        // Diameter = 2 * Radius = 20
        Assert.Equal(20, circle.Diameter);

        // Area = π * r² = π * 100 ≈ 314.16
        Assert.True(Math.Abs(circle.Area - 314.159f) < 0.1);

        // Circumference = 2 * π * r = 20π ≈ 62.83
        Assert.True(Math.Abs(circle.Circumference - 62.832f) < 0.1);
    }

    [Fact]
    public void CircleData_DistanceTo_CalculatesCorrectly()
    {
        var circle1 = new CircleData(0, 0, 10);  // 原点
        var circle2 = new CircleData(30, 40, 20); // 3-4-5 三角形，距离 = 50

        var distance = circle1.DistanceTo(circle2);

        Assert.Equal(50, distance);
    }

    [Fact]
    public void CircleData_ToString_ContainsCenterAndRadius()
    {
        var circle = new CircleData(100, 200, 50);
        var str = circle.ToString();

        Assert.Contains("Circle", str);
        Assert.Contains("100", str);
        Assert.Contains("200", str);
        Assert.Contains("50", str);
    }

    #endregion

    #region LineData Tests

    [Fact]
    public void LineData_Create_SetsProperties()
    {
        var line = new LineData(0, 0, 100, 100);

        Assert.Equal(0, line.StartX);
        Assert.Equal(0, line.StartY);
        Assert.Equal(100, line.EndX);
        Assert.Equal(100, line.EndY);
    }

    [Fact]
    public void LineData_Length_CalculatesCorrectly()
    {
        // 3-4-5 三角形
        var line = new LineData(0, 0, 30, 40);

        Assert.Equal(50, line.Length);
    }

    [Fact]
    public void LineData_MidPoint_CalculatesCorrectly()
    {
        var line = new LineData(0, 0, 100, 200);

        Assert.Equal(50, line.MidX);
        Assert.Equal(100, line.MidY);
    }

    [Fact]
    public void LineData_Angle_Horizontal_ReturnsZero()
    {
        var line = new LineData(0, 0, 100, 0);

        Assert.Equal(0, line.Angle);
    }

    [Fact]
    public void LineData_Angle_VerticalUp_Returns90()
    {
        var line = new LineData(0, 0, 0, -100);

        Assert.True(Math.Abs(line.Angle - 90) < 0.01 || Math.Abs(line.Angle + 90) < 0.01);
    }

    [Fact]
    public void LineData_DistanceToPoint_CalculatesCorrectly()
    {
        // 水平线 y = 0，从 (0,0) 到 (100,0)
        var line = new LineData(0, 0, 100, 0);

        // 点 (50, 30) 到线 y=0 的距离 = 30
        var distance = line.DistanceToPoint(50, 30);

        Assert.Equal(30, distance);
    }

    [Fact]
    public void LineData_ToString_ContainsEndpoints()
    {
        var line = new LineData(0, 0, 100, 200);
        var str = line.ToString();

        Assert.Contains("Line", str);
        Assert.Contains("0", str);
        Assert.Contains("100", str);
        Assert.Contains("200", str);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void DetectionResult_Equal_SameValues_AreEqual()
    {
        var d1 = new DetectionResult("A", 0.9f, 10, 20, 30, 40);
        var d2 = new DetectionResult("A", 0.9f, 10, 20, 30, 40);

        Assert.Equal(d1, d2);
    }

    [Fact]
    public void DetectionResult_NotEqual_DifferentValues()
    {
        var d1 = new DetectionResult("A", 0.9f, 10, 20, 30, 40);
        var d2 = new DetectionResult("B", 0.9f, 10, 20, 30, 40);

        Assert.NotEqual(d1, d2);
    }

    [Fact]
    public void CircleData_Equal_SameValues_AreEqual()
    {
        var c1 = new CircleData(100, 200, 50);
        var c2 = new CircleData(100, 200, 50);

        Assert.Equal(c1, c2);
    }

    [Fact]
    public void CircleData_NotEqual_DifferentValues()
    {
        var c1 = new CircleData(100, 200, 50);
        var c2 = new CircleData(100, 200, 60);

        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void LineData_Equal_SameValues_AreEqual()
    {
        var l1 = new LineData(0, 0, 100, 100);
        var l2 = new LineData(0, 0, 100, 100);

        Assert.Equal(l1, l2);
    }

    [Fact]
    public void LineData_NotEqual_DifferentValues()
    {
        var l1 = new LineData(0, 0, 100, 100);
        var l2 = new LineData(0, 0, 100, 200);

        Assert.NotEqual(l1, l2);
    }

    #endregion
}
