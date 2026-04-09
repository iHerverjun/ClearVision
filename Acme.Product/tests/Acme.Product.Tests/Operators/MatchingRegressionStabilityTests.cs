using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class MatchingRegressionStabilityTests
{
    [Fact]
    public async Task TemplateMatching_ShouldBeStableAcrossRepeatedRuns()
    {
        var op = new Operator("template_stable", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Domain", "Gradient", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.45, "double"));

        var matcher = new TemplateMatchOperator(Substitute.For<ILogger<TemplateMatchOperator>>());

        using var scene = new Mat(180, 180, MatType.CV_8UC3, new Scalar(25, 25, 25));
        using var template = CreatePatternTemplate();
        using var shifted = template.MatReadOnly.Clone();
        shifted.ConvertTo(shifted, shifted.Type(), 1.15, 35);
        CopyTemplate(scene, shifted, 72, 64);
        var sceneBytes = scene.ToBytes(".png");
        var templateBytes = template.MatReadOnly.ToBytes(".png");

        var scores = new List<double>();
        var positions = new List<Position>();
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var result = await matcher.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = sceneBytes,
                ["Template"] = templateBytes
            });

            result.IsSuccess.Should().BeTrue();
            result.OutputData!["IsMatch"].Should().Be(true);
            scores.Add(Convert.ToDouble(result.OutputData["Score"]));
            positions.Add(result.OutputData["Position"].Should().BeOfType<Position>().Subject);
        }

        scores.Should().OnlyContain(score => score > 0.45);
        scores.Max().Should().BeApproximately(scores.Min(), 1e-6);
        positions.Select(position => position.X).Should().OnlyContain(x => Math.Abs(x - positions[0].X) < 1e-6);
        positions.Select(position => position.Y).Should().OnlyContain(y => Math.Abs(y - positions[0].Y) < 1e-6);
    }

    [Fact]
    public async Task ShapeMatching_ShouldBeStableAcrossRepeatedRuns()
    {
        var op = new Operator("shape_stable", OperatorType.ShapeMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("MinScore", "MinScore", "double", 0.45, 0.1, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStart", "AngleStart", "double", -120.0, -180.0, 180.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleExtent", "AngleExtent", "double", 240.0, 0.0, 360.0, true));
        op.AddParameter(TestHelpers.CreateParameter("AngleStep", "AngleStep", "double", 1.0, 0.1, 10.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMin", "ScaleMin", "double", 1.0, 0.2, 3.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleMax", "ScaleMax", "double", 1.0, 0.2, 3.0, true));
        op.AddParameter(TestHelpers.CreateParameter("ScaleStep", "ScaleStep", "double", 0.1, 0.01, 1.0, true));
        op.AddParameter(TestHelpers.CreateParameter("NumLevels", "NumLevels", "int", 2, 1, 6, true));

        var matcher = new ShapeMatchingOperator(Substitute.For<ILogger<ShapeMatchingOperator>>());

        using var template = CreatePatternTemplate();
        using var rotated = RotateExpanded(template.MatReadOnly, 45.0);
        using var scene = new Mat(220, 220, MatType.CV_8UC3, Scalar.Black);
        using (var roi = new Mat(scene, new Rect(84, 72, rotated.Width, rotated.Height)))
        {
            rotated.CopyTo(roi);
        }
        var sceneBytes = scene.ToBytes(".png");
        var templateBytes = template.MatReadOnly.ToBytes(".png");

        var scores = new List<double>();
        var centerXs = new List<double>();
        var centerYs = new List<double>();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var result = await matcher.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = sceneBytes,
                ["Template"] = templateBytes
            });

            result.IsSuccess.Should().BeTrue();
            result.OutputData!["IsMatch"].Should().Be(true);
            var match = result.OutputData["Matches"]
                .Should().BeAssignableTo<IEnumerable<object>>()
                .Subject
                .Cast<Dictionary<string, object>>()
                .Single();

            scores.Add(Convert.ToDouble(match["Score"]));
            centerXs.Add(Convert.ToDouble(match["CenterX"]));
            centerYs.Add(Convert.ToDouble(match["CenterY"]));
        }

        scores.Max().Should().BeApproximately(scores.Min(), 1e-6);
        centerXs.Max().Should().BeApproximately(centerXs.Min(), 1e-6);
        centerYs.Max().Should().BeApproximately(centerYs.Min(), 1e-6);
    }

    [Fact]
    public async Task PlanarMatching_ShouldBeStableAcrossRepeatedRuns()
    {
        var op = new Operator("planar_stable", OperatorType.PlanarMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("DetectorType", "ORB"));
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchCount", 6));
        op.Parameters.Add(TestHelpers.CreateParameter("MinInliers", 4));
        op.Parameters.Add(TestHelpers.CreateParameter("ScoreThreshold", 0.2));

        var matcher = new PlanarMatchingOperator(Substitute.For<ILogger<PlanarMatchingOperator>>());

        using var template = CreateFeatureRichImage();
        using var scene = WarpIntoScene(template.MatReadOnly);
        var sceneBytes = scene.ToBytes(".png");
        var templateBytes = template.MatReadOnly.ToBytes(".png");

        var scores = new List<double>();
        var inlierCounts = new List<int>();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var result = await matcher.ExecuteAsync(op, new Dictionary<string, object>
            {
                ["Image"] = sceneBytes,
                ["Template"] = templateBytes
            });

            result.IsSuccess.Should().BeTrue();
            result.OutputData!["IsMatch"].Should().Be(true);
            result.OutputData["VerificationPassed"].Should().Be(true);
            scores.Add(Convert.ToDouble(result.OutputData["Score"]));
            inlierCounts.Add(Convert.ToInt32(result.OutputData["InlierCount"]));
        }

        scores.Max().Should().BeApproximately(scores.Min(), 1e-6);
        inlierCounts.Should().OnlyContain(count => count == inlierCounts[0]);
    }

    private static ImageWrapper CreatePatternTemplate()
    {
        var mat = new Mat(48, 48, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4, 4, 40, 40), Scalar.White, -1);
        Cv2.Line(mat, new Point(4, 24), new Point(44, 24), Scalar.Black, 2);
        Cv2.Line(mat, new Point(24, 4), new Point(24, 44), Scalar.Black, 2);
        Cv2.Circle(mat, new Point(15, 15), 5, Scalar.Black, -1);
        return new ImageWrapper(mat);
    }

    private static void CopyTemplate(Mat scene, Mat template, int x, int y)
    {
        using var roi = new Mat(scene, new Rect(x, y, template.Width, template.Height));
        template.CopyTo(roi);
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

    private static ImageWrapper CreateFeatureRichImage()
    {
        var mat = new Mat(400, 400, MatType.CV_8UC3, Scalar.Gray);
        Cv2.Rectangle(mat, new Rect(50, 50, 100, 100), Scalar.Black, -1);
        Cv2.Rectangle(mat, new Rect(200, 150, 120, 80), Scalar.White, -1);
        Cv2.Circle(mat, new Point(300, 300), 50, Scalar.Black, -1);
        for (var i = 0; i < 10; i++)
        {
            Cv2.Line(mat, new Point(i * 40, 0), new Point(i * 40, 400), Scalar.DarkGray, 2);
        }

        return new ImageWrapper(mat);
    }

    private static Mat WarpIntoScene(Mat template)
    {
        var scene = new Mat(520, 520, MatType.CV_8UC3, Scalar.Gray);
        var src = new[]
        {
            new Point2f(0, 0),
            new Point2f(template.Width - 1, 0),
            new Point2f(template.Width - 1, template.Height - 1),
            new Point2f(0, template.Height - 1)
        };
        var dst = new[]
        {
            new Point2f(90, 70),
            new Point2f(390, 110),
            new Point2f(360, 410),
            new Point2f(120, 430)
        };

        using var homography = Cv2.GetPerspectiveTransform(src, dst);
        Cv2.WarpPerspective(template, scene, homography, scene.Size(), InterpolationFlags.Linear, BorderTypes.Transparent);
        return scene;
    }
}
