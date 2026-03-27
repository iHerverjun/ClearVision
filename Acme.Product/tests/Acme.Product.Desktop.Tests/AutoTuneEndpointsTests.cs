using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Desktop.Endpoints;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acme.Product.Desktop.Tests;

public class AutoTuneEndpointsTests
{
    [Fact]
    public async Task FlowNodePreview_ShouldReturnMetricsDiagnosticCodesAndSuggestions()
    {
        var targetNodeId = Guid.NewGuid();
        var previewService = Substitute.For<IFlowNodePreviewService>();
        var autoTuneService = Substitute.For<IAutoTuneService>();

        previewService.PreviewWithMetricsAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<Guid>(),
                Arg.Any<byte[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowNodePreviewWithMetricsResult
            {
                Success = true,
                TargetNodeId = targetNodeId,
                PreviewImage = new byte[] { 1, 2, 3 },
                Outputs = new Dictionary<string, object>
                {
                    ["IsMatch"] = false,
                    ["ExpectedLabels"] = new[] { "Wire_Brown", "Wire_Black", "Wire_Blue" },
                    ["ActualOrder"] = new[] { "Wire_Brown", "Wire_Blue" }
                },
                Metrics = new PreviewMetrics
                {
                    OverallScore = 0.72,
                    Diagnostics = new List<string> { PreviewDiagnosticTags.MissingExpectedClass }
                },
                DiagnosticCodes = new List<string> { "missing_expected_class" },
                Suggestions = new List<ParameterSuggestion>
                {
                    new()
                    {
                        ParameterName = "BoxNms.ScoreThreshold",
                        SuggestedValue = "decrease",
                        Reason = "当前数量低于预期",
                        ExpectedImprovement = "保留更多候选框"
                    }
                }
            }));

        await using var host = await AutoTuneEndpointTestHost.CreateAsync(previewService, autoTuneService);

