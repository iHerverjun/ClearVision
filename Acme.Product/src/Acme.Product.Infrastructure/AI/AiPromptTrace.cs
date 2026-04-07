namespace Acme.Product.Infrastructure.AI;

public sealed class AiPromptTrace
{
    public string Mode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public AiModelCapabilities? Capabilities { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public object? AttachmentReport { get; set; }
    public string UsedReferenceFlowSummary { get; set; } = string.Empty;
}
