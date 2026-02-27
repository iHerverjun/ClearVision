using System.Text.Json;
using Acme.Product.Core.DTOs;
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
}
