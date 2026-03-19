using System.Net.Http.Json;
using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Desktop.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenCvSharp;

namespace Acme.Product.Desktop.Tests;

public class PreviewNodeEndpointsTests
{
    [Fact]
    public async Task PreviewNode_UsesBreakAtOperatorAndReturnsTargetOutput()
    {
        var projectId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var debugSessionId = Guid.NewGuid();
        var outputBytes = new byte[] { 1, 2, 3, 4 };
        DebugOptions? capturedOptions = null;
        OperatorFlow? capturedFlow = null;

        await using var host = await PreviewNodeTestHost.CreateAsync(flowExecution =>
        {
            flowExecution.ExecuteFlowDebugAsync(
                    Arg.Any<OperatorFlow>(),
                    Arg.Any<DebugOptions>(),
                    Arg.Any<Dictionary<string, object>?>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    capturedFlow = callInfo.ArgAt<OperatorFlow>(0);
                    capturedOptions = callInfo.ArgAt<DebugOptions>(1);

                    return Task.FromResult(new FlowDebugExecutionResult
                    {
                        IsSuccess = true,
                        DebugSessionId = debugSessionId,
                        ExecutionTimeMs = 12,
                        IntermediateResults = new Dictionary<Guid, Dictionary<string, object>>
                        {
                            [targetNodeId] = new()
                            {
                                ["Image"] = outputBytes,
                                ["Score"] = 0.95
                            }
                        },
                        DebugOperatorResults = new List<OperatorDebugResult>
                        {
                            new()
                            {
                                OperatorId = targetNodeId,
                                OperatorName = "Threshold",
                                IsSuccess = true,
                                ExecutionOrder = 0,
                                ExecutionTimeMs = 12
                            }
                        }
                    });
                });
        });

        var request = new PreviewNodeRequest
        {
            ProjectId = projectId,
            TargetNodeId = targetNodeId,
            DebugSessionId = debugSessionId,
            ImageFormat = ".bmp",
            Parameters = new Dictionary<string, object>
            {
                ["Threshold"] = 180
            },
            FlowData = new FlowData
            {
                Operators = new List<OperatorData>
                {
                    new()
                    {
                        Id = targetNodeId,
                        Name = "Threshold",
                        Type = "Thresholding",
                        Parameters = new Dictionary<string, object>
                        {
                            ["Threshold"] = 128
                        }
                    }
                }
            }
        };

        using var response = await host.Client.PostAsJsonAsync("/api/flows/preview-node", request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("targetNodeId").GetGuid().Should().Be(targetNodeId);
        document.RootElement.GetProperty("debugSessionId").GetGuid().Should().Be(debugSessionId);
        document.RootElement.GetProperty("outputImageBase64").GetString().Should().Be(Convert.ToBase64String(outputBytes));
        document.RootElement.GetProperty("executedOperators").GetArrayLength().Should().Be(1);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.BreakAtOperatorId.Should().Be(targetNodeId);
        capturedOptions.DebugSessionId.Should().Be(debugSessionId);
        capturedOptions.ImageFormat.Should().Be(".bmp");
        capturedOptions.EnableIntermediateCache.Should().BeTrue();

        capturedFlow.Should().NotBeNull();
        var thresholdParameter = capturedFlow!.Operators
            .Single(op => op.Id == targetNodeId)
            .Parameters
            .Single(param => param.Name == "Threshold")
            .GetValue();

        ReadIntValue(thresholdParameter).Should().Be(180);
    }

    [Fact]
    public async Task PreviewNode_ReturnsMinimalFeedbackMetrics()
    {
        var projectId = Guid.NewGuid();
        var targetNodeId = Guid.NewGuid();
        var previewImage = CreateBinaryPreviewImageBytes();

        await using var host = await PreviewNodeTestHost.CreateAsync(flowExecution =>
        {
            flowExecution.ExecuteFlowDebugAsync(
                    Arg.Any<OperatorFlow>(),
                    Arg.Any<DebugOptions>(),
                    Arg.Any<Dictionary<string, object>?>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new FlowDebugExecutionResult
                {
                    IsSuccess = true,
                    DebugSessionId = Guid.NewGuid(),
                    ExecutionTimeMs = 24,
                    IntermediateResults = new Dictionary<Guid, Dictionary<string, object>>
                    {
                        [targetNodeId] = new()
                        {
                            ["Image"] = previewImage,
                            ["Defects"] = new List<Dictionary<string, object>>
                            {
                                new() { ["Area"] = 4.0 },
                                new() { ["Area"] = 6.0 }
                            }
                        }
                    }
                }));
        });

        using var response = await host.Client.PostAsJsonAsync("/api/flows/preview-node", new PreviewNodeRequest
        {
            ProjectId = projectId,
            TargetNodeId = targetNodeId,
            FlowData = new FlowData
            {
                Operators = new List<OperatorData>
                {
                    new()
                    {
                        Id = targetNodeId,
                        Name = "Threshold",
                        Type = "Thresholding"
                    }
                }
            }
        });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metrics = document.RootElement.GetProperty("metrics");
        metrics.GetProperty("blobCount").GetInt32().Should().Be(2);
        metrics.GetProperty("binaryRatio").GetDouble().Should().BeApproximately(0.5d, 0.001d);
        metrics.GetProperty("areaStats").GetProperty("min").GetDouble().Should().Be(4d);
        metrics.GetProperty("areaStats").GetProperty("max").GetDouble().Should().Be(6d);
        metrics.GetProperty("areaStats").GetProperty("mean").GetDouble().Should().Be(5d);
    }

    private static int ReadIntValue(object? value)
    {
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            double doubleValue => (int)doubleValue,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetInt32(),
            _ => throw new InvalidOperationException($"Unsupported parameter value: {value?.GetType().FullName ?? "<null>"}")
        };
    }

    private static byte[] CreateBinaryPreviewImageBytes()
    {
        using var image = new Mat(2, 2, MatType.CV_8UC1, Scalar.All(0));
        image.Set(0, 0, 255);
        image.Set(1, 1, 255);
        return image.ToBytes(".png");
    }

    private sealed class PreviewNodeTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private PreviewNodeTestHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<PreviewNodeTestHost> CreateAsync(Action<IFlowExecutionService> configureFlowExecution)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });

            builder.WebHost.UseTestServer();

            var flowExecution = Substitute.For<IFlowExecutionService>();
            var projectRepository = Substitute.For<IProjectRepository>();
            configureFlowExecution(flowExecution);

            builder.Services.AddSingleton(flowExecution);
            builder.Services.AddSingleton(projectRepository);

            var app = builder.Build();
            app.MapPreviewNodeEndpoints();
            await app.StartAsync();

            return new PreviewNodeTestHost(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
