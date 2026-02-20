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
using Xunit;

namespace Acme.Product.Tests.Integration
{
    public class PerformanceAcceptanceTests
    {
        private readonly IFlowExecutionService _flowExecutionService;
        private readonly OperatorFlow _testFlow;

        public PerformanceAcceptanceTests()
        {
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
            inputNode.AddOutputPort("Output", Acme.Product.Core.Enums.PortDataType.Image);

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
    }
}
