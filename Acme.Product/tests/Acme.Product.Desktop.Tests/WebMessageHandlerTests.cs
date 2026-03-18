using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Contracts.Messages;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Desktop.Handlers;
using Acme.Product.Infrastructure.Events;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class WebMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdateFlowCommand_ShouldPersistThroughProjectService()
    {
        var operatorFactory = new OperatorFactory();
        var projectRepository = Substitute.For<IProjectRepository>();
        var flowStorage = Substitute.For<IProjectFlowStorage>();
        var project = new Project("WebMessage Flow");

        projectRepository.GetByIdAsync(project.Id).Returns(Task.FromResult<Project?>(project));

        await using var serviceProvider = BuildServiceProvider(services =>
        {
            services.AddSingleton(projectRepository);
            services.AddSingleton(flowStorage);
            services.AddSingleton<IOperatorFactory>(operatorFactory);
            services.AddScoped<ProjectService>();
        });

        var handler = CreateHandler(serviceProvider, operatorFactory);
        var payload = JsonSerializer.Serialize(new UpdateFlowCommand
        {
            ProjectId = project.Id,
            Flow = new FlowData
            {
                Operators =
                [
                    new OperatorData
                    {
                        Id = Guid.NewGuid(),
                        Name = "ResultOutput",
                        Type = nameof(OperatorType.ResultOutput),
                        X = 120,
                        Y = 80
                    }
                ],
                Connections = []
            }
        });

        var response = await handler.HandleAsync(new WebMessage
        {
            Type = nameof(UpdateFlowCommand),
            Id = "req-update-flow",
            Payload = payload
        });

        response.Success.Should().BeTrue();
        await flowStorage.Received(1).SaveFlowJsonAsync(
            project.Id,
            Arg.Is<string>(json => json.Contains("ResultOutput") && json.Contains("MainFlow")));
    }

    [Fact]
    public async Task HandleAsync_ExecuteOperatorCommand_ShouldResolveOperatorFromStoredFlow()
    {
        var operatorFactory = new OperatorFactory();
        var projectRepository = Substitute.For<IProjectRepository>();
        var flowStorage = Substitute.For<IProjectFlowStorage>();
        var flowExecutionService = Substitute.For<IFlowExecutionService>();
        var project = new Project("Stored Flow");
        var operatorId = Guid.NewGuid();
        var operatorDto = CreateOperatorDto(operatorFactory, OperatorType.ResultOutput, "ResultOutput", operatorId);
        var flowDto = new OperatorFlowDto
        {
            Id = Guid.NewGuid(),
            Name = "MainFlow",
            Operators = [operatorDto],
            Connections = []
        };

        projectRepository.GetAllAsync().Returns(Task.FromResult<IEnumerable<Project>>(new[] { project }));
        projectRepository.GetByIdAsync(project.Id).Returns(Task.FromResult<Project?>(project));
        flowStorage.LoadFlowJsonAsync(project.Id).Returns(Task.FromResult<string?>(JsonSerializer.Serialize(flowDto)));
        flowExecutionService.ExecuteOperatorAsync(Arg.Any<Operator>(), Arg.Any<Dictionary<string, object>?>())
            .Returns(Task.FromResult(new OperatorExecutionResult
            {
                OperatorId = operatorId,
                OperatorName = "ResultOutput",
                IsSuccess = true,
                OutputData = new Dictionary<string, object> { ["Status"] = "OK" },
                ExecutionTimeMs = 8
            }));

        await using var serviceProvider = BuildServiceProvider(services =>
        {
            services.AddSingleton(projectRepository);
            services.AddSingleton(flowStorage);
            services.AddSingleton(flowExecutionService);
            services.AddSingleton<IOperatorFactory>(operatorFactory);
            services.AddScoped<ProjectService>();
        });

        var handler = CreateHandler(serviceProvider, operatorFactory);
        var payload = JsonSerializer.Serialize(new ExecuteOperatorCommand
        {
            OperatorId = operatorId,
            Inputs = new Dictionary<string, object> { ["Value"] = 42 }
        });

        var response = await handler.HandleAsync(new WebMessage
        {
            Type = nameof(ExecuteOperatorCommand),
            Id = "req-execute-operator",
            Payload = payload
        });

        response.Success.Should().BeTrue();
        await flowExecutionService.Received(1).ExecuteOperatorAsync(
            Arg.Is<Operator>(op => op.Id == operatorId && op.Type == OperatorType.ResultOutput),
            Arg.Any<Dictionary<string, object>?>());
    }

    [Fact]
    public async Task HandleAsync_StopInspectionCommand_ShouldReturnExplicitFailure()
    {
        var operatorFactory = new OperatorFactory();

        await using var serviceProvider = BuildServiceProvider(_ => { });
        var handler = CreateHandler(serviceProvider, operatorFactory);

        var response = await handler.HandleAsync(new WebMessage
        {
            Type = nameof(StopInspectionCommand),
            Id = "req-stop-inspection",
            Payload = "{}"
        });

        response.Success.Should().BeFalse();
        response.Error.Should().Contain("/api/inspection/realtime/stop");
    }

    private static WebMessageHandler CreateHandler(ServiceProvider serviceProvider, OperatorFactory operatorFactory)
    {
        var eventStore = new InMemoryEventStore(NullLogger<InMemoryEventStore>.Instance);
        var eventBus = new InMemoryInspectionEventBus(NullLogger<InMemoryInspectionEventBus>.Instance, eventStore);

        return new WebMessageHandler(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            operatorFactory,
            eventBus,
            NullLogger<WebMessageHandler>.Instance);
    }

    private static ServiceProvider BuildServiceProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        return services.BuildServiceProvider();
    }

    private static OperatorDto CreateOperatorDto(
        OperatorFactory operatorFactory,
        OperatorType operatorType,
        string name,
        Guid operatorId)
    {
        var @operator = operatorFactory.CreateOperator(operatorType, name, 0, 0);
        typeof(Operator).GetProperty(nameof(Operator.Id))?.SetValue(@operator, operatorId);

        return new OperatorDto
        {
            Id = @operator.Id,
            Name = @operator.Name,
            Type = @operator.Type,
            X = 0,
            Y = 0,
            InputPorts = @operator.InputPorts.Select(port => new PortDto
            {
                Id = port.Id,
                Name = port.Name,
                Direction = port.Direction,
                DataType = port.DataType,
                IsRequired = port.IsRequired
            }).ToList(),
            OutputPorts = @operator.OutputPorts.Select(port => new PortDto
            {
                Id = port.Id,
                Name = port.Name,
                Direction = port.Direction,
                DataType = port.DataType,
                IsRequired = port.IsRequired
            }).ToList(),
            Parameters = @operator.Parameters.Select(parameter => new ParameterDto
            {
                Id = parameter.Id,
                Name = parameter.Name,
                DisplayName = parameter.DisplayName,
                Description = parameter.Description,
                DataType = parameter.DataType,
                Value = parameter.Value,
                DefaultValue = parameter.DefaultValue,
                MinValue = parameter.MinValue,
                MaxValue = parameter.MaxValue,
                IsRequired = parameter.IsRequired,
                Options = parameter.Options
            }).ToList()
        };
    }
}
