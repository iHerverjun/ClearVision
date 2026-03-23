using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenCvSharp;
using System.Text.Json;
using DetectionResultValue = Acme.Product.Core.ValueObjects.DetectionResult;

namespace Acme.Product.Tests.Services;

public class AutoTuneServiceTests
{
    [Fact]
    public async Task AutoTuneInFlowAsync_UsesTargetBreakpointAndKeepsBestParameters()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var metricsAnalyzer = Substitute.For<IPreviewMetricsAnalyzer>();
        var flowNodePreviewService = Substitute.For<IFlowNodePreviewService>();
        var service = new AutoTuneService(
            NullLogger<AutoTuneService>.Instance,
            flowExecution,
            metricsAnalyzer,
            flowNodePreviewService);

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
            OverallScore = currentBlobCount == 5 ? 0.9 : 0.3,
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

    [Fact]
    public async Task AutoTuneScenarioAsync_ShouldOnlyTuneBoxNmsThresholds()
    {
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var metricsAnalyzer = Substitute.For<IPreviewMetricsAnalyzer>();
        var flowNodePreviewService = Substitute.For<IFlowNodePreviewService>();
        var service = new AutoTuneService(
            NullLogger<AutoTuneService>.Instance,
            flowExecution,
            metricsAnalyzer,
            flowNodePreviewService);

        var flow = new OperatorFlow("WireSequenceFlow");
        var boxNms = new Operator("BoxNms", OperatorType.BoxNms, 0, 0);
        boxNms.AddParameter(new Parameter(Guid.NewGuid(), "ScoreThreshold", "ScoreThreshold", string.Empty, "double", 0.25d));
        boxNms.AddParameter(new Parameter(Guid.NewGuid(), "IouThreshold", "IouThreshold", string.Empty, "double", 0.45d));
        var judge = new Operator("Judge", OperatorType.DetectionSequenceJudge, 0, 0);

        flow.AddOperator(boxNms);
        flow.AddOperator(judge);
        flow.Connections.Add(new OperatorConnection(boxNms.Id, Guid.NewGuid(), judge.Id, Guid.NewGuid()));

        var seenThresholds = new List<(double Score, double Iou)>();
        var previewCall = 0;
        flowNodePreviewService.PreviewWithMetricsAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<Guid>(),
                Arg.Any<byte[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                previewCall++;
                var callFlow = callInfo.ArgAt<OperatorFlow>(0);
                var callBoxNms = callFlow.Operators.Single(item => item.Type == OperatorType.BoxNms);
                seenThresholds.Add((
                    ReadDoubleParam(callBoxNms, "ScoreThreshold"),
                    ReadDoubleParam(callBoxNms, "IouThreshold")));

                return Task.FromResult(previewCall == 1
                    ? new FlowNodePreviewWithMetricsResult
                    {
                        Success = true,
                        TargetNodeId = judge.Id,
                        Outputs = new Dictionary<string, object>
                        {
                            ["DetectionList"] = new DetectionList(new[]
                            {
                                new DetectionResultValue("Wire_Brown", 0.52f, 10f, 10f, 8f, 8f),
                                new DetectionResultValue("Wire_Brown", 0.48f, 12f, 10f, 8f, 8f)
                            }),
                            ["ExpectedLabels"] = new[] { "Wire_Brown", "Wire_Black", "Wire_Blue" },
                            ["ExpectedCount"] = 3,
                            ["RequiredMinConfidence"] = 0.6d,
                            ["IsMatch"] = false
                        },
                        Metrics = new PreviewMetrics
                        {
                            OverallScore = 0.25,
                            Diagnostics =
                            [
                                PreviewDiagnosticTags.DuplicateDetectedClass,
                                PreviewDiagnosticTags.DetectionCountMismatch,
                                PreviewDiagnosticTags.LowDetectionConfidence
                            ]
                        },
                        DiagnosticCodes =
                        [
                            "duplicate_detected_class",
                            "detection_count_mismatch",
                            "low_detection_confidence"
                        ]
                    }
                    : new FlowNodePreviewWithMetricsResult
                    {
                        Success = true,
                        TargetNodeId = judge.Id,
                        Outputs = new Dictionary<string, object>
                        {
                            ["DetectionList"] = new DetectionList(new[]
                            {
                                new DetectionResultValue("Wire_Brown", 0.92f, 10f, 10f, 8f, 8f),
                                new DetectionResultValue("Wire_Black", 0.90f, 20f, 10f, 8f, 8f),
                                new DetectionResultValue("Wire_Blue", 0.89f, 30f, 10f, 8f, 8f)
                            }),
                            ["ExpectedLabels"] = new[] { "Wire_Brown", "Wire_Black", "Wire_Blue" },
                            ["ExpectedCount"] = 3,
                            ["RequiredMinConfidence"] = 0.6d,
                            ["ActualOrder"] = new[] { "Wire_Brown", "Wire_Black", "Wire_Blue" },
                            ["IsMatch"] = true
                        },
                        Metrics = new PreviewMetrics
                        {
                            OverallScore = 0.92,
                            Diagnostics = new List<string>()
                        },
                        DiagnosticCodes = new List<string>()
                    });
            });

        var result = await service.AutoTuneScenarioAsync(
            "wire-sequence-terminal",
            flow,
            CreateInputImage(),
            new AutoTuneGoal(),
            maxIterations: 5,
            ct: CancellationToken.None);

        result.Success.Should().BeTrue();
        result.IsGoalAchieved.Should().BeTrue();
        result.TotalIterations.Should().Be(2);
        result.FinalParameters.Keys.Should().BeEquivalentTo("BoxNms.ScoreThreshold", "BoxNms.IouThreshold");
        Convert.ToDouble(result.FinalParameters["BoxNms.ScoreThreshold"]).Should().BeApproximately(0.2d, 0.0001d);
        Convert.ToDouble(result.FinalParameters["BoxNms.IouThreshold"]).Should().BeApproximately(0.4d, 0.0001d);
        seenThresholds.Should().HaveCount(2);
        seenThresholds[0].Should().Be((0.25d, 0.45d));
        seenThresholds[1].Should().Be((0.2d, 0.4d));
    }

    private static double ReadDoubleParam(Operator @operator, string name)
    {
        return @operator.Parameters.Single(item => item.Name == name).GetValue() switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            decimal decimalValue => (double)decimalValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetDouble(),
            string stringValue => double.Parse(stringValue),
            _ => throw new InvalidOperationException($"Unexpected parameter value for {name}")
        };
    }
}
