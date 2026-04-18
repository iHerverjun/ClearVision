using System.Reflection;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class FeatureMatchOperatorBaseTests
{
    [Fact]
    public void EstimateAndVerifyHomography_WhenVerificationFails_ShouldDropProjectedPoseData()
    {
        var sut = new AkazeFeatureMatchOperator(Substitute.For<ILogger<AkazeFeatureMatchOperator>>());
        var method = typeof(AkazeFeatureMatchOperator).BaseType!.GetMethod(
            "EstimateAndVerifyHomography",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var templateKeyPoints = new[]
        {
            new KeyPoint(0, 0, 1),
            new KeyPoint(20, 0, 1),
            new KeyPoint(20, 20, 1),
            new KeyPoint(0, 20, 1)
        };
        var searchKeyPoints = new[]
        {
            new KeyPoint(-100, -100, 1),
            new KeyPoint(200, -100, 1),
            new KeyPoint(200, 200, 1),
            new KeyPoint(-100, 200, 1)
        };
        var matches = Enumerable.Range(0, templateKeyPoints.Length)
            .Select(index => new DMatch(index, index, 0))
            .ToList();

        var result = method!.Invoke(sut, new object[]
        {
            templateKeyPoints,
            searchKeyPoints,
            matches,
            new Size(20, 20),
            new Size(30, 30),
            5.0,
            4,
            4,
            0.25
        });

        result.Should().NotBeNull();
        var tupleType = result!.GetType();
        var homography = (Mat?)tupleType.GetField("Item1")!.GetValue(result);
        var corners = (Point2f[])tupleType.GetField("Item2")!.GetValue(result)!;
        var metrics = tupleType.GetField("Item3")!.GetValue(result)!;
        var verificationPassed = (bool)metrics.GetType().GetProperty("VerificationPassed")!.GetValue(metrics)!;

        verificationPassed.Should().BeFalse();
        homography.Should().BeNull();
        corners.Should().BeEmpty();
    }
}
