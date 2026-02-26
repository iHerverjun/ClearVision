using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

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
    }

    private static ImageWrapper CreateLineImage()
    {
        var mat = new Mat(240, 320, MatType.CV_8UC3, Scalar.Black);
        Cv2.Line(mat, new Point(20, 30), new Point(300, 210), Scalar.White, 3);
        return new ImageWrapper(mat);
    }
}
