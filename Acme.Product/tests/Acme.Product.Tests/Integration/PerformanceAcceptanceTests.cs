using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Memory;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenCvSharp;
using Xunit;

namespace Acme.Product.Tests.Integration
{
    [Collection(PerformanceAcceptanceCollection.Name)]
    public class PerformanceAcceptanceTests
    {
        private readonly IFlowExecutionService _flowExecutionService;
        private readonly OperatorFlow _testFlow;

        public PerformanceAcceptanceTests()
        {
            // Keep performance tests isolated from prior pool state.
            MatPool.Shared.Configure(maxPerBucket: 128, maxTotalGb: 8.0);
            MatPool.Shared.Trim();

            var logger = Substitute.For<ILogger<FlowExecutionService>>();

            var executors = new List<IOperatorExecutor>
            {
                new ImageAcquisitionOperator(
                    Substitute.For<ILogger<ImageAcquisitionOperator>>(),
                    Substitute.For<ICameraManager>()),
                new TemplateMatchOperator(Substitute.For<ILogger<TemplateMatchOperator>>()),
                new ResultOutputOperator(Substitute.For<ILogger<ResultOutputOperator>>())
            };

            _flowExecutionService = new FlowExecutionService(
                executors,
                logger,
                Substitute.For<IVariableContext>());

            _testFlow = CreateComplexFlow();
        }

        private OperatorFlow CreateComplexFlow()
        {
            var flow = new OperatorFlow("TestFlow");

            var inputNode = new Operator("ImageAcquisition", Acme.Product.Core.Enums.OperatorType.ImageAcquisition, 0, 0);
            inputNode.AddOutputPort("Image", Acme.Product.Core.Enums.PortDataType.Image);

            var matchNode = new Operator("TemplateMatch", Acme.Product.Core.Enums.OperatorType.TemplateMatching, 200, 0);
            matchNode.AddInputPort("Input", Acme.Product.Core.Enums.PortDataType.Image, true);
            matchNode.AddOutputPort("MatchScore", Acme.Product.Core.Enums.PortDataType.Float);

            var outputNode = new Operator("ResultOutput", Acme.Product.Core.Enums.OperatorType.ResultOutput, 400, 0);
            outputNode.AddInputPort("Input1", Acme.Product.Core.Enums.PortDataType.Any, true);

            flow.AddOperator(inputNode);
            flow.AddOperator(matchNode);
            flow.AddOperator(outputNode);

            flow.AddConnection(new OperatorConnection(
                inputNode.Id,
                inputNode.OutputPorts.First().Id,
                matchNode.Id,
                matchNode.InputPorts.First().Id
            ));

            flow.AddConnection(new OperatorConnection(
                matchNode.Id,
                matchNode.OutputPorts.First().Id,
                outputNode.Id,
                outputNode.InputPorts.First().Id
            ));

            return flow;
        }

        private static byte[] CreateTestImageBytes()
        {
            // Simple 1x1 PNG
            var base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            return Convert.FromBase64String(base64Png);
        }

        [Fact(Timeout = 120000)]
        public async Task LongRunningStability_ShouldExecute1000IterationsWithoutMemoryLeak()
        {
            int iterations = 1000;
            long initialMemory = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();

            var testImage = CreateTestImageBytes();
            var inputData = new Dictionary<string, object>
            {
                { "Image", testImage }
            };

            int failures = 0;

            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    await _flowExecutionService.ExecuteFlowAsync(_testFlow, inputData);
                }
                catch
                {
                    failures++;
                }

                if (i % 100 == 0)
                {
                    await Task.Yield();
                }
            }

            stopwatch.Stop();
            long finalMemory = GC.GetTotalMemory(true);

