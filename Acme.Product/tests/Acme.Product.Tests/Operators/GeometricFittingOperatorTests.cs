using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using System.Reflection;

namespace Acme.Product.Tests.Operators;

public class GeometricFittingOperatorTests
{
    private readonly GeometricFittingOperator _operator;

    public GeometricFittingOperatorTests()
    {
        _operator = new GeometricFittingOperator(Substitute.For<ILogger<GeometricFittingOperator>>());
    }

    [Fact]
    public void OperatorType_ShouldBeGeometricFitting()
    {
        _operator.OperatorType.Should().Be(OperatorType.GeometricFitting);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("GeoFit", OperatorType.GeometricFitting, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("GeoFit", OperatorType.GeometricFitting, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithNoValidContour_ShouldReturnTopLevelFailure()
    {
        var op = new Operator("GeoFit", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FitType", "Circle", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 100.0, "double"));

        using var blank = new ImageWrapper(new Mat(160, 160, MatType.CV_8UC3, Scalar.Black));
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(blank));

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NoFeature");
    }

    [Fact]
    public async Task ExecuteAsync_WithRansacLineFit_ShouldReturnRobustResult()
    {
        var op = new Operator("GeoFit", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FitType", "Line", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        op.AddParameter(TestHelpers.CreateParameter("RobustMethod", "Ransac", "string"));
        op.AddParameter(TestHelpers.CreateParameter("RansacIterations", "RansacIterations", "int", 200, 10, 5000, true));
        op.AddParameter(TestHelpers.CreateParameter("RansacInlierThreshold", "RansacInlierThreshold", "double", 2.0, 0.1, 100.0, true));

        using var image = CreateLineImage();
        var inputs = TestHelpers.CreateImageInputs(image);
        var result = await _operator.ExecuteAsync(op, inputs);

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("FitResult");

        var fitResult = result.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;
        fitResult.Should().ContainKey("RobustMethod");
        fitResult["RobustMethod"].Should().Be("Ransac");
        fitResult.Should().ContainKeys("InlierCount", "InlierRatio", "RansacMeanResidual", "RansacMaxResidual", "RansacModel", "ResidualMean", "ResidualMax");
    }

    [Fact]
    public async Task ExecuteAsync_WithRansacLineFit_ShouldRejectContourOutliers()
    {
        var leastSquaresOp = new Operator("GeoFitLineLs", OperatorType.GeometricFitting, 0, 0);
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("FitType", "Line", "string"));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("MinPoints", 3, "int"));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("RobustMethod", "LeastSquares", "string"));

        var ransacOp = new Operator("GeoFitLineRansac", OperatorType.GeometricFitting, 0, 0);
        ransacOp.AddParameter(TestHelpers.CreateParameter("FitType", "Line", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        ransacOp.AddParameter(TestHelpers.CreateParameter("MinPoints", 3, "int"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RobustMethod", "Ransac", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RansacIterations", "RansacIterations", "int", 300, 10, 5000, true));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RansacInlierThreshold", "RansacInlierThreshold", "double", 2.0, 0.1, 100.0, true));

        using var image = CreateLineWithOutlierBlockImage();
        var leastSquaresResult = await _operator.ExecuteAsync(leastSquaresOp, TestHelpers.CreateImageInputs(image.AddRef()));
        var ransacResult = await _operator.ExecuteAsync(ransacOp, TestHelpers.CreateImageInputs(image.AddRef()));

        leastSquaresResult.IsSuccess.Should().BeTrue();
        ransacResult.IsSuccess.Should().BeTrue();

        var leastSquaresFit = leastSquaresResult.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var ransacFit = ransacResult.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var leastSquaresGeometry = leastSquaresFit["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var ransacGeometry = ransacFit["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var ransacLine = ransacGeometry["Line"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var ransacAngleError = NormalizeAngleDifference(Convert.ToDouble(ransacLine["Angle"]));

        ransacFit["RobustMethod"].Should().Be("Ransac");
        ransacFit.Should().ContainKey("RansacModel");
        ransacFit.Should().ContainKeys("RansacMeanResidual", "RansacMaxResidual", "ResidualMean", "ResidualMax");
        ransacAngleError.Should().BeLessThan(0.15);
    }

    [Fact]
    public async Task ExecuteAsync_WithRansacCircleFit_ShouldRejectContourOutliers()
    {
        var leastSquaresOp = new Operator("GeoFitCircleLs", OperatorType.GeometricFitting, 0, 0);
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("FitType", "Circle", "string"));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("RobustMethod", "LeastSquares", "string"));

        var ransacOp = new Operator("GeoFitCircleRansac", OperatorType.GeometricFitting, 0, 0);
        ransacOp.AddParameter(TestHelpers.CreateParameter("FitType", "Circle", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        ransacOp.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RobustMethod", "Ransac", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RansacIterations", "RansacIterations", "int", 300, 10, 5000, true));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RansacInlierThreshold", "RansacInlierThreshold", "double", 2.5, 0.1, 100.0, true));

        using var image = CreateCircleWithOutlierTailImage();
        var leastSquaresResult = await _operator.ExecuteAsync(leastSquaresOp, TestHelpers.CreateImageInputs(image.AddRef()));
        var ransacResult = await _operator.ExecuteAsync(ransacOp, TestHelpers.CreateImageInputs(image.AddRef()));

        leastSquaresResult.IsSuccess.Should().BeTrue();
        ransacResult.IsSuccess.Should().BeTrue();

        var leastSquaresFit = leastSquaresResult.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var ransacFit = ransacResult.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var leastSquaresGeometry = leastSquaresFit["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var ransacGeometry = ransacFit["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var ransacCenter = ransacGeometry["Center"].Should().BeOfType<Position>().Subject;
        var ransacRadius = Convert.ToDouble(ransacGeometry["Radius"]);

        ransacFit["RobustMethod"].Should().Be("Ransac");
        ransacFit.Should().ContainKey("InlierCount");
        ransacFit.Should().ContainKey("RansacModel");
        ransacRadius.Should().BeApproximately(50.0, 0.40);
        ransacCenter.X.Should().BeApproximately(120.0, 0.40);
        ransacCenter.Y.Should().BeApproximately(120.0, 0.40);
    }

    [Fact]
    public async Task ExecuteAsync_WithPrecisionLineFit_ShouldRecoverIndustrialGeometry()
    {
        var op = new Operator("GeoFitLinePrecision", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FitType", "Line", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 80.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinArea", 60, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));

        var start = new Point2d(20.5, 190.25);
        var end = new Point2d(290.75, 52.5);
        var expectedAngle = Math.Atan2(end.Y - start.Y, end.X - start.X) * 180.0 / Math.PI;

        using var image = IndustrialMeasurementSceneFactory.CreateLineImage(320, 240, start, end, 4.0, supersample: 16);
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var fitResult = result.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var geometry = fitResult["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var line = geometry["Line"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var actualAngle = Convert.ToDouble(line["Angle"]);

        NormalizeAngleDifference(actualAngle - expectedAngle).Should().BeLessThan(0.10);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.20);
        Convert.ToDouble(fitResult["ResidualMean"]).Should().BeLessThan(2.5);
    }

    [Fact]
    public async Task ExecuteAsync_WithPrecisionCircleFit_ShouldRecoverIndustrialGeometry()
    {
        var op = new Operator("GeoFitCirclePrecision", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FitType", "Circle", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 96.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinArea", 2000, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledCircleImage(
            width: 320,
            height: 240,
            center: new Point2d(120.4, 99.6),
            radius: 52.3,
            supersample: 16);
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var geometry = ((Dictionary<string, object>)result.OutputData!["FitResult"])["Geometry"]
            .Should()
            .BeOfType<Dictionary<string, object>>()
            .Subject;
        var center = geometry["Center"].Should().BeOfType<Position>().Subject;
        var radius = Convert.ToDouble(geometry["Radius"]);

        center.X.Should().BeApproximately(120.4, 0.20);
        center.Y.Should().BeApproximately(99.6, 0.20);
        radius.Should().BeApproximately(52.3, 0.20);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.20);
    }

    private static ImageWrapper CreateLineImage()
    {
        var mat = new Mat(240, 320, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 30), new Point(300, 210), Scalar.White, 3);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateCircleWithOutlierTailImage()
    {
        var mat = new Mat(280, 320, MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(mat, new Point(120, 120), 50, Scalar.White, -1);
        Cv2.Rectangle(mat, new Rect(190, 104, 70, 32), Scalar.White, -1);
        return new ImageWrapper(mat);
    }

    private static ImageWrapper CreateLineWithOutlierBlockImage()
    {
        var mat = new Mat(240, 320, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 120), new Point(220, 120), Scalar.White, 3);
        Cv2.Rectangle(mat, new Rect(230, 40, 50, 120), Scalar.White, -1);
        return new ImageWrapper(mat);
    }
    private static ImageWrapper CreateEllipseWithOutlierTailImage()
    {
        var mat = new Mat(320, 320, MatType.CV_8UC3, Scalar.Black);
        Cv2.Ellipse(mat, new RotatedRect(new Point2f(160, 160), new Size2f(160, 80), 30), Scalar.White, -1);
        Cv2.Rectangle(mat, new Rect(220, 180, 80, 50), Scalar.White, -1); // 加上一块明显的离群色块
        return new ImageWrapper(mat);
    }

    [Fact]
    public async Task ExecuteAsync_WithRansacEllipseFit_ShouldRejectContourOutliers()
    {
        var leastSquaresOp = new Operator("GeoFitEllipseLs", OperatorType.GeometricFitting, 0, 0);
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("FitType", "Ellipse", "string"));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        leastSquaresOp.AddParameter(TestHelpers.CreateParameter("RobustMethod", "LeastSquares", "string"));

        var ransacOp = new Operator("GeoFitEllipseRansac", OperatorType.GeometricFitting, 0, 0);
        ransacOp.AddParameter(TestHelpers.CreateParameter("FitType", "Ellipse", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("Threshold", "Threshold", "double", 100.0, 0.0, 255.0, true));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RobustMethod", "Ransac", "string"));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RansacIterations", "RansacIterations", "int", 500, 10, 5000, true));
        ransacOp.AddParameter(TestHelpers.CreateParameter("RansacInlierThreshold", "RansacInlierThreshold", "double", 3.0, 0.1, 100.0, true));

        using var image = CreateEllipseWithOutlierTailImage();
        var leastSquaresResult = await _operator.ExecuteAsync(leastSquaresOp, TestHelpers.CreateImageInputs(image.AddRef()));
        var ransacResult = await _operator.ExecuteAsync(ransacOp, TestHelpers.CreateImageInputs(image.AddRef()));

        leastSquaresResult.IsSuccess.Should().BeTrue();
        ransacResult.IsSuccess.Should().BeTrue();

        var leastSquaresFit = leastSquaresResult.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var ransacFit = ransacResult.OutputData!["FitResult"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var leastSquaresGeometry = leastSquaresFit["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;
        var ransacGeometry = ransacFit["Geometry"].Should().BeOfType<Dictionary<string, object>>().Subject;

        var leastSquaresCenter = leastSquaresGeometry["Center"].Should().BeOfType<Position>().Subject;
        var ransacCenter = ransacGeometry["Center"].Should().BeOfType<Position>().Subject;

        // 理论中心是在 (160, 160)
        var leastSquaresError = Math.Abs(leastSquaresCenter.X - 160.0) + Math.Abs(leastSquaresCenter.Y - 160.0);
        var ransacError = Math.Abs(ransacCenter.X - 160.0) + Math.Abs(ransacCenter.Y - 160.0);

        ransacFit["RobustMethod"].Should().Be("Ransac");
        ransacFit.Should().ContainKey("InlierCount");
        ransacFit.Should().ContainKey("RansacModel");
        
        ransacError.Should().BeLessThan(leastSquaresError);
        ransacCenter.X.Should().BeApproximately(160.0, 8.0);
        ransacCenter.Y.Should().BeApproximately(160.0, 8.0);
        
        var ransacMajorAxis = Convert.ToDouble(ransacGeometry["MajorAxis"]);
        var ransacMinorAxis = Convert.ToDouble(ransacGeometry["MinorAxis"]);
        
        // 确保能大概拟合出正确的轴长 (160, 80)
        Math.Max(ransacMajorAxis, ransacMinorAxis).Should().BeApproximately(160.0, 10.0);
        Math.Min(ransacMajorAxis, ransacMinorAxis).Should().BeApproximately(80.0, 10.0);
    }

    [Fact]
    public async Task ExecuteAsync_WithPrecisionEllipseFit_ShouldRecoverIndustrialGeometry()
    {
        var op = new Operator("GeoFitEllipsePrecision", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(TestHelpers.CreateParameter("FitType", "Ellipse", "string"));
        op.AddParameter(TestHelpers.CreateParameter("Threshold", 96.0, "double"));
        op.AddParameter(TestHelpers.CreateParameter("MinArea", 3000, "int"));
        op.AddParameter(TestHelpers.CreateParameter("ContourSelection", "BestResidual", "string"));

        using var image = IndustrialMeasurementSceneFactory.CreateFilledEllipseImage(
            width: 360,
            height: 280,
            center: new Point2d(180.25, 140.75),
            size: new Size2d(150.0, 90.0),
            angleDeg: 27.5,
            supersample: 16);
        var result = await _operator.ExecuteAsync(op, TestHelpers.CreateImageInputs(image));

        result.IsSuccess.Should().BeTrue(result.ErrorMessage);
        var geometry = ((Dictionary<string, object>)result.OutputData!["FitResult"])["Geometry"]
            .Should()
            .BeOfType<Dictionary<string, object>>()
            .Subject;
        var center = geometry["Center"].Should().BeOfType<Position>().Subject;
        var majorAxis = Convert.ToDouble(geometry["MajorAxis"]);
        var minorAxis = Convert.ToDouble(geometry["MinorAxis"]);
        var angle = Convert.ToDouble(geometry["Angle"]);

        center.X.Should().BeApproximately(180.25, 0.30);
        center.Y.Should().BeApproximately(140.75, 0.30);
        Math.Max(majorAxis, minorAxis).Should().BeApproximately(150.0, 0.40);
        Math.Min(majorAxis, minorAxis).Should().BeApproximately(90.0, 0.40);
        NormalizeEllipseAngleDifference(angle, 27.5).Should().BeLessThan(0.55);
        Convert.ToDouble(result.OutputData["UncertaintyPx"]).Should().BeLessThan(0.20);
    }

    [Fact]
    public void EllipseResidual_WithHighEccentricityAndRotation_ShouldBeStableForNormalOffsets()
    {
        var residualMethod = typeof(GeometricFittingOperator).GetMethod(
            "ApproximateGeometricDistancePointToEllipse",
            BindingFlags.Static | BindingFlags.NonPublic);
        residualMethod.Should().NotBeNull();

        var ellipse = new RotatedRect(
            center: new Point2f(160f, 120f),
            size: new Size2f(240f, 40f),
            angle: 33f);

        const double expectedOffset = 1.5;
        var sampleAngles = new[] { 15.0, 40.0, 70.0, 110.0, 150.0, 200.0, 250.0, 320.0 };
        foreach (var angle in sampleAngles)
        {
            var offsetPoint = CreatePointOffsetAlongEllipseNormal(ellipse, angle, expectedOffset);
            var residual = Convert.ToDouble(residualMethod!.Invoke(null, new object[] { offsetPoint, ellipse }));

            residual.Should().BeGreaterThan(0.0);
            residual.Should().BeApproximately(expectedOffset, 0.40);
        }
    }

    private static double NormalizeAngleDifference(double deltaDegrees)
    {
        var normalized = deltaDegrees % 180.0;
        if (normalized > 90.0)
        {
            normalized -= 180.0;
        }
        else if (normalized < -90.0)
        {
            normalized += 180.0;
        }

        return Math.Abs(normalized);
    }

    private static double NormalizeEllipseAngleDifference(double actualAngle, double expectedAngle)
    {
        return new[]
        {
            NormalizeAngleDifference(actualAngle - expectedAngle),
            NormalizeAngleDifference((actualAngle + 90.0) - expectedAngle),
            NormalizeAngleDifference((actualAngle - 90.0) - expectedAngle)
        }.Min();
    }

    private static Point2f CreatePointOffsetAlongEllipseNormal(RotatedRect ellipse, double angleDegrees, double offsetPixels)
    {
        var a = ellipse.Size.Width / 2.0;
        var b = ellipse.Size.Height / 2.0;
        var t = angleDegrees * Math.PI / 180.0;

        var x = a * Math.Cos(t);
        var y = b * Math.Sin(t);

        // Normal direction from implicit ellipse gradient in local coordinates.
        var gx = x / (a * a);
        var gy = y / (b * b);
        var gNorm = Math.Sqrt((gx * gx) + (gy * gy));
        gx /= gNorm;
        gy /= gNorm;

        var localOffsetX = x + (offsetPixels * gx);
        var localOffsetY = y + (offsetPixels * gy);

        var angleRad = ellipse.Angle * Math.PI / 180.0;
        var cosA = Math.Cos(angleRad);
        var sinA = Math.Sin(angleRad);

        var worldX = (localOffsetX * cosA) - (localOffsetY * sinA) + ellipse.Center.X;
        var worldY = (localOffsetX * sinA) + (localOffsetY * cosA) + ellipse.Center.Y;
        return new Point2f((float)worldX, (float)worldY);
    }
}
