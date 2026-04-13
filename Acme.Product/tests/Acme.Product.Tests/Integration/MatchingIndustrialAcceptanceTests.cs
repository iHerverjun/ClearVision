using System.Numerics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.PointCloud;
using Acme.Product.Tests.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Integration;

public sealed class MatchingIndustrialAcceptanceTests
{
    [Fact]
    public async Task TemplateMatching_RoiMaskGradient_ShouldReturnGlobalCoordinates()
    {
        var templateMatch = new TemplateMatchOperator(NullLogger<TemplateMatchOperator>.Instance);
        var op = new Operator("template_acceptance", OperatorType.TemplateMatching, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("Domain", "Gradient", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 0.45, "double"));
        op.AddParameter(TestHelpers.CreateParameter("UseRoi", true, "bool"));
        op.AddParameter(TestHelpers.CreateParameter("RoiX", 80, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiY", 90, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiWidth", 90, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RoiHeight", 90, "int"));

        using var template = CreatePatternTemplate();
        using var scene = new Mat(220, 220, MatType.CV_8UC3, new Scalar(20, 20, 20));
        using var placed = template.Clone();
        placed.ConvertTo(placed, placed.Type(), 0.8, 60);
        CopyTemplate(scene, placed, 104, 118);

        using var mask = new Mat(220, 220, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mask, new Rect(96, 110, 48, 48), Scalar.White, -1);

        var result = await templateMatch.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = scene.ToBytes(".png"),
            ["Template"] = template.ToBytes(".png"),
            ["Mask"] = mask.ToBytes(".png")
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["IsMatch"].Should().Be(true);
        result.OutputData["Method"].Should().Be("CCoeffNormed:Gradient");
        var position = result.OutputData["Position"].Should().BeOfType<Position>().Subject;
        position.X.Should().BeGreaterThan(118);
        position.Y.Should().BeGreaterThan(132);
    }

    [Fact]
    public async Task ShapeMatching_PositionCorrection_ShouldStayInPixelSpace()
    {
        var shapeMatch = new ShapeMatchingOperator(NullLogger<ShapeMatchingOperator>.Instance);
        var positionCorrection = new PositionCorrectionOperator(NullLogger<PositionCorrectionOperator>.Instance);

        var shapeOp = new Operator("shape_acceptance", OperatorType.ShapeMatching, 0, 0);
        shapeOp.AddParameter(TestHelpers.CreateParameter("MinScore", 0.45, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("AngleStart", -90.0, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("AngleExtent", 180.0, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("AngleStep", 1.0, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("ScaleMin", 1.0, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("ScaleMax", 1.0, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("ScaleStep", 0.1, "double"));
        shapeOp.AddParameter(TestHelpers.CreateParameter("NumLevels", 2, "int"));

        using var template = CreatePatternTemplate();
        using var rotated = RotateExpanded(template, 45.0);
        using var scene = new Mat(240, 240, MatType.CV_8UC3, Scalar.Black);
        CopyTemplate(scene, rotated, 80, 70);

        var matchResult = await shapeMatch.ExecuteAsync(shapeOp, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = new ImageWrapper(template)
        });

        matchResult.IsSuccess.Should().BeTrue(matchResult.ErrorMessage);
        matchResult.OutputData!["IsMatch"].Should().Be(true);
        var primaryMatch = matchResult.OutputData["Matches"]
            .Should().BeAssignableTo<IEnumerable<object>>()
            .Subject
            .Cast<Dictionary<string, object>>()
            .First();
        var referencePoint = new Position(Convert.ToDouble(primaryMatch["CenterX"]), Convert.ToDouble(primaryMatch["CenterY"]));

        var correctionOp = new Operator("position_correction", OperatorType.PositionCorrection, 0, 0);
        correctionOp.AddParameter(TestHelpers.CreateParameter("CorrectionMode", "Translation", "string"));
        var correctionResult = await positionCorrection.ExecuteAsync(correctionOp, new Dictionary<string, object>
        {
            ["ReferencePoint"] = referencePoint,
            ["BasePoint"] = new Position(50, 50),
            ["RoiX"] = 60,
            ["RoiY"] = 70
        });

        correctionResult.IsSuccess.Should().BeTrue();
        Convert.ToInt32(correctionResult.OutputData!["CorrectedX"]).Should().BeGreaterThan(60);
        Convert.ToInt32(correctionResult.OutputData["CorrectedY"]).Should().BeGreaterThan(70);
        correctionResult.OutputData.Should().ContainKey("AppliedOffsetX");
        correctionResult.OutputData.Should().ContainKey("AppliedOffsetY");
        correctionResult.OutputData.Should().ContainKey("TransformMatrix");
        correctionResult.OutputData["CompensationMode"].Should().Be("Translation");
    }

    [Fact]
    public async Task PpfMatch_SymmetricSphere_ShouldSurfaceAmbiguityAtOperatorLevel()
    {
        var ppfMatch = new PPFMatchOperator(NullLogger<PPFMatchOperator>.Instance);
        var op = new Operator("ppf_symmetric_acceptance", OperatorType.PPFMatch, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("NormalRadius", 0.05, "double"));
        op.AddParameter(TestHelpers.CreateParameter("FeatureRadius", 0.10, "double"));
        op.AddParameter(TestHelpers.CreateParameter("NumSamples", 180, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ModelRefStride", 2, "int"));
        op.AddParameter(TestHelpers.CreateParameter("RansacIterations", 1200, "int"));
        op.AddParameter(TestHelpers.CreateParameter("InlierThreshold", 0.01, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinInliers", 120, "int"));

        var generator = new SyntheticPointCloudGenerator(seed: 401);
        using var model = generator.GenerateSphere(Vector3.Zero, radius: 0.20f, numPoints: 2600, noise: 0.0004f, includeColors: false, includeNormals: true);
        var gt = Matrix4x4.CreateFromYawPitchRoll(0.45f, 0.12f, -0.35f) * Matrix4x4.CreateTranslation(0.08f, -0.03f, 0.02f);
        using var scene = model.Transform(gt);

        var result = await ppfMatch.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["ModelPointCloud"] = model,
            ["ScenePointCloud"] = scene
        });

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        result.OutputData!["AmbiguityDetected"].Should().Be(true);
        result.OutputData["IsMatch"].Should().Be(false);
        result.OutputData["FailureReason"].Should().Be("Ambiguous coarse pose solution.");
    }

    private static Mat CreatePatternTemplate()
    {
        var mat = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(4, 4, 32, 32), Scalar.White, -1);
        Cv2.Line(mat, new Point(4, 20), new Point(36, 20), Scalar.Black, 2);
        Cv2.Circle(mat, new Point(14, 14), 4, Scalar.Black, -1);
        return mat;
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

    private static void CopyTemplate(Mat scene, Mat template, int x, int y)
    {
        using var roi = new Mat(scene, new Rect(x, y, template.Width, template.Height));
        template.CopyTo(roi);
    }
}
