// IAiFlowGenerationService.cs
// AI 流程生成服务接口
// 定义 AI 流程生成与验证的服务契约
// 作者：蘅芜君
using Acme.Product.Core.DTOs;
using Acme.Product.Contracts.Messages;

namespace Acme.Product.Core.Services;

/// <summary>
/// AI 工作流生成服务接口
/// </summary>
public interface IAiFlowGenerationService
{
    /// <summary>
    /// 根据自然语言描述生成工作流
    /// </summary>
    /// <param name="request">生成请求（用户描述 + 可选上下文）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<AiFlowGenerationResult> GenerateFlowAsync(
        AiFlowGenerationRequest request,
        Action<string>? onProgress = null,
        Action<AiStreamChunk>? onStreamChunk = null,
        CancellationToken cancellationToken = default,
        Action<GenerateFlowAttachmentReport>? onAttachmentReport = null);
}

/// <summary>
/// AI 校验服务接口
/// </summary>
public interface IAiFlowValidator
{
    /// <summary>
    /// 校验 AI 生成的工作流是否合法
    /// </summary>
    AiValidationResult Validate(AiGeneratedFlowJson generatedFlow);
}
