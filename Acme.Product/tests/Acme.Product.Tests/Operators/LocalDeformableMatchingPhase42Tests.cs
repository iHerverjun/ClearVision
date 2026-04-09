using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Operators;

public class LocalDeformableMatchingPhase42Tests
{
    [Fact]
    public async Task ExecuteAsync_WithRepeatedTemplate_ShouldNotEmitUnsafeFallbackMatches()
    {
        var sut = new LocalDeformableMatchingOperator(Substitute.For<ILogger<LocalDeformableMatchingOperator>>());
        var op = new Operator("LocalDeformableMatching_Multi", OperatorType.LocalDeformableMatching, 0, 0);
        op.Parameters.Add(TestHelpers.CreateParameter("MinMatchScore", 0.0, "double"));
        op.Parameters.Add(TestHelpers.CreateParameter("PyramidLevels", 2, "int"));
        op.Parameters.Add(TestHelpers.CreateParameter("MaxIterations", 2, "int"));
        op.Parameters.Add(TestHelpers.CreateParameter("MaxMatches", 4, "int"));
        op.Parameters.Add(TestHelpers.CreateParameter("CandidateThreshold", 0.55, "double"));
        op.Parameters.Add(TestHelpers.CreateParameter("EnableFallback", true, "bool"));
        op.Parameters.Add(TestHelpers.CreateParameter("EnableNms", true, "bool"));
        op.Parameters.Add(TestHelpers.CreateParameter("ParallelCandidates", true, "bool"));

        using var template = CreateFeatureTemplate();
        using var scene = CreateSceneWithRepeatedTemplate(template.MatReadOnly);

        var result = await sut.ExecuteAsync(op, new Dictionary<string, object>
        {
            ["Image"] = new ImageWrapper(scene),
            ["Template"] = template
        });

        result.IsSuccess.Should().BeTrue();
        result.OutputData.Should().ContainKey("Matches");
        result.OutputData.Should().ContainKey("MatchCount");
        Convert.ToInt32(result.OutputData!["MatchCount"]).Should().Be(0);
        result.OutputData["IsMatch"].Should().Be(false);
        result.OutputData["FailureReason"].Should().NotBeNull();
    }

    private static ImageWrapper CreateFeatureTemplate()
    {
        var mat = new Mat(96, 96, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(mat, new Rect(8, 10, 28, 24), Scalar.White, -1);
        Cv2.Circle(mat, new Point(68, 28), 14, Scalar.White, -1);
        Cv2.Line(mat, new Point(10, 78), new Point(86, 52), new Scalar(180, 180, 180), 3);
        Cv2.Line(mat, new Point(20, 50), new Point(78, 86), Scalar.White, 2);
        Cv2.Rectangle(mat, new Rect(48, 58, 24, 16), new Scalar(120, 120, 120), -1);
        return new ImageWrapper(mat);
    }

    private static Mat CreateSceneWithRepeatedTemplate(Mat template)
    {
        var scene = new Mat(260, 360, MatType.CV_8UC3, Scalar.Black);
        using (var roi1 = new Mat(scene, new Rect(30, 40, template.Width, template.Height)))
        {
            template.CopyTo(roi1);
        }

        using (var roi2 = new Mat(scene, new Rect(190, 110, template.Width, template.Height)))
        {
            template.CopyTo(roi2);
        }

        return scene;
    }
}
