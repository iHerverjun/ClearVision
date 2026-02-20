using System.Diagnostics;
using Acme.Product.Core.Cameras;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Acme.Product.Core.ValueObjects;
using Xunit;
using OpenCvSharp; // Added for MatPool and OpenCV types
using Acme.Product.Infrastructure.Memory; // Added for MatPool

namespace Acme.Product.Tests.Integration
{
    public class PerformanceAcceptanceTests
    {
        private readonly IFlowExecutionService _flowExecutionService;
        private readonly OperatorFlow _testFlow;

        public PerformanceAcceptanceTests()
        {
            // 全局共享实例 MatPool 的容量设置
            // 确保在任何测试运行前，MatPool.Shared 已经被初始化或配置
            // 这里的设置会影响整个应用程序的 MatPool 行为，包括测试和实际运行
            MatPool.Shared.Configure(maxPerBucket: 64, maxTotalGb: 4.0);

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

            flow.AddConnection(new Acme.Product.Core.ValueObjects.OperatorConnection(
                inputNode.Id,
                inputNode.OutputPorts.First().Id,
                matchNode.Id,
                matchNode.InputPorts.First().Id
            ));

            flow.AddConnection(new Acme.Product.Core.ValueObjects.OperatorConnection(
                matchNode.Id,
                matchNode.OutputPorts.First().Id,
                outputNode.Id,
                outputNode.InputPorts.First().Id
            ));

            return flow;
        }

        private static byte[] CreateTestImageBytes()
        {
            // 简单1x1 PNG
            var base64Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            return Convert.FromBase64String(base64Png);
        }

        [Fact(Timeout = 120000)] // 2分钟超时限制
        public async Task LongRunningStability_ShouldExecute1000IterationsWithoutMemoryLeak()
        {
            // Arrange
            int iterations = 1000;
            long initialMemory = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();

            var testImage = CreateTestImageBytes();
            var inputData = new Dictionary<string, object>
            {
                { "Image", testImage }
            };

            int failures = 0;

            // Act
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    await _flowExecutionService.ExecuteFlowAsync(_testFlow, inputData);
                }
                catch (Exception)
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

            // Assert
            double memoryIncreaseMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);

