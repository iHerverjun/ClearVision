using System.Text.Json;
using Acme.Product.Application.Analysis;
using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acme.Product.Tests.Services;

public class InspectionServiceSingleRunTests
{
    [Fact]
    public async Task ExecuteSingleAsync_WithExplicitFlow_ShouldPreferClientFlow()
    {
        var projectId = Guid.NewGuid();
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var resultRepository = Substitute.For<IInspectionResultRepository>();
        var projectRepository = Substitute.For<IProjectRepository>();
        var imageAcquisition = Substitute.For<IImageAcquisitionService>();
        var configurationService = Substitute.For<IConfigurationService>();
        var coordinator = Substitute.For<IInspectionRuntimeCoordinator>();
        var worker = Substitute.For<IInspectionWorker>();
        var flowStorage = Substitute.For<IProjectFlowStorage>();
        var explicitFlow = CreateFlow("client-flow");
        OperatorFlow? executedFlow = null;
        Dictionary<string, object>? executedInputs = null;

        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                executedFlow = callInfo.Arg<OperatorFlow>();
                executedInputs = callInfo.ArgAt<Dictionary<string, object>?>(1);
                return Task.FromResult(new FlowExecutionResult
                {
                    IsSuccess = true,
                    ExecutionTimeMs = 12,
                    OutputData = new Dictionary<string, object> { ["JudgmentResult"] = "OK" }
                });
            });
        resultRepository
            .AddAsync(Arg.Any<InspectionResult>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<InspectionResult>()));

        var service = new InspectionService(
            resultRepository,
            projectRepository,
            flowExecution,
            imageAcquisition,
            configurationService,
            coordinator,
            worker,
            Substitute.For<IImageCacheRepository>(),
            new AnalysisDataBuilder(),
            flowStorage,
            NullLogger<InspectionService>.Instance);

        await service.ExecuteSingleAsync(projectId, new byte[] { 1, 2, 3 }, explicitFlow);

        executedFlow.Should().NotBeNull();
        executedFlow!.Operators.Should().ContainSingle(operatorEntity => operatorEntity.Name == "client-flow");
        executedInputs.Should().NotBeNull();
        executedInputs!.Should().ContainKey("Image");
        _ = projectRepository.DidNotReceive().GetWithFlowAsync(Arg.Any<Guid>());
        _ = flowStorage.DidNotReceive().LoadFlowJsonAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ExecuteSingleAsync_WhenDatabaseFlowIsEmpty_ShouldFallbackToFileFlow()
    {
        var projectId = Guid.NewGuid();
        var flowExecution = Substitute.For<IFlowExecutionService>();
        var resultRepository = Substitute.For<IInspectionResultRepository>();
        var projectRepository = Substitute.For<IProjectRepository>();
        var imageAcquisition = Substitute.For<IImageAcquisitionService>();
        var configurationService = Substitute.For<IConfigurationService>();
        var coordinator = Substitute.For<IInspectionRuntimeCoordinator>();
        var worker = Substitute.For<IInspectionWorker>();
        var flowStorage = Substitute.For<IProjectFlowStorage>();
        var project = new Project("fallback-project");
        var fileFlowJson = SerializeFlowDto("file-flow");
        OperatorFlow? executedFlow = null;

        projectRepository.GetWithFlowAsync(projectId).Returns(project);
        flowStorage.LoadFlowJsonAsync(projectId).Returns(fileFlowJson);
        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                executedFlow = callInfo.Arg<OperatorFlow>();
                return Task.FromResult(new FlowExecutionResult
                {
                    IsSuccess = true,
                    ExecutionTimeMs = 9,
                    OutputData = new Dictionary<string, object> { ["JudgmentResult"] = "OK" }
                });
            });
        resultRepository
            .AddAsync(Arg.Any<InspectionResult>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<InspectionResult>()));

        var service = new InspectionService(
            resultRepository,
            projectRepository,
            flowExecution,
            imageAcquisition,
            configurationService,
            coordinator,
            worker,
            Substitute.For<IImageCacheRepository>(),
            new AnalysisDataBuilder(),
            flowStorage,
            NullLogger<InspectionService>.Instance);

        await service.ExecuteSingleAsync(projectId, new byte[] { 9, 9, 9 }, flow: null);

        executedFlow.Should().NotBeNull();
        executedFlow!.Operators.Should().ContainSingle(operatorEntity => operatorEntity.Name == "file-flow");
        _ = flowStorage.Received(1).LoadFlowJsonAsync(projectId);
    }

    private static OperatorFlow CreateFlow(string operatorName)
    {
        var flow = new OperatorFlow("test-flow");
        flow.AddOperator(new Operator(Guid.NewGuid(), operatorName, OperatorType.ResultOutput, 0, 0));
        return flow;
    }

    private static string SerializeFlowDto(string operatorName)
    {
        var dto = new OperatorFlowDto
        {
            Name = "stored-flow",
            Operators = new List<OperatorDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = operatorName,
                    Type = OperatorType.ResultOutput,
                    X = 0,
                    Y = 0
                }
            }
        };

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        });
    }
}
