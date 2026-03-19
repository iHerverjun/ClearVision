using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Tests.Services;

public class AutoTuneServiceTests
{
    [Fact]
    public async Task AutoTuneInFlowAsync_UsesTargetBreakpointAndKeepsBestParameters()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var metricsAnalyzer = Substitute.For<IPreviewMetricsAnalyzer>();
        var service = new AutoTuneService(
            NullLogger<AutoTuneService>.Instance,
            flowExecution,
            metricsAnalyzer);

        var flow = new OperatorFlow("AutoTuneFlow");
        var targetOperator = new Operator("Threshold", OperatorType.Thresholding, 0, 0);
        flow.AddOperator(targetOperator);

        var seenOptions = new List<DebugOptions>();
        var allocatedImages = new List<Mat>();
        var metricsCallCount = 0;

        flowExecution.ExecuteFlowDebugAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<DebugOptions>(),
                Arg.Any<Dictionary<string, object>?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                seenOptions.Add(callInfo.ArgAt<DebugOptions>(1));

                var image = new Mat(6, 6, MatType.CV_8UC1, Scalar.All(255));
                allocatedImages.Add(image);

                return Task.FromResult(new FlowDebugExecutionResult
                {
                    IsSuccess = true,
                    IntermediateResults = new Dictionary<Guid, Dictionary<string, object>>
                    {
                        [targetOperator.Id] = new()
                        {
                            ["Image"] = image
                        }
                    }
                });
            });

        metricsAnalyzer.Analyze(Arg.Any<Mat>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<AutoTuneGoal?>())
            .Returns(_ =>
            {
                metricsCallCount++;
                return metricsCallCount == 1
                    ? CreateMetrics(currentBlobCount: 10)
                    : CreateMetrics(currentBlobCount: 5);
            });

        try
        {
            var result = await service.AutoTuneInFlowAsync(
                flow,
                targetOperator.Id,
                CreateInputImage(),
                new Dictionary<string, object> { ["Threshold"] = 100 },
                new AutoTuneGoal
                {
                    TargetBlobCount = 5,
                    Tolerance = 0.1
                },
                maxIterations: 3,
                ct: CancellationToken.None);

            result.Success.Should().BeTrue();
            result.IsGoalAchieved.Should().BeTrue();
            result.TotalIterations.Should().Be(2);
            Convert.ToInt32(result.FinalParameters["Threshold"]).Should().Be(178);
            seenOptions.Should().HaveCount(2);
            seenOptions.Should().OnlyContain(options =>
                options.BreakAtOperatorId == targetOperator.Id &&
                options.EnableIntermediateCache);
        }
        finally
        {
            foreach (var image in allocatedImages)
            {
                image.Dispose();
            }
        }
    }

    private static PreviewMetrics CreateMetrics(int currentBlobCount)
    {
        return new PreviewMetrics
        {
            Goals = new OptimizationGoals
            {
                CurrentBlobCount = currentBlobCount,
                TargetBlobCount = 5,
                NoisePenalty = 0,
                FragmentPenalty = 0,
                AreaDistributionScore = 0.5,
                ShapeRegularityScore = 0.5
            }
        };
    }

    private static byte[] CreateInputImage()
    {
        using var image = new Mat(6, 6, MatType.CV_8UC3, Scalar.All(255));
        Cv2.ImEncode(".png", image, out var encoded);
        return encoded;
    }
}