            // Allow 50MB inflation for test frameworks etc. 
            Assert.True(memoryIncreaseMB < 50.0, $"Memory leaked severely. Increased by {memoryIncreaseMB:F2} MB");
            Assert.True(stopwatch.ElapsedMilliseconds < 60000, $"Execution took too long: {stopwatch.ElapsedMilliseconds} ms"); // should be less than 60s
        }

        [Fact(Timeout = 300000)] // 5分钟超时限制
        public async Task DeepStability_6000x4000_WithFanOut_ShouldMaintainPoolHitRateAndP99()
        {
            // 这是真实工业场景：6000x4000 分辨率，连续循环 100 次（时间限制），包含扇出、池化效率与时延抖动要求
            int iterations = 100;
            var stopwatch = new Stopwatch();

            // 1. 构建扇出计算图（1 Acquisition -> 3 x MedianBlur -> 3 x ImageCrop）
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

                flow.AddConnection(new Acme.Product.Core.ValueObjects.OperatorConnection(inputNode.Id, inputNode.OutputPorts[0].Id, blurNode.Id, blurNode.InputPorts[0].Id));
                flow.AddConnection(new Acme.Product.Core.ValueObjects.OperatorConnection(blurNode.Id, blurNode.OutputPorts[0].Id, cropNode.Id, cropNode.InputPorts[0].Id));
            }

            // 2. 环境设置 (6000x4000 图像，模拟真实相机出图)
            using var hugeMat = new OpenCvSharp.Mat(4000, 6000, OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.Black);
            var latencies = new List<long>();

            long initialMemory = GC.GetTotalMemory(true);
            long initialHits = Acme.Product.Infrastructure.Memory.MatPool.Shared.HitCount;
            long initialMisses = Acme.Product.Infrastructure.Memory.MatPool.Shared.MissCount;

            // 3. 压测循环 (100 次，核实命中率与稳定性)
            for (int i = 0; i < iterations; i++)
            {
                // 模拟每轮新出的图像（从池中租借，模拟真实相机采样）
                using var wrapper = new ImageWrapper(Acme.Product.Infrastructure.Memory.MatPool.Shared.Rent(6000, 4000, OpenCvSharp.MatType.CV_8UC3));
                var inputData = new Dictionary<string, object> { { "Image", wrapper } };

                stopwatch.Restart();
                var result = await flowService.ExecuteFlowAsync(flow, inputData);
                stopwatch.Stop();
                latencies.Add(stopwatch.ElapsedMilliseconds);

                Assert.True(result.IsSuccess, $"Flow execution failed in iteration {i}: {result.ErrorMessage}");

                // 显式释放所有算子的输出结果中的图像，确保池化资源 100% 归还 (关键修复)
                foreach (var opRes in result.OperatorResults)
                {
                    if (opRes.OutputData != null)
                    {
                        foreach (var val in opRes.OutputData.Values)
                            (val as IDisposable)?.Dispose();
                    }
                }

                if (i % 10 == 0)
                    await Task.Yield();
            }

            long finalMemory = GC.GetTotalMemory(true);

            // Assert
            latencies.Sort();
            long p50 = latencies[latencies.Count / 2];
            long p99 = latencies[(int)(latencies.Count * 0.99)];

            // P99 抖动不应超过 P50 的 3 倍
            Assert.True(p99 <= p50 * 3, $"Latency spike detected: P50={p50}ms, P99={p99}ms (allowed <= {p50 * 3}ms)");

            // 内存增长控制在 100MB 以内 (考虑到 72MB 缓冲图片和 .NET 托管堆开销)
            double memoryIncreaseMB = (finalMemory - initialMemory) / (1024.0 * 1024.0);
            Assert.True(memoryIncreaseMB < 100.0, $"Memory leaked. Increased by {memoryIncreaseMB:F2} MB");

            // MatPool 的缓存命中率验证
            long hits = Acme.Product.Infrastructure.Memory.MatPool.Shared.HitCount - initialHits;
            long misses = Acme.Product.Infrastructure.Memory.MatPool.Shared.MissCount - initialMisses;
            double hitRate = (hits + misses == 0) ? 0 : (double)hits / (hits + misses);

            // 命中率要求 >= 90%
            Assert.True(hitRate >= 0.90, $"MatPool hit rate too low: {hitRate:P2}. Hits={hits}, Misses={misses}");
        }

        [Fact]
        public async Task ForEach_ParallelMode_ShouldExecuteInParallel()
        {
            // 通过验证 15 目标 x 50ms 串行应当 750ms，并行应当 ~50ms 来断言
            // 这里我们创建一个模拟延迟算子
            var flow = new OperatorFlow("ForEachParallelFlow");

            var foreachNode = new Operator("ForEach", Acme.Product.Core.Enums.OperatorType.ForEach, 0, 0);
            foreachNode.AddParameter(new Parameter(Guid.NewGuid(), "IoMode", "IoMode", "", "string", "Parallel"));

            // 构建带有一个延时算子的简单内部子图（用 TryCatch 作为占位，因为没有单独的 Delay 算子，或者我们创建一个 Mock）
            var innerFlow = new OperatorFlow("InnerParallel");
            var delayNode = new Operator("Delay", Acme.Product.Core.Enums.OperatorType.ResultOutput, 0, 0); // 拿一个不用参数的直接通过
            innerFlow.AddOperator(delayNode);
            foreachNode.AddParameter(new Parameter(Guid.NewGuid(), "SubGraph", "SubGraph", "", "object", innerFlow));
            flow.AddOperator(foreachNode);

            // 创建子图执行服务的 Mock
            var mockServiceProvider = Substitute.For<IServiceProvider>();
            var mockSubFlowService = Substitute.For<IFlowExecutionService>();
            mockSubFlowService.ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(async x =>
                {
                    await Task.Delay(50); // 模拟每个子图执行 50ms
                    return new FlowExecutionResult { IsSuccess = true, OutputData = new Dictionary<string, object> { { "Result", true } } };
                });

            mockServiceProvider.GetService(typeof(IFlowExecutionService)).Returns(mockSubFlowService);
            // IServiceProvider.GetRequiredService is an extension method resolving from GetService

            var foreachExec = new ForEachOperator(Substitute.For<ILogger<ForEachOperator>>(), mockServiceProvider);
            var flowService = new FlowExecutionService(new IOperatorExecutor[] { foreachExec }, Substitute.For<ILogger<FlowExecutionService>>(), Substitute.For<IVariableContext>());

            // 输入 15 个元素的集合
            var inputArray = Enumerable.Range(0, 15).Select(x => (object)x).ToList();
            var inputs = new Dictionary<string, object> { { "Items", inputArray } };

            var stopwatch = Stopwatch.StartNew();
            var result = await flowService.ExecuteFlowAsync(flow, inputs);
            stopwatch.Stop();

            Assert.True(result.IsSuccess, $"Flow execution failed: {result.ErrorMessage}");

            // 理论上 50ms 左右，考虑框架调度开销与环境波动（虚机等），应 <= 350ms
            Assert.True(stopwatch.ElapsedMilliseconds <= 350, $"Parallel execution was too slow: {stopwatch.ElapsedMilliseconds}ms for 15 elements with 50ms payload. Likely falling back to sequential.");
        }
    }
}
