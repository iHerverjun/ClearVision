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

    [Fact]
    public async Task ExecuteSingleAsync_WhenJudgmentSignalMissing_ShouldFailClosed()
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

        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowExecutionResult
            {
                IsSuccess = true,
                ExecutionTimeMs = 12,
                OutputData = new Dictionary<string, object>()
            }));
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

        var result = await service.ExecuteSingleAsync(projectId, new byte[] { 1, 2, 3 }, explicitFlow);

        result.Status.Should().Be(InspectionStatus.Error);
        result.ErrorMessage.Should().Be("MissingJudgmentSignal");

        using var doc = JsonDocument.Parse(result.OutputDataJson ?? "{}");
        doc.RootElement.TryGetProperty("MissingJudgmentSignal", out var missingSignal).Should().BeTrue();
        missingSignal.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteSingleAsync_WhenJudgmentFieldTypeInvalid_ShouldReturnError()
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

        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowExecutionResult
            {
                IsSuccess = true,
                ExecutionTimeMs = 9,
                OutputData = new Dictionary<string, object> { ["IsOk"] = "true" }
            }));
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

        var result = await service.ExecuteSingleAsync(projectId, new byte[] { 9, 9, 9 }, explicitFlow);

        result.Status.Should().Be(InspectionStatus.Error);
        result.ErrorMessage.Should().Contain("InvalidJudgmentType:IsOk");
    }

    [Fact]
    public async Task ExecuteSingleAsync_WhenWrappedResultContainsIsMatch_ShouldTreatAsOk()
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

        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowExecutionResult
            {
                IsSuccess = true,
                ExecutionTimeMs = 15,
                OutputData = new Dictionary<string, object>
                {
                    ["Result"] = new Dictionary<string, object>
                    {
                        ["IsMatch"] = true,
                        ["Message"] = "Sequence matched."
                    }
                }
            }));
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

        var result = await service.ExecuteSingleAsync(projectId, new byte[] { 7, 8, 9 }, explicitFlow);

        result.Status.Should().Be(InspectionStatus.OK);
        result.ErrorMessage.Should().BeNull();

        using var doc = JsonDocument.Parse(result.OutputDataJson ?? "{}");
        doc.RootElement.GetProperty("JudgmentSource").GetString().Should().Be("Result.IsMatch");
        doc.RootElement.GetProperty("MissingJudgmentSignal").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSingleAsync_WhenDataWrapperContainsIsAnomaly_ShouldTreatAsNg()
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

        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowExecutionResult
            {
                IsSuccess = true,
                ExecutionTimeMs = 16,
                OutputData = new Dictionary<string, object>
                {
                    ["Result"] = "informational-text",
                    ["Data"] = new Dictionary<string, object>
                    {
                        ["IsAnomaly"] = true
                    }
                }
            }));
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

        var result = await service.ExecuteSingleAsync(projectId, new byte[] { 1, 3, 5 }, explicitFlow);

        result.Status.Should().Be(InspectionStatus.NG);

        using var doc = JsonDocument.Parse(result.OutputDataJson ?? "{}");
        doc.RootElement.GetProperty("JudgmentSource").GetString().Should().Be("Data.IsAnomaly");
        doc.RootElement.GetProperty("MissingJudgmentSignal").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteSingleAsync_WhenFlowExecutionThrows_ShouldReturnPersistedErrorResult()
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

        flowExecution
            .ExecuteFlowAsync(Arg.Any<OperatorFlow>(), Arg.Any<Dictionary<string, object>?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<FlowExecutionResult>(new InvalidOperationException("flow exploded")));
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

        var result = await service.ExecuteSingleAsync(projectId, new byte[] { 2, 4, 6 }, explicitFlow);

        result.Status.Should().Be(InspectionStatus.Error);
        result.ErrorMessage.Should().Be("flow exploded");
        await resultRepository.Received(1).AddAsync(Arg.Is<InspectionResult>(item =>
            item.Status == InspectionStatus.Error &&
            item.ErrorMessage == "flow exploded"));
    }

    [Fact]
    public async Task ExecuteSingleAsync_WhenCameraAcquisitionThrows_ShouldReturnPersistedErrorResult()
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
        var explicitFlow = CreateFlow("camera-flow");

        imageAcquisition
            .AcquireFromCameraAsync("camera-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ImageDto>(new InvalidOperationException("camera offline")));
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

        var result = await service.ExecuteSingleAsync(projectId, "camera-1", explicitFlow);

        result.Status.Should().Be(InspectionStatus.Error);
        result.ErrorMessage.Should().Contain("camera offline");
        await resultRepository.Received(1).AddAsync(Arg.Is<InspectionResult>(item =>
            item.Status == InspectionStatus.Error &&
            item.ErrorMessage != null &&
            item.ErrorMessage.Contains("camera offline", StringComparison.Ordinal)));
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