        using var response = await host.Client.PostAsJsonAsync("/api/autotune/flow-node/preview", new FlowNodePreviewRequest
        {
            FlowId = Guid.NewGuid(),
            TargetNodeId = targetNodeId,
            InputImageBase64 = Convert.ToBase64String(new byte[] { 9, 9, 9 }),
            FlowData = CreateFlowData(targetNodeId, OperatorType.DetectionSequenceJudge)
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("targetNodeId").GetGuid().Should().Be(targetNodeId);
        document.RootElement.GetProperty("previewImageBase64").GetString().Should().Be(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        document.RootElement.GetProperty("diagnosticCodes")[0].GetString().Should().Be("missing_expected_class");
        document.RootElement.GetProperty("suggestions")[0].GetProperty("parameterName").GetString().Should().Be("BoxNms.ScoreThreshold");
        document.RootElement.GetProperty("metrics").GetProperty("overallScore").GetDouble().Should().BeApproximately(0.72d, 0.001d);
    }

    [Fact]
    public async Task FlowNodePreview_ShouldReturnMissingResources()
    {
        var targetNodeId = Guid.NewGuid();
        var previewService = Substitute.For<IFlowNodePreviewService>();
        var autoTuneService = Substitute.For<IAutoTuneService>();

        previewService.PreviewWithMetricsAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<Guid>(),
                Arg.Any<byte[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FlowNodePreviewWithMetricsResult
            {
                Success = false,
                TargetNodeId = targetNodeId,
                ErrorMessage = "线序预览缺少必要资源",
                DiagnosticCodes = new List<string> { "missing_model", "missing_labels" },
                MissingResources = new List<PreviewMissingResource>
                {
                    new()
                    {
                        ResourceType = "Model",
                        ResourceKey = "DeepLearning.ModelPath",
                        Description = "缺少模型文件路径",
                        DiagnosticCode = "missing_model"
                    },
                    new()
                    {
                        ResourceType = "Label",
                        ResourceKey = "DeepLearning.LabelsPath",
                        Description = "缺少标签文件路径",
                        DiagnosticCode = "missing_labels"
                    }
                }
            }));

        await using var host = await AutoTuneEndpointTestHost.CreateAsync(previewService, autoTuneService);

        using var response = await host.Client.PostAsJsonAsync("/api/autotune/flow-node/preview", new FlowNodePreviewRequest
        {
            FlowId = Guid.NewGuid(),
            TargetNodeId = targetNodeId,
            FlowData = CreateFlowData(targetNodeId, OperatorType.DetectionSequenceJudge)
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("missingResources").GetArrayLength().Should().Be(2);
        document.RootElement.GetProperty("diagnosticCodes").EnumerateArray().Select(item => item.GetString()).Should()
            .Contain(new[] { "missing_model", "missing_labels" });
    }

    [Fact]
    public async Task FlowNodePreview_ShouldPreserveCanvasSerializedImageAcquisitionParameters()
    {
        var acquisitionId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        OperatorFlow? capturedFlow = null;
        var previewService = Substitute.For<IFlowNodePreviewService>();
        var autoTuneService = Substitute.For<IAutoTuneService>();
        using var acquisitionParametersJson = JsonDocument.Parse("""
            [
              { "name": "SourceType", "value": "File", "dataType": "enum" },
              { "name": "FilePath", "value": "demo.png", "dataType": "file" }
            ]
            """);

        var acquisitionOutputId = Guid.NewGuid();
        var targetInputId = Guid.NewGuid();

        previewService.PreviewWithMetricsAsync(
                Arg.Any<OperatorFlow>(),
                Arg.Any<Guid>(),
                Arg.Any<byte[]?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedFlow = callInfo.ArgAt<OperatorFlow>(0);
                return Task.FromResult(new FlowNodePreviewWithMetricsResult
                {
                    Success = true,
                    TargetNodeId = targetNodeId
                });
            });

        await using var host = await AutoTuneEndpointTestHost.CreateAsync(previewService, autoTuneService);

        using var response = await host.Client.PostAsJsonAsync("/api/autotune/flow-node/preview", new
        {
            flowId = Guid.NewGuid(),
            targetNodeId = targetNodeId,
            flowData = new
            {
                id = Guid.NewGuid(),
                name = "CanvasPreviewFlow",
                operators = new object[]
                {
                    new
                    {
                        id = acquisitionId,
                        name = "Acquire",
                        type = "ImageAcquisition",
                        x = 0,
                        y = 0,
                        parameters = acquisitionParametersJson.RootElement.Clone(),
                        outputPorts = new[]
                        {
                            new
                            {
                                id = acquisitionOutputId,
                                name = "Image",
                                dataType = "Image",
                                isRequired = false
                            }
                        }
                    },
                    new
                    {
                        id = targetNodeId,
                        name = "Resize",
                        type = "ImageResize",
                        x = 10,
                        y = 10,
                        inputPorts = new[]
                        {
                            new
                            {
                                id = targetInputId,
                                name = "Image",
                                dataType = "Image",
                                isRequired = true
                            }
                        }
                    }
                },
                connections = new object[]
                {
                    new
                    {
                        sourceOperatorId = acquisitionId,
                        sourcePortId = acquisitionOutputId,
                        targetOperatorId = targetNodeId,
                        targetPortId = targetInputId
                    }
                }
            }
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        capturedFlow.Should().NotBeNull();
        var acquisition = capturedFlow!.Operators.Single(op => op.Id == acquisitionId);
        acquisition.Parameters.Single(p => p.Name == "SourceType").GetValue().Should().Be("File");
        acquisition.Parameters.Single(p => p.Name == "FilePath").GetValue().Should().Be("demo.png");
    }

    [Fact]
    public async Task ScenarioAutoTune_ShouldReturnFinalPreviewAndParameters()
    {
        var targetNodeId = Guid.NewGuid();
        var previewService = Substitute.For<IFlowNodePreviewService>();
        var autoTuneService = Substitute.For<IAutoTuneService>();

        autoTuneService.AutoTuneScenarioAsync(
                Arg.Any<string>(),
                Arg.Any<OperatorFlow>(),
                Arg.Any<byte[]>(),
                Arg.Any<AutoTuneGoal>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ScenarioAutoTuneResult
            {
                Success = true,
                ScenarioKey = "wire-sequence-terminal",
                FinalParameters = new Dictionary<string, object>
                {
                    ["BoxNms.ScoreThreshold"] = 0.2d,
                    ["BoxNms.IouThreshold"] = 0.4d
                },
                TotalIterations = 2,
                TotalExecutionTimeMs = 30,
                IsGoalAchieved = true,
                DiagnosticCodes = new List<string>(),
                FinalPreview = new FlowNodePreviewWithMetricsResult
                {
                    Success = true,
                    TargetNodeId = targetNodeId,
                    Outputs = new Dictionary<string, object>
                    {
                        ["IsMatch"] = true
                    },
                    Suggestions = new List<ParameterSuggestion>
                    {
                        new()
                        {
                            ParameterName = "BoxNms.IouThreshold",
                            SuggestedValue = "decrease",
                            Reason = "收紧重复框",
                            ExpectedImprovement = "减少同类重复框"
                        }
                    }
                }
            }));

        await using var host = await AutoTuneEndpointTestHost.CreateAsync(previewService, autoTuneService);

        using var response = await host.Client.PostAsJsonAsync("/api/autotune/scenario", new ScenarioAutoTuneRequest
        {
            ScenarioKey = "wire-sequence-terminal",
            InputImageBase64 = Convert.ToBase64String(new byte[] { 7, 8, 9 }),
            FlowData = CreateFlowData(targetNodeId, OperatorType.DetectionSequenceJudge)
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("scenarioKey").GetString().Should().Be("wire-sequence-terminal");
        document.RootElement.GetProperty("finalParameters").GetProperty("BoxNms.ScoreThreshold").GetDouble().Should().BeApproximately(0.2d, 0.001d);
        document.RootElement.GetProperty("finalParameters").GetProperty("BoxNms.IouThreshold").GetDouble().Should().BeApproximately(0.4d, 0.001d);
        document.RootElement.GetProperty("finalPreview").GetProperty("success").GetBoolean().Should().BeTrue();
    }

    private static FlowDataDto CreateFlowData(Guid nodeId, OperatorType type)
    {
        return new FlowDataDto
        {
            Id = Guid.NewGuid(),
            Name = "WireSequenceFlow",
            Nodes =
            [
                new FlowNodeDto
                {
                    Id = nodeId,
                    Name = type.ToString(),
                    Type = type,
                    Position = new PositionDto()
                }
            ]
        };
    }

    private sealed class AutoTuneEndpointTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private AutoTuneEndpointTestHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<AutoTuneEndpointTestHost> CreateAsync(
            IFlowNodePreviewService previewService,
            IAutoTuneService autoTuneService)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddLogging();
            builder.Services.AddSingleton(previewService);
            builder.Services.AddSingleton(autoTuneService);
            builder.Services.AddSingleton(Substitute.For<IPreviewMetricsAnalyzer>());
            builder.Services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapAutoTuneEndpoints();
            await app.StartAsync();

            return new AutoTuneEndpointTestHost(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "test-user")
            ], SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