            double memoryIncreaseMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);

            Assert.True(memoryIncreaseMB < 50.0, $"Memory leaked severely. Increased by {memoryIncreaseMB:F2} MB");
            Assert.True(stopwatch.ElapsedMilliseconds < 60000, $"Execution took too long: {stopwatch.ElapsedMilliseconds} ms");
        }

        [Fact(Timeout = 300000)]
        public async Task DeepStability_6000x4000_WithFanOut_ShouldMaintainPoolHitRateAndP99()
        {
            int warmupIterations = GetEnvInt("CV_PERF_WARMUP_ITERS", 12, 0, 100);
            int measuredIterations = GetEnvInt("CV_PERF_MEASURE_ITERS", 80, 20, 200);
            double maxP99ToP50Ratio = GetEnvDouble("CV_PERF_MAX_P99_P50_RATIO", 4.0, 1.0, 20.0);
            double minHitRate = GetEnvDouble("CV_PERF_MIN_HIT_RATE", 0.55, 0.0, 1.0);
            double maxMemoryIncreaseMb = GetEnvDouble("CV_PERF_MAX_MEMORY_MB", 150.0, 32.0, 1024.0);

            MatPool.Shared.Configure(maxPerBucket: 128, maxTotalGb: 8.0);
            MatPool.Shared.Trim();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            var stopwatch = new Stopwatch();

            var flow = new OperatorFlow("FanOutProcessFlow");
            var inputNode = new Operator("ImageAcquisition", Acme.Product.Core.Enums.OperatorType.ImageAcquisition, 0, 0);
            inputNode.AddOutputPort("Image", Acme.Product.Core.Enums.PortDataType.Image);
            flow.AddOperator(inputNode);

            var executors = new List<IOperatorExecutor>
            {
                new ImageAcquisitionOperator(Substitute.For<ILogger<ImageAcquisitionOperator>>(), Substitute.For<ICameraManager>()),
                new MedianBlurOperator(Substitute.For<ILogger<MedianBlurOperator>>()),
                new ImageCropOperator(Substitute.For<ILogger<ImageCropOperator>>())
            };
            var flowService = new FlowExecutionService(executors, Substitute.For<ILogger<FlowExecutionService>>(), Substitute.For<IVariableContext>());

            for (int i = 0; i < 3; i++)
            {
                var blurNode = new Operator($"Blur_{i}", Acme.Product.Core.Enums.OperatorType.MedianBlur, 200, i * 150);
                blurNode.AddInputPort("Image", Acme.Product.Core.Enums.PortDataType.Image, true);
                blurNode.AddOutputPort("OutputImage", Acme.Product.Core.Enums.PortDataType.Image);
                blurNode.AddParameter(new Parameter(Guid.NewGuid(), "KernelSize", "KernelSize", "", "int", 3));
                flow.AddOperator(blurNode);

                var cropNode = new Operator($"Crop_{i}", Acme.Product.Core.Enums.OperatorType.ImageCrop, 400, i * 150);
                cropNode.AddInputPort("Image", Acme.Product.Core.Enums.PortDataType.Image, true);
                cropNode.AddOutputPort("OutputImage", Acme.Product.Core.Enums.PortDataType.Image);
                cropNode.AddParameter(new Parameter(Guid.NewGuid(), "X", "X", "", "int", 0));
                cropNode.AddParameter(new Parameter(Guid.NewGuid(), "Y", "Y", "", "int", 0));
                cropNode.AddParameter(new Parameter(Guid.NewGuid(), "Width", "Width", "", "int", 1000));
                cropNode.AddParameter(new Parameter(Guid.NewGuid(), "Height", "Height", "", "int", 1000));
                flow.AddOperator(cropNode);

                flow.AddConnection(new OperatorConnection(inputNode.Id, inputNode.OutputPorts[0].Id, blurNode.Id, blurNode.InputPorts[0].Id));
                flow.AddConnection(new OperatorConnection(blurNode.Id, blurNode.OutputPorts[0].Id, cropNode.Id, cropNode.InputPorts[0].Id));
            }

            for (int i = 0; i < warmupIterations; i++)
            {
                using var warmupWrapper = new ImageWrapper(MatPool.Shared.Rent(6000, 4000, MatType.CV_8UC3));
                var warmupInput = new Dictionary<string, object> { { "Image", warmupWrapper } };
                var warmupResult = await flowService.ExecuteFlowAsync(flow, warmupInput);
                Assert.True(warmupResult.IsSuccess, $"Warmup flow failed in iteration {i}: {warmupResult.ErrorMessage}");
                DisposeFlowResult(warmupResult);
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            var latencies = new List<long>(measuredIterations);
            long initialMemory = GC.GetTotalMemory(true);
            long initialHits = MatPool.Shared.HitCount;
            long initialMisses = MatPool.Shared.MissCount;

            for (int i = 0; i < measuredIterations; i++)
            {
                using var wrapper = new ImageWrapper(MatPool.Shared.Rent(6000, 4000, MatType.CV_8UC3));
                var inputData = new Dictionary<string, object> { { "Image", wrapper } };

                stopwatch.Restart();
                var result = await flowService.ExecuteFlowAsync(flow, inputData);
                stopwatch.Stop();
                latencies.Add(stopwatch.ElapsedMilliseconds);

                Assert.True(result.IsSuccess, $"Flow execution failed in iteration {i}: {result.ErrorMessage}");
                DisposeFlowResult(result);

                if (i % 10 == 0)
                {
                    await Task.Yield();
                }
            }

            long finalMemory = GC.GetTotalMemory(true);

            latencies.Sort();
            long p50 = latencies[latencies.Count / 2];
            int p99Index = Math.Clamp((int)Math.Ceiling(latencies.Count * 0.99) - 1, 0, latencies.Count - 1);
            long p99 = latencies[p99Index];

            Assert.True(
                p99 <= p50 * maxP99ToP50Ratio,
                $"Latency spike detected: P50={p50}ms, P99={p99}ms (allowed <= {p50 * maxP99ToP50Ratio:F2}ms)");

            double memoryIncreaseMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            Assert.True(
                memoryIncreaseMB < maxMemoryIncreaseMb,
                $"Memory leaked. Increased by {memoryIncreaseMB:F2} MB (allowed < {maxMemoryIncreaseMb:F2} MB)");

            long hits = MatPool.Shared.HitCount - initialHits;
            long misses = MatPool.Shared.MissCount - initialMisses;
            double hitRate = (hits + misses == 0) ? 0 : (double)hits / (hits + misses);

            Assert.True(
                hitRate >= minHitRate,
                $"MatPool hit rate too low: {hitRate:P2}. Hits={hits}, Misses={misses}, Required>={minHitRate:P2}, Warmup={warmupIterations}, Measured={measuredIterations}");
        }

        [Fact]
        public async Task ForEach_ParallelMode_ShouldExecuteInParallel()
        {
            var flow = new OperatorFlow("ForEachParallelFlow");

            var foreachNode = new Operator("ForEach", Acme.Product.Core.Enums.OperatorType.ForEach, 0, 0);
            foreachNode.AddParameter(new Parameter(Guid.NewGuid(), "IoMode", "IoMode", "", "string", "Parallel"));

            var innerFlow = new OperatorFlow("InnerParallel");
            var delayNode = new Operator("Delay", Acme.Product.Core.Enums.OperatorType.ResultOutput, 0, 0);
            innerFlow.AddOperator(delayNode);
            foreachNode.AddParameter(new Parameter(Guid.NewGuid(), "SubGraph", "SubGraph", "", "object", innerFlow));
            flow.AddOperator(foreachNode);

            var mockServiceProvider = Substitute.For<IServiceProvider>();
            var mockSubFlowService = Substitute.For<IFlowExecutionService>();
            mockSubFlowService.ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    await Task.Delay(50);
                    return new FlowExecutionResult
                    {
                        IsSuccess = true,
                        OutputData = new Dictionary<string, object> { { "Result", true } }
                    };
                });

            mockServiceProvider.GetService(typeof(IFlowExecutionService)).Returns(mockSubFlowService);

            var foreachExec = new ForEachOperator(Substitute.For<ILogger<ForEachOperator>>(), mockServiceProvider);
            var flowService = new FlowExecutionService(new IOperatorExecutor[] { foreachExec }, Substitute.For<ILogger<FlowExecutionService>>(), Substitute.For<IVariableContext>());

            var inputArray = Enumerable.Range(0, 15).Select(x => (object)x).ToList();
            var inputs = new Dictionary<string, object> { { "Items", inputArray } };

            var stopwatch = Stopwatch.StartNew();
            var result = await flowService.ExecuteFlowAsync(flow, inputs);
            stopwatch.Stop();

            Assert.True(result.IsSuccess, $"Flow execution failed: {result.ErrorMessage}");
            Assert.True(stopwatch.ElapsedMilliseconds <= 350, $"Parallel execution was too slow: {stopwatch.ElapsedMilliseconds}ms for 15 elements with 50ms payload. Likely falling back to sequential.");
        }

        private static int GetEnvInt(string name, int defaultValue, int min, int max)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return defaultValue;
            }

            return Math.Clamp(parsed, min, max);
        }

        private static double GetEnvDouble(string name, double defaultValue, double min, double max)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (!double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return defaultValue;
            }

            return Math.Clamp(parsed, min, max);
        }

        private static void DisposeFlowResult(FlowExecutionResult result)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            DisposeObjectGraph(result.OutputData, visited);

            foreach (var opRes in result.OperatorResults)
            {
                DisposeObjectGraph(opRes.OutputData, visited);
            }
        }

        private static void DisposeObjectGraph(object? value, HashSet<object> visited)
        {
            if (value == null)
            {
                return;
            }

            if (value is not ValueType && value is not string && !visited.Add(value))
            {
                return;
            }

            if (value is IDisposable disposable)
            {
                disposable.Dispose();
                return;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    DisposeObjectGraph(entry.Value, visited);
                }
                return;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                foreach (var item in enumerable)
                {
                    DisposeObjectGraph(item, visited);
                }
            }
        }
    }
}
