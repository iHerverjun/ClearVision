using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class Phase42RegionProcessingOperatorTests
{
    [Fact]
    public async Task RegionErosion_ShouldReduceArea()
    {
        var sut = new RegionErosionOperator(Substitute.For<ILogger<RegionErosionOperator>>());
        var region = CreateRectangleRegion(10, 10, 30, 24);
        var op = new Operator("RegionErosion", OperatorType.RegionErosion, 0, 0);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var eroded = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;
        eroded.Area.Should().BeLessThan(region.Area);
    }

    [Fact]
    public async Task RegionDilation_ShouldIncreaseArea()
    {
        var sut = new RegionDilationOperator(Substitute.For<ILogger<RegionDilationOperator>>());
        var region = CreateRectangleRegion(20, 20, 20, 16);
        var op = new Operator("RegionDilation", OperatorType.RegionDilation, 0, 0);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var dilated = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;
        dilated.Area.Should().BeGreaterThan(region.Area);
    }

    [Fact]
    public async Task RegionOpening_ShouldSuppressSmallNoise()
    {
        var sut = new RegionOpeningOperator(Substitute.For<ILogger<RegionOpeningOperator>>());
        var region = CreateRegionWithNoise();
        var op = new Operator("RegionOpening", OperatorType.RegionOpening, 0, 0);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var opened = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;
        opened.Area.Should().BeLessOrEqualTo(region.Area);
    }

    [Fact]
    public async Task RegionClosing_ShouldFillSmallHole()
    {
        var sut = new RegionClosingOperator(Substitute.For<ILogger<RegionClosingOperator>>());
        var region = CreateRegionWithHole();
        var op = new Operator("RegionClosing", OperatorType.RegionClosing, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("KernelWidth", 7, "int"));
        op.Parameters.Add(TestHelpers.CreateParameter("KernelHeight", 7, "int"));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var closed = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;
        closed.Area.Should().BeGreaterThan(region.Area);
    }

    [Fact]
    public async Task RegionSkeleton_ShouldProduceThinnerRegion()
    {
        var sut = new RegionSkeletonOperator(Substitute.For<ILogger<RegionSkeletonOperator>>());
        var region = CreateThickCrossRegion();
        var op = new Operator("RegionSkeleton", OperatorType.RegionSkeleton, 0, 0);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var skeleton = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;
        skeleton.Area.Should().BeGreaterThan(0);
        skeleton.Area.Should().BeLessThan(region.Area);
    }

    [Fact]
    public async Task RegionSkeleton_ShouldPreserveOriginalCoordinates()
    {
        var sut = new RegionSkeletonOperator(Substitute.For<ILogger<RegionSkeletonOperator>>());
        var region = CreateOffsetThickCrossRegion();
        var op = new Operator("RegionSkeleton", OperatorType.RegionSkeleton, 0, 0);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var skeleton = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;
        skeleton.BoundingBox.X.Should().BeGreaterThanOrEqualTo(region.BoundingBox.X);
        skeleton.BoundingBox.Y.Should().BeGreaterThanOrEqualTo(region.BoundingBox.Y);
        skeleton.BoundingBox.Right.Should().BeLessThanOrEqualTo(region.BoundingBox.Right);
        skeleton.BoundingBox.Bottom.Should().BeLessThanOrEqualTo(region.BoundingBox.Bottom);
    }

    [Fact]
    public async Task RegionDilation_WithEvenSizedKernel_ShouldMatchOpenCv()
    {
        var sut = new RegionDilationOperator(Substitute.For<ILogger<RegionDilationOperator>>());
        using var mat = new Mat(40, 40, MatType.CV_8UC1, Scalar.Black);
        mat.Set(20, 20, (byte)255);
        var region = Region.FromMat(mat);

        var op = new Operator("RegionDilation", OperatorType.RegionDilation, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("KernelWidth", 4, "int"));
        op.Parameters.Add(TestHelpers.CreateParameter("KernelHeight", 4, "int"));
        op.Parameters.Add(TestHelpers.CreateParameter("KernelShape", "Rectangle", "string"));

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object> { ["Region"] = region });

        result.IsSuccess.Should().BeTrue();
        var dilated = result.OutputData!["Region"].Should().BeOfType<Region>().Subject;

        using var expected = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(4, 4));
        Cv2.Dilate(mat, expected, kernel);
        var expectedRegion = Region.FromMat(expected);

        dilated.RunLengths.Should().Equal(expectedRegion.RunLengths);
    }

    [Fact]
    public async Task RegionBooleanOperators_ShouldProduceConsistentAreas()
    {
        var unionSut = new RegionUnionOperator(Substitute.For<ILogger<RegionUnionOperator>>());
        var intersectionSut = new RegionIntersectionOperator(Substitute.For<ILogger<RegionIntersectionOperator>>());
        var diffSut = new RegionDifferenceOperator(Substitute.For<ILogger<RegionDifferenceOperator>>());
        var complementSut = new RegionComplementOperator(Substitute.For<ILogger<RegionComplementOperator>>());

        var regionA = CreateRectangleRegion(10, 10, 20, 20);
        var regionB = CreateRectangleRegion(20, 18, 20, 20);

        var unionResult = await unionSut.ExecuteAsync(new Operator("RegionUnion", OperatorType.RegionUnion, 0, 0), new Dictionary<string, object>
        {
            ["Region1"] = regionA,
            ["Region2"] = regionB
        });
        var intersectionResult = await intersectionSut.ExecuteAsync(new Operator("RegionIntersection", OperatorType.RegionIntersection, 0, 0), new Dictionary<string, object>
        {
            ["Region1"] = regionA,
            ["Region2"] = regionB
        });
        var differenceResult = await diffSut.ExecuteAsync(new Operator("RegionDifference", OperatorType.RegionDifference, 0, 0), new Dictionary<string, object>
        {
            ["Region1"] = regionA,
            ["Region2"] = regionB
        });
        var complementResult = await complementSut.ExecuteAsync(new Operator("RegionComplement", OperatorType.RegionComplement, 0, 0), new Dictionary<string, object>
        {
            ["Region"] = regionA,
            ["ImageWidth"] = 60,
            ["ImageHeight"] = 50
        });

        unionResult.IsSuccess.Should().BeTrue();
        intersectionResult.IsSuccess.Should().BeTrue();
        differenceResult.IsSuccess.Should().BeTrue();
        complementResult.IsSuccess.Should().BeTrue();

        var union = (Region)unionResult.OutputData!["Region"];
        var intersection = (Region)intersectionResult.OutputData!["Region"];
        var difference = (Region)differenceResult.OutputData!["Region"];
        var complement = (Region)complementResult.OutputData!["Region"];

        union.Area.Should().Be(regionA.Area + regionB.Area - intersection.Area);
        difference.Area.Should().Be(regionA.Area - intersection.Area);
        complement.Area.Should().Be(60 * 50 - regionA.Area);
    }

    private static Region CreateRectangleRegion(int x, int y, int width, int height, int imageWidth = 80, int imageHeight = 80)
    {
        using var mat = new Mat(imageHeight, imageWidth, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(x, y, width, height), Scalar.White, -1);
        return Region.FromMat(mat);
    }

    private static Region CreateRegionWithNoise()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(18, 18, 28, 20), Scalar.White, -1);
        Cv2.Circle(mat, new Point(8, 8), 1, Scalar.White, -1);
        Cv2.Circle(mat, new Point(70, 12), 1, Scalar.White, -1);
        return Region.FromMat(mat);
    }

    private static Region CreateRegionWithHole()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(15, 15, 36, 30), Scalar.White, -1);
        Cv2.Rectangle(mat, new Rect(30, 26, 4, 4), Scalar.Black, -1);
        return Region.FromMat(mat);
    }

    private static Region CreateThickCrossRegion()
    {
        using var mat = new Mat(80, 80, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(34, 12, 12, 50), Scalar.White, -1);
        Cv2.Rectangle(mat, new Rect(18, 30, 44, 12), Scalar.White, -1);
        return Region.FromMat(mat);
    }

    private static Region CreateOffsetThickCrossRegion()
    {
        using var mat = new Mat(120, 120, MatType.CV_8UC1, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(58, 32, 12, 58), Scalar.White, -1);
        Cv2.Rectangle(mat, new Rect(38, 52, 52, 12), Scalar.White, -1);
        return Region.FromMat(mat);
    }
}
