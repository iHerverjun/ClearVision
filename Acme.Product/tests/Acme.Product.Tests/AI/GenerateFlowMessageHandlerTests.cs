using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.AI;
using FluentAssertions;
using NSubstitute;

namespace Acme.Product.Tests.AI;

public class GenerateFlowMessageHandlerTests
{
    [Fact(DisplayName = "GenerateFlowMessageHandler should pass attachments to generation request")]
    public async Task HandleAsync_ShouldForwardAttachments()
    {
        // Arrange
        var generationService = Substitute.For<IAiFlowGenerationService>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler>>();
        var handler = new GenerateFlowMessageHandler(generationService, logger);
        var attachments = new List<string>
        {
            @"C:\temp\template.png",
            @"C:\temp\target.png"
        };

        generationService.GenerateFlowAsync(
                Arg.Any<AiFlowGenerationRequest>(),
                Arg.Any<Action<string>>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.AiStreamChunk>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>())
            .Returns(Task.FromResult(new AiFlowGenerationResult
            {
                Success = true,
                Flow = new { operators = Array.Empty<object>(), connections = Array.Empty<object>() }
            }));

        // Act
        var resultJson = await handler.HandleAsync(
            description: "tune template matching parameters",
            sessionId: "session-1",
            existingFlowJson: """{"operators":[]}""",
            hint: "template matching",
            attachments: attachments);

        // Assert
        await generationService.Received(1).GenerateFlowAsync(
            Arg.Is<AiFlowGenerationRequest>(request =>
                request.Attachments != null &&
                request.Attachments.SequenceEqual(attachments) &&
                request.Description == "tune template matching parameters"),
            Arg.Any<Action<string>>(),
            Arg.Any<Action<Acme.Product.Contracts.Messages.AiStreamChunk>>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>());

        using var doc = JsonDocument.Parse(resultJson);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "GenerateFlowMessageHandler should forward attachment report message")]
    public async Task HandleAsync_ShouldForwardAttachmentReport()
    {
        // Arrange
        var generationService = Substitute.For<IAiFlowGenerationService>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler>>();
        var handler = new GenerateFlowMessageHandler(generationService, logger);
        var receivedMessages = new List<(string Type, string Payload)>();

        generationService.GenerateFlowAsync(
                Arg.Any<AiFlowGenerationRequest>(),
                Arg.Any<Action<string>>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.AiStreamChunk>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>())
            .Returns(callInfo =>
            {
                var reportCallback = callInfo.ArgAt<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>(4);
                reportCallback(new Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport
                {
                    Sent =
                    [
                        new Acme.Product.Contracts.Messages.GenerateFlowAttachmentSentItem
                        {
                            Path = @"C:\temp\template.png",
                            Name = "template.png"
                        }
                    ],
                    Skipped =
                    [
                        new Acme.Product.Contracts.Messages.GenerateFlowAttachmentSkippedItem
                        {
                            Path = @"C:\temp\bad.txt",
                            Name = "bad.txt",
                            Reason = "unsupported_format"
                        }
                    ]
                });

                return Task.FromResult(new AiFlowGenerationResult
                {
                    Success = true,
                    Flow = new { operators = Array.Empty<object>(), connections = Array.Empty<object>() }
                });
            });

        // Act
        _ = await handler.HandleAsync(
            description: "demo",
            attachments: [@"C:\temp\template.png", @"C:\temp\bad.txt"],
            onMessage: (type, payload) => receivedMessages.Add((type, payload)));

        // Assert
        receivedMessages.Should().Contain(message => message.Type == "GenerateFlowAttachmentReport");
        var payloadJson = receivedMessages.First(message => message.Type == "GenerateFlowAttachmentReport").Payload;
        using var payloadDoc = JsonDocument.Parse(payloadJson);
        payloadDoc.RootElement.GetProperty("sent").GetArrayLength().Should().Be(1);
        payloadDoc.RootElement.GetProperty("skipped").GetArrayLength().Should().Be(1);
        payloadDoc.RootElement.GetProperty("skipped")[0].GetProperty("reason").GetString()
            .Should().Be("unsupported_format");
    }

    [Fact(DisplayName = "GenerateFlowMessageHandler should serialize OperatorType as string")]
    public async Task HandleAsync_ShouldSerializeOperatorTypeAsString()
    {
        // Arrange
        var generationService = Substitute.For<IAiFlowGenerationService>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler>>();
        var handler = new GenerateFlowMessageHandler(generationService, logger);

        generationService.GenerateFlowAsync(
                Arg.Any<AiFlowGenerationRequest>(),
                Arg.Any<Action<string>>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.AiStreamChunk>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>())
            .Returns(Task.FromResult(new AiFlowGenerationResult
            {
                Success = true,
                Flow = new OperatorFlowDto
                {
                    Operators = new List<OperatorDto>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Name = "华睿相机采集",
                            Type = OperatorType.ImageAcquisition
                        }
                    }
                }
            }));

        // Act
        var resultJson = await handler.HandleAsync("用华睿相机做缺陷检测");

        // Assert
        using var doc = JsonDocument.Parse(resultJson);
        var firstOp = doc.RootElement
            .GetProperty("flow")
            .GetProperty("operators")[0];

        firstOp.GetProperty("type").ValueKind.Should().Be(JsonValueKind.String);
        firstOp.GetProperty("type").GetString().Should().Be("ImageAcquisition");
    }

    [Fact(DisplayName = "GenerateFlowMessageHandler should include template-first structured fields")]
    public async Task HandleAsync_ShouldSerializeTemplateFirstStructuredPayload()
    {
        // Arrange
        var generationService = Substitute.For<IAiFlowGenerationService>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler>>();
        var handler = new GenerateFlowMessageHandler(generationService, logger);

        generationService.GenerateFlowAsync(
                Arg.Any<AiFlowGenerationRequest>(),
                Arg.Any<Action<string>>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.AiStreamChunk>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>())
            .Returns(Task.FromResult(new AiFlowGenerationResult
            {
                Success = true,
                Flow = new { operators = Array.Empty<object>(), connections = Array.Empty<object>() },
                RecommendedTemplate = new AiRecommendedTemplateInfo
                {
                    TemplateId = Guid.NewGuid().ToString(),
                    TemplateName = "端子线序检测",
                    MatchReason = "命中关键词：线序、端子",
                    MatchMode = "template-first",
                    Confidence = 0.91
                },
                PendingParameters =
                [
                    new AiPendingParameterInfo
                    {
                        OperatorId = "op_3",
                        ParameterNames = ["ModelPath", "Confidence"]
                    }
                ],
                MissingResources =
                [
                    new AiMissingResourceInfo
                    {
                        ResourceType = "Model",
                        ResourceKey = "DeepLearning.ModelPath",
                        Description = "缺少模型文件路径"
                    }
                ]
            }));

        // Act
        var resultJson = await handler.HandleAsync("线序检测");

        // Assert
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        root.GetProperty("recommendedTemplate").GetProperty("templateName").GetString()
            .Should().Be("端子线序检测");
        root.GetProperty("recommendedTemplate").GetProperty("matchMode").GetString()
            .Should().Be("template-first");
        root.GetProperty("pendingParameters").GetArrayLength().Should().Be(1);
        root.GetProperty("pendingParameters")[0].GetProperty("operatorId").GetString().Should().Be("op_3");
        root.GetProperty("missingResources").GetArrayLength().Should().Be(1);
        root.GetProperty("missingResources")[0].GetProperty("resourceKey").GetString()
            .Should().Be("DeepLearning.ModelPath");
    }

    [Fact(DisplayName = "GenerateFlowMessageHandler should serialize cancelled completion status")]
    public async Task HandleAsync_ShouldSerializeCancelledCompletionStatus()
    {
        // Arrange
        var generationService = Substitute.For<IAiFlowGenerationService>();
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<GenerateFlowMessageHandler>>();
        var handler = new GenerateFlowMessageHandler(generationService, logger);

        generationService.GenerateFlowAsync(
                Arg.Any<AiFlowGenerationRequest>(),
                Arg.Any<Action<string>>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.AiStreamChunk>>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<Action<Acme.Product.Contracts.Messages.GenerateFlowAttachmentReport>>())
            .Returns(Task.FromResult(new AiFlowGenerationResult
            {
                Success = false,
                ErrorMessage = "用户已取消本次生成。",
                CompletionStatus = AiFlowGenerationResult.CompletionStatusCancelled,
                FailureType = AiFlowGenerationResult.FailureTypeUserCancelled,
                FailureSummary = new AiFailureSummary
                {
                    Category = "execution",
                    Code = "user_cancelled",
                    Message = "用户已取消本次生成。"
                },
                LastAttemptDiagnostics =
                [
                    new AiAttemptDiagnostic
                    {
                        AttemptNumber = 1,
                        Stage = "execution",
                        Summary = "用户主动取消",
                        Issues =
                        [
                            new AiValidationDiagnostic
                            {
                                Severity = AiValidationSeverity.Error,
                                Code = "user_cancelled",
                                Category = "execution",
                                Message = "用户主动取消"
                            }
                        ]
                    }
                ]
            }));

        // Act
        var resultJson = await handler.HandleAsync("取消测试", requestId: "req-cancel-1");

        // Assert
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("status").GetString().Should().Be(AiFlowGenerationResult.CompletionStatusCancelled);
        root.GetProperty("failureType").GetString().Should().Be(AiFlowGenerationResult.FailureTypeUserCancelled);
        root.GetProperty("failureSummary").GetString().Should().Be("用户已取消本次生成。");
        root.GetProperty("lastAttemptDiagnostics").GetArrayLength().Should().Be(1);
        root.GetProperty("requestId").GetString().Should().Be("req-cancel-1");
    }

}
