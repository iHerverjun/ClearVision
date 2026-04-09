using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using System.Reflection;

namespace Acme.Product.Tests.Operators;

public class ShapeMatchingOperatorTests
{
    private readonly ShapeMatchingOperator _operator;

    public ShapeMatchingOperatorTests()
    {
        _operator = new ShapeMatchingOperator(Substitute.For<ILogger<ShapeMatchingOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeShapeMatching()
    {
        _operator.OperatorType.Should().Be(OperatorType.ShapeMatching);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("Shape", OperatorType.ShapeMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("Shape", OperatorType.ShapeMatching, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithNumLevels_ShouldExposeLevelsUsed()
    {
        var op = new Operator("Shape", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", "MinScore", "double", 0.6, 0.1, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", "AngleStart", "double", 0.0, -180.0, 180.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", "AngleExtent", "double", 0.0, 0.0, 360.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", "AngleStep", "double", 1.0, 0.1, 10.0, true));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", "NumLevels", "int", 4, 1, 6, true));

        using var template = CreateTemplateImage();
        using var scene = CreateSceneImage(template.MatReadOnly);
        var inputs = new Dictionary<string, object>
        {
            { "Image", scene },
            { "Template", template }
        };

        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("NumLevelsUsed");
        result.OutputData!["NumLevelsUsed"].Should().BeOfType<int>();
        ((int)result.OutputData["NumLevelsUsed"]).Should().BeGreaterThan(1);
    }

    [Theory]
    [InlineData(30.0)]
    [InlineData(45.0)]
    [InlineData(90.0)]
    public async Task ExecuteAsync_WithRotatedTightTemplate_ShouldKeepMatch(double angle)
    {
        var op = new Operator("ShapeRotate", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", "MinScore", "double", 0.45, 0.1, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", "AngleStart", "double", -120.0, -180.0, 180.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", "AngleExtent", "double", 240.0, 0.0, 360.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", "AngleStep", "double", 1.0, 0.1, 10.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMin", "ScaleMin", "double", 1.0, 0.2, 3.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMax", "ScaleMax", "double", 1.0, 0.2, 3.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleStep", "ScaleStep", "double", 0.1, 0.01, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", "NumLevels", "int", 2, 1, 6, true));

        using var template = CreateTightRotationTemplate();
        using var rotated = RotateExpanded(template.MatReadOnly, angle);
        using var sceneMat = new Mat(240, 240, MatType.CV_8UC3, Scalar.Black);
        using (var roi = new Mat(sceneMat, new Rect(70, 60, rotated.Width, rotated.Height)))
        {
            rotated.CopyTo(roi);
        }

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(sceneMat),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["FailureReason"].Should().Be(string.Empty);
        var matches = result.OutputData["Matches"].Should().BeAssignableTo<IEnumerable<object>>().Subject.Cast<Dictionary<string, object>>().ToList();
        matches.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithScaledRotatedTarget_ShouldReportApproximatePose()
    {
        var op = new Operator("ShapeScaledRotate", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", "MinScore", "double", 0.45, 0.1, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", "AngleStart", "double", -60.0, -180.0, 180.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", "AngleExtent", "double", 120.0, 0.0, 360.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", "AngleStep", "double", 1.0, 0.1, 10.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMin", "ScaleMin", "double", 0.8, 0.2, 3.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMax", "ScaleMax", "double", 1.4, 0.2, 3.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleStep", "ScaleStep", "double", 0.05, 0.01, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", "NumLevels", "int", 3, 1, 6, true));

        using var template = CreateTightRotationTemplate();
        using var rotatedScaled = RotateExpanded(ResizeTemplate(template.MatReadOnly, 1.2), 30.0);
        using var sceneMat = new Mat(260, 260, MatType.CV_8UC3, Scalar.Black);
        using (var roi = new Mat(sceneMat, new Rect(70, 80, rotatedScaled.Width, rotatedScaled.Height)))
        {
            rotatedScaled.CopyTo(roi);
        }

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(sceneMat),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(true);

        var match = result.OutputData["Matches"]
            .Should().BeAssignableTo<IEnumerable<object>>()
            .Subject
            .Cast<Dictionary<string, object>>()
            .Single();

        Convert.ToDouble(match["Angle"]).Should().BeApproximately(30.0, 3.0);
        Convert.ToDouble(match["Scale"]).Should().BeApproximately(1.2, 0.12);
        Convert.ToDouble(match["CenterX"]).Should().BeInRange(95.0, 150.0);
        Convert.ToDouble(match["CenterY"]).Should().BeInRange(105.0, 165.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithBlankScene_ShouldReturnFailureReason()
    {
        var op = new Operator("ShapeNegative", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", "MinScore", "double", 0.75, 0.1, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", "AngleStart", "double", -30.0, -180.0, 180.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", "AngleExtent", "double", 60.0, 0.0, 360.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", "AngleStep", "double", 1.0, 0.1, 10.0, true));

        using var template = CreateTightRotationTemplate();
        using var sceneMat = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);

        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(sceneMat),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData!["IsMatch"].Should().Be(false);
        result.OutputData["MatchCount"].Should().Be(0);
        result.OutputData["FailureReason"].Should().Be("No rotation-scale template match satisfied the score threshold.");
    }

    [Fact]
    public void HasSufficientSignal_WithLowContrastTemplate_ShouldReturnTrue()
    {
        using var gray = new Mat(40, 40, MatType.CV_8UC1, Scalar.All(100));
        Cv2.Rectangle(gray, new Rect(5, 5, 30, 30), Scalar.All(102), -1);
        Cv2.Line(gray, new Point(8, 20), new Point(32, 20), Scalar.All(98), 2);
        Cv2.Line(gray, new Point(20, 8), new Point(20, 32), Scalar.All(98), 2);
        Cv2.Circle(gray, new Point(14, 14), 4, Scalar.All(104), -1);

        var method = typeof(ShapeMatchingOperator).GetMethod(
            "HasSufficientSignal",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        var accepted = (bool)method!.Invoke(null, new object[] { gray })!;
        accepted.Should().BeTrue();
    }

    private static ImageWrapper CreateTemplateImage()
    {
        var mat = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(8, 8, 24, 24), Scalar.White, -1);
        Cv2.Circle(mat, new Point(20, 20), 6, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateSceneImage(Mat template)
    {
        var mat = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        using (var roi = new Mat(mat, new Rect(90, 70, template.Width, template.Height)))
        {
            template.CopyTo(roi);
        }

        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateTightRotationTemplate()
    {
        var mat = new Mat(48, 48, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(2, 2, 44, 44), Scalar.White, -1);
        Cv2.Line(mat, new Point(4, 24), new Point(44, 24), Scalar.Black, 2);
        Cv2.Line(mat, new Point(24, 4), new Point(24, 44), Scalar.Black, 2);
        Cv2.Circle(mat, new Point(15, 15), 5, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateLowContrastTemplate()
    {
        var mat = new Mat(40, 40, MatType.CV_8UC3, new Scalar(100, 100, 100));
        Cv2.Rectangle(mat, new Rect(5, 5, 30, 30), new Scalar(102, 102, 102), -1);
        Cv2.Line(mat, new Point(8, 20), new Point(32, 20), new Scalar(98, 98, 98), 2);
        Cv2.Line(mat, new Point(20, 8), new Point(20, 32), new Scalar(98, 98, 98), 2);
        Cv2.Circle(mat, new Point(14, 14), 4, new Scalar(104, 104, 104), -1);
        return new ImageWrapper(mat);
    }

    private static Mat RotateExpanded(Mat src, double angle)
    {
        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        using var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        var cos = Math.Abs(rotMatrix.Get<double>(0, 0));
        var sin = Math.Abs(rotMatrix.Get<double>(0, 1));
        var boundWidth = Math.Max(1, (int)Math.Ceiling((src.Height * sin) + (src.Width * cos)));
        var boundHeight = Math.Max(1, (int)Math.Ceiling((src.Height * cos) + (src.Width * sin)));
        rotMatrix.Set(0, 2, rotMatrix.Get<double>(0, 2) + (boundWidth / 2.0) - center.X);
        rotMatrix.Set(1, 2, rotMatrix.Get<double>(1, 2) + (boundHeight / 2.0) - center.Y);

        var rotated = new Mat();
        Cv2.WarpAffine(src, rotated, rotMatrix, new Size(boundWidth, boundHeight), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);
        return rotated;
    }

    private static Mat ResizeTemplate(Mat src, double scale)
    {
        var resized = new Mat();
        Cv2.Resize(src, resized, new Size(0, 0), scale, scale, InterpolationFlags.Linear);
        return resized;
    }
}
