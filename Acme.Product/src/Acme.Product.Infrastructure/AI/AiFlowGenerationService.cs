// AiFlowGenerationService.cs
// AI 流程生成服务实现
// 负责流程草案生成、修正与结果封装
// 作者：蘅芜君
using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.AI.DryRun;
using Acme.Product.Infrastructure.AI.Runtime;
using Acme.Product.Contracts.Messages;
using OpenCvSharp;
using System.Globalization;
using System.Net;
using System.Text;

namespace Acme.Product.Infrastructure.AI;

public class AiFlowGenerationService : IAiFlowGenerationService
{
    private readonly AiGenerationOrchestrator _aiOrchestrator;
    private readonly PromptBuilder _promptBuilder;
    private readonly IConversationalFlowService _conversationalFlowService;
    private readonly IAiFlowValidator _validator;
    private readonly AutoLayoutService _layoutService;
    private readonly IOperatorFactory _operatorFactory;
    private readonly IFlowTemplateService _templateService;
    private readonly DryRunService _dryRunService;
    private readonly Microsoft.Extensions.Logging.ILogger<AiFlowGenerationService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new FlexibleStringDictionaryJsonConverter()
        }
    };
    private const int DefaultMaxMultimodalAttachmentCount = 4;
    private static readonly string[] _templateFirstKeywords =
    [
        "线序", "端子", "接线顺序", "排针顺序",
        "wire sequence", "terminal order", "connector order", "wiring order"
    ];
    private static readonly string[] _wireTemplateHints =
    [
        "线序", "端子", "接线", "connector", "terminal", "wire"
    ];

    public AiFlowGenerationService(
        AiGenerationOrchestrator aiOrchestrator,
        PromptBuilder promptBuilder,
        IConversationalFlowService conversationalFlowService,
        IAiFlowValidator validator,
        AutoLayoutService layoutService,
        IOperatorFactory operatorFactory,
        IFlowTemplateService templateService,
        DryRunService dryRunService,
        Microsoft.Extensions.Logging.ILogger<AiFlowGenerationService> logger)
    {
        _aiOrchestrator = aiOrchestrator;
        _promptBuilder = promptBuilder;
        _conversationalFlowService = conversationalFlowService;
        _validator = validator;
        _layoutService = layoutService;
        _operatorFactory = operatorFactory;
        _templateService = templateService;
        _dryRunService = dryRunService;
        _logger = logger;
    }

    public async Task<AiFlowGenerationResult> GenerateFlowAsync(
        AiFlowGenerationRequest request,
        Action<string>? onProgress = null,
        Action<AiStreamChunk>? onStreamChunk = null,
        CancellationToken cancellationToken = default,
        Action<GenerateFlowAttachmentReport>? onAttachmentReport = null)
    {
        // 推送：构建提示词
        onProgress?.Invoke("正在分析需求并构建提示词...");
        var conversationContext = _conversationalFlowService.PrepareContext(request);
        var templatePriority = await BuildTemplatePriorityContextAsync(request, cancellationToken);
        if (templatePriority.IsTemplateFirst)
        {
            onProgress?.Invoke("检测到线序高频场景，已切换模板优先生成模式...");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt(request.Description);
        var userMessage = BuildUserMessage(
            request,
            conversationContext.ExistingFlowJson,
            conversationContext.Intent,
            conversationContext.PromptContext,
            templatePriority);

        // 读取当前激活模型快照
        var activeModel = _aiOrchestrator.ResolveGenerationModel();
        var options = activeModel.ToGenerationOptions();
        var capabilities = _aiOrchestrator.ResolveCapabilities(activeModel);

        var maxAttachmentCount = capabilities.MaxImageCount > 0
            ? Math.Min(DefaultMaxMultimodalAttachmentCount, capabilities.MaxImageCount)
            : 0;
        var maxImageBytes = Math.Min(
            AiApiClient.MaxImageBytes,
            capabilities.MaxImageBytes > 0 ? capabilities.MaxImageBytes : AiApiClient.MaxImageBytes);

        var attachmentSelection = AnalyzeMultimodalAttachments(request.Attachments, maxAttachmentCount, maxImageBytes);
        if (request.Attachments is { Count: > 0 })
        {
            onAttachmentReport?.Invoke(attachmentSelection.Report);
        }

        IReadOnlyList<string> activeSendablePaths = attachmentSelection.SendablePaths;
        if (activeSendablePaths.Count > 0 && !capabilities.SupportsVisionInput)
        {
            _logger.LogInformation(
                "Model {Model} capability says vision input is unsupported. Falling back to text-only mode.",
                options.Model);
            activeSendablePaths = Array.Empty<string>();
            onAttachmentReport?.Invoke(BuildFallbackAttachmentReport(attachmentSelection.Report, "model_not_support_image"));
            onProgress?.Invoke("当前模型不支持图片输入，已自动切换为文本模式（附件仅用于元信息）。");
        }
        var currentUserMessage = BuildUserChatMessage(userMessage, activeSendablePaths);

        AiGeneratedFlowJson? generatedFlow = null;
        AiValidationResult? lastValidation = null;
        List<AiAttemptDiagnostic> lastAttemptDiagnostics = new();
        string? lastRawResponse = null;
        int retryCount = 0;

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Calling AI API, attempt {Attempt}", attempt + 1);

                // 推送：正在调用 AI（带重试次数）
                if (attempt > 0)
                    onProgress?.Invoke($"AI 响应未通过校验或出错，正在重试（第 {attempt}/{options.MaxRetries} 次重试）...");
                else
                    onProgress?.Invoke("正在请求 AI 模型生成方案...");

                // 构建完整的上下文消息
                var messages = new List<ChatMessage> { currentUserMessage };
                if (attempt > 0)
                {
                    if (!string.IsNullOrWhiteSpace(lastRawResponse))
                    {
                        messages.Add(new ChatMessage("assistant", TrimRetryOutput(lastRawResponse)));
                    }

                    messages.Add(new ChatMessage("user", BuildRetryMessage(userMessage, lastValidation!, lastRawResponse)));
                }

                // 调用 API（使用流式接口）
                var completionResult = await _aiOrchestrator.StreamCompleteAsync(
                    systemPrompt,
                    messages,
                    chunk => onStreamChunk?.Invoke(chunk),
                    activeModel,
                    cancellationToken);
                var rawResponse = completionResult.Content;
                lastRawResponse = rawResponse;
                _logger.LogDebug("AI 原始响应长度：{Length}", rawResponse.Length);
                if (!string.IsNullOrEmpty(completionResult.Reasoning))
                {
                    _logger.LogDebug("AI 思维链：{Reasoning}", completionResult.Reasoning[..Math.Min(200, completionResult.Reasoning.Length)] + "...");
                }
                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    var reasoningLength = completionResult.Reasoning?.Length ?? 0;
                    _logger.LogWarning("AI 流式响应正文为空。ReasoningLength={ReasoningLength}", reasoningLength);
                }

                // 推送：解析结果
                onProgress?.Invoke("收到 AI 响应，正在解析 JSON 数据...");
                // 解析 AI 输出的 JSON
                generatedFlow = ParseAiResponse(rawResponse);
                if (generatedFlow == null)
                {
                    lastValidation = new AiValidationResult();
                    lastValidation.AddError(
                        "AI 返回的内容不是合法的 JSON 格式",
                        code: "invalid_json",
                        category: "format",
                        relatedFields: ["response.content"],
                        repairHint: "请只返回一个完整 JSON 对象，不要附加 markdown、解释文本或多余前后缀。");
                    lastAttemptDiagnostics = BuildAttemptDiagnostics(
                        attempt + 1,
                        "parse",
                        lastValidation,
                        lastRawResponse);
                    retryCount++;
                    continue;
                }

                // 推送：校验结果
                onProgress?.Invoke("正在校验生成的算子和参数有效性...");
                // 校验
                lastValidation = _validator.Validate(generatedFlow);
                lastAttemptDiagnostics = BuildAttemptDiagnostics(
                    attempt + 1,
                    "validation",
                    lastValidation,
                    lastRawResponse);
                if (lastValidation.IsValid)
                {
                    // 校验通过，转换为 DTO 并返回
                    var (flowDto, actualOperatorIdMap) = ConvertToFlowDto(generatedFlow, request.Description);
                    _layoutService.ApplyLayout(flowDto);

                    onProgress?.Invoke("正在进行 Dry-Run 沙箱安全校验与分支覆盖率统计...");

                    // S6-003: 转换并在虚拟沙箱中运行以收集覆盖率
                    object? dryRunReport = null;
                    try
                    {
                        var flowEntity = ConvertDtoToEntity(flowDto); // 暂时需转换为 Entity 供仿真使用
                        var drResult = await _dryRunService.RunAsync(
                            flowEntity,
                            new Dictionary<string, object>(), // 空输入
                            new DryRunStubRegistry(),
                            cancellationToken);

                        dryRunReport = new
                        {
                            drResult.CoveragePercentage,
                            drResult.CoveredBranches,
                            drResult.TotalBranches,
                            drResult.IsSuccess
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "DryRun 预演阶段异常，跳过覆盖率采集");
                    }

                    var recommendedTemplate = ResolveRecommendedTemplate(generatedFlow, templatePriority);
                    var pendingParameters = BuildPendingParameters(generatedFlow, actualOperatorIdMap);
                    var missingResources = BuildMissingResources(generatedFlow, templatePriority);
                    generatedFlow.PendingParameters = pendingParameters;

                    _conversationalFlowService.RecordAssistantResponse(
                        conversationContext.SessionId,
                        generatedFlow.Explanation,
                        JsonSerializer.Serialize(generatedFlow, _jsonOptions),
                        JsonSerializer.Serialize(flowDto, _jsonOptions));

                    return new AiFlowGenerationResult
                    {
                        Success = true,
                        CompletionStatus = AiFlowGenerationResult.CompletionStatusCompleted,
                        Flow = flowDto,
                        AiExplanation = generatedFlow.Explanation,
                        Reasoning = completionResult.Reasoning,
                        ParametersNeedingReview = generatedFlow.ParametersNeedingReview,
                        RetryCount = retryCount,
                        SessionId = conversationContext.SessionId,
                        DetectedIntent = conversationContext.Intent.ToString().ToUpperInvariant(),
                        DryRunResult = dryRunReport,
                        RecommendedTemplate = recommendedTemplate,
                        PendingParameters = pendingParameters,
                        MissingResources = missingResources
                    };
                }

                _logger.LogWarning("AI 生成内容校验失败，错误：{Errors}",
                    string.Join("; ", lastValidation.Errors));
                retryCount++;
            }
            catch (OperationCanceledException)
            {
                var wasUserCancelled = cancellationToken.IsCancellationRequested;
                var failureType = wasUserCancelled
                    ? AiFlowGenerationResult.FailureTypeUserCancelled
                    : AiFlowGenerationResult.FailureTypeTimeout;
                var completionStatus = wasUserCancelled
                    ? AiFlowGenerationResult.CompletionStatusCancelled
                    : AiFlowGenerationResult.CompletionStatusTimedOut;
                var errorMessage = wasUserCancelled
                    ? "用户已取消本次生成。"
                    : "AI generation timed out. Please retry.";
                var cancelledValidation = new AiValidationResult();
                cancelledValidation.AddError(
                    errorMessage,
                    code: wasUserCancelled ? "user_cancelled" : "generation_timeout",
                    category: "execution",
                    relatedFields: ["request"],
                    repairHint: wasUserCancelled
                        ? "如需继续，请在补充信息后重新发起生成。"
                        : "请稍后重试，或缩短输入与附件规模。");
                lastAttemptDiagnostics = BuildAttemptDiagnostics(
                    attempt + 1,
                    "execution",
                    cancelledValidation,
                    lastRawResponse);

                _logger.LogWarning(
                    "AI 生成被中断。WasUserCancelled={WasUserCancelled}, SessionId={SessionId}",
                    wasUserCancelled,
                    conversationContext.SessionId);
                RecordFailureResponse(
                    conversationContext.SessionId,
                    errorMessage,
                    lastRawResponse);
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    CompletionStatus = completionStatus,
                    FailureType = failureType,
                    FailureSummary = BuildFailureSummary(
                        cancelledValidation,
                        retryCount,
                        errorMessage,
                        lastRawResponse,
                        fallbackCode: wasUserCancelled ? "user_cancelled" : "generation_timeout",
                        fallbackCategory: "execution"),
                    LastAttemptDiagnostics = lastAttemptDiagnostics
                };
            }
            catch (Exception ex)
            {
                // 附件发送导致 400 时，自动降级为文本模式并重试一次，避免直接失败。
                if (activeSendablePaths.Count > 0 && IsBadRequestHttpException(ex))
                {
                    _logger.LogWarning(
                        ex,
                        "Multimodal request failed with 400. Fallback to text-only mode. Model={Model}, Provider={Provider}",
                        options.Model,
                        options.Provider);

                    activeSendablePaths = Array.Empty<string>();
                    currentUserMessage = BuildUserChatMessage(userMessage, activeSendablePaths);
                    onAttachmentReport?.Invoke(BuildFallbackAttachmentReport(attachmentSelection.Report, "model_not_support_image"));
                    onProgress?.Invoke("图片附件暂不被当前模型/接口支持，已自动改为文本模式重试...");
                    retryCount++;
                    attempt--;
                    continue;
                }

                _logger.LogError(ex, "AI API 调用失败");
                var failureValidation = new AiValidationResult();
                failureValidation.AddError(
                    $"AI service call failed: {ex.Message}",
                    code: "service_call_failed",
                    category: "execution",
                    relatedFields: ["request"],
                    repairHint: "请检查模型服务状态、网络环境或输入约束后重试。");
                lastAttemptDiagnostics = BuildAttemptDiagnostics(
                    attempt + 1,
                    "execution",
                    failureValidation,
                    lastRawResponse);
                RecordFailureResponse(
                    conversationContext.SessionId,
                    $"AI service call failed: {ex.Message}",
                    lastRawResponse);
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"AI service call failed: {ex.Message}",
                    CompletionStatus = AiFlowGenerationResult.CompletionStatusFailed,
                    FailureType = AiFlowGenerationResult.FailureTypeSystemError,
                    FailureSummary = BuildFailureSummary(
                        failureValidation,
                        retryCount,
                        $"AI service call failed: {ex.Message}",
                        lastRawResponse,
                        fallbackCode: "service_call_failed",
                        fallbackCategory: "execution"),
                    LastAttemptDiagnostics = lastAttemptDiagnostics
                };
            }
        }

        // 所有重试均失败
        var finalErrorMessage = BuildFinalValidationErrorMessage(lastValidation, retryCount);
        RecordFailureResponse(conversationContext.SessionId, finalErrorMessage, lastRawResponse);
        return new AiFlowGenerationResult
        {
            Success = false,
            ErrorMessage = finalErrorMessage,
            RetryCount = retryCount,
            CompletionStatus = AiFlowGenerationResult.CompletionStatusFailed,
            FailureSummary = BuildFailureSummary(
                lastValidation,
                retryCount,
                finalErrorMessage,
                lastRawResponse,
                fallbackCode: "validation_failed",
                fallbackCategory: "validation"),
            LastAttemptDiagnostics = lastAttemptDiagnostics
        };
    }

    private string BuildUserMessage(
        AiFlowGenerationRequest request,
        string? existingFlow,
        ConversationIntent intent,
        string promptContext,
        TemplatePriorityContext templatePriority)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Please generate a workflow from the following request:");
        sb.AppendLine();
        sb.AppendLine(request.Description);

        if (!string.IsNullOrWhiteSpace(request.AdditionalContext))
        {
            sb.AppendLine();
            sb.AppendLine($"Additional context: {request.AdditionalContext}");
        }

        if (templatePriority.IsTemplateFirst)
        {
            sb.AppendLine();
            sb.AppendLine("Template-first mode is enabled for this request.");
            sb.AppendLine($"Template match reason: {templatePriority.MatchReason}");
            if (templatePriority.Template != null)
            {
                sb.AppendLine($"Preferred template id: {templatePriority.Template.Id}");
                sb.AppendLine($"Preferred template name: {templatePriority.Template.Name}");
                sb.AppendLine($"Preferred template industry: {templatePriority.Template.Industry}");
                if (!string.IsNullOrWhiteSpace(templatePriority.Template.FlowJson))
                {
                    sb.AppendLine("Preferred template skeleton JSON:");
                    sb.AppendLine("```json");
                    sb.AppendLine(TrimTemplateFlowJson(templatePriority.Template.FlowJson));
                    sb.AppendLine("```");
                    sb.AppendLine("Reuse this skeleton as the starting point unless the user explicitly asks to replace it.");
                }
            }
            else
            {
                sb.AppendLine("No exact template file is currently available, but keep the workflow in wire-sequence pattern.");
            }
            sb.AppendLine("Please preserve template skeleton first, then only adjust missing operators or parameters.");
            sb.AppendLine("In JSON output, include recommendedTemplate, pendingParameters, and missingResources fields.");
        }

        var attachmentContext = BuildAttachmentContext(request.Attachments);
        if (!string.IsNullOrWhiteSpace(attachmentContext))
        {
            sb.AppendLine();
            sb.AppendLine("Attachment context:");
            sb.AppendLine(attachmentContext);
            sb.AppendLine("If attachments include template and target images, provide concrete template-matching parameter ranges.");
        }

        if (!string.IsNullOrWhiteSpace(promptContext))
        {
            sb.AppendLine();
            sb.AppendLine("会话上下文：");
            sb.AppendLine(promptContext);
        }

        if (!string.IsNullOrWhiteSpace(existingFlow))
        {
            sb.AppendLine();
            sb.AppendLine("以下是用户当前的工作流 JSON，请在此基础上处理：");
            sb.AppendLine("```json");
            sb.AppendLine(existingFlow);
            sb.AppendLine("```");

            if (intent == ConversationIntent.Modify)
            {
                sb.AppendLine("Requirement: apply incremental changes only to explicitly requested parts and keep other nodes/connections unchanged.");
            }
            else if (intent == ConversationIntent.Explain)
            {
                sb.AppendLine("Requirement: keep operators and connections unchanged and focus on improving the explanation field.");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Output must be valid JSON only with no extra text.");

        return sb.ToString();
    }

    private static ChatMessage BuildUserChatMessage(string userMessage, IReadOnlyList<string> sendablePaths)
    {
        if (sendablePaths.Count == 0)
            return new ChatMessage("user", userMessage);

        var parts = new List<ChatMessageContentPart>(sendablePaths.Count + 1)
        {
            ChatMessageContentPart.TextPart(userMessage)
        };
        parts.AddRange(sendablePaths.Select(path => ChatMessageContentPart.ImageFile(path, "high")));
        return new ChatMessage("user", parts);
    }

    private static AttachmentSelectionResult AnalyzeMultimodalAttachments(
        IReadOnlyList<string>? attachments,
        int maxCount,
        int maxImageBytes)
    {
        if (attachments == null || attachments.Count == 0 || maxCount <= 0)
        {
            return new AttachmentSelectionResult(Array.Empty<string>(), new GenerateFlowAttachmentReport());
        }

        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sendablePaths = new List<string>(Math.Min(attachments.Count, maxCount));
        var sent = new List<GenerateFlowAttachmentSentItem>();
        var skipped = new List<GenerateFlowAttachmentSkippedItem>();

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment))
                continue;

            var normalizedPath = attachment.Trim();
            if (!dedup.Add(normalizedPath))
                continue;

            var name = Path.GetFileName(normalizedPath);
            if (sendablePaths.Count >= maxCount)
            {
                skipped.Add(new GenerateFlowAttachmentSkippedItem
                {
                    Path = normalizedPath,
                    Name = name,
                    Reason = "limit_exceeded"
                });
                continue;
            }

            if (!File.Exists(normalizedPath))
            {
                skipped.Add(new GenerateFlowAttachmentSkippedItem
                {
                    Path = normalizedPath,
                    Name = name,
                    Reason = "file_missing"
                });
                continue;
            }

            var extension = Path.GetExtension(normalizedPath);
            if (!AiApiClient.IsSupportedImageExtension(extension))
            {
                skipped.Add(new GenerateFlowAttachmentSkippedItem
                {
                    Path = normalizedPath,
                    Name = name,
                    Reason = "unsupported_format"
                });
                continue;
            }

            try
            {
                var info = new FileInfo(normalizedPath);
                if (info.Length <= 0 || info.Length > maxImageBytes)
                {
                    skipped.Add(new GenerateFlowAttachmentSkippedItem
                    {
                        Path = normalizedPath,
                        Name = name,
                        Reason = "file_too_large"
                    });
                    continue;
                }

                using var stream = File.Open(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length <= 0)
                {
                    skipped.Add(new GenerateFlowAttachmentSkippedItem
                    {
                        Path = normalizedPath,
                        Name = name,
                        Reason = "read_failed"
                    });
                    continue;
                }
            }
            catch
            {
                skipped.Add(new GenerateFlowAttachmentSkippedItem
                {
                    Path = normalizedPath,
                    Name = name,
                    Reason = "read_failed"
                });
                continue;
            }

            sendablePaths.Add(normalizedPath);
            sent.Add(new GenerateFlowAttachmentSentItem
            {
                Path = normalizedPath,
                Name = name
            });
        }

        return new AttachmentSelectionResult(
            sendablePaths,
            new GenerateFlowAttachmentReport
            {
                Sent = sent,
                Skipped = skipped
            });
    }

    private static List<string> NormalizeAttachmentPaths(IReadOnlyList<string>? attachments, int maxCount)
    {
        if (attachments == null || attachments.Count == 0 || maxCount <= 0)
            return new List<string>();

        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedPaths = new List<string>(Math.Min(attachments.Count, maxCount));

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment))
                continue;

            var normalized = attachment.Trim();
            if (!dedup.Add(normalized))
                continue;

            normalizedPaths.Add(normalized);
            if (normalizedPaths.Count >= maxCount)
                break;
        }

        return normalizedPaths;
    }

    private static bool IsBadRequestHttpException(Exception ex)
    {
        if (ex is not HttpRequestException httpEx)
            return false;

        if (httpEx.StatusCode == HttpStatusCode.BadRequest)
            return true;

        return httpEx.Message.Contains("400", StringComparison.OrdinalIgnoreCase);
    }

    private static GenerateFlowAttachmentReport BuildFallbackAttachmentReport(GenerateFlowAttachmentReport originalReport, string reason)
    {
        var skipped = new List<GenerateFlowAttachmentSkippedItem>(originalReport.Skipped);
        foreach (var sent in originalReport.Sent)
        {
            skipped.Add(new GenerateFlowAttachmentSkippedItem
            {
                Path = sent.Path,
                Name = sent.Name,
                Reason = reason
            });
        }

        return new GenerateFlowAttachmentReport
        {
            Sent = new List<GenerateFlowAttachmentSentItem>(),
            Skipped = skipped
        };
    }

    private string BuildAttachmentContext(IReadOnlyList<string>? attachments)
    {
        var normalizedPaths = NormalizeAttachmentPaths(attachments, maxCount: 8);
        if (normalizedPaths.Count == 0)
            return string.Empty;

        var lines = new List<string>(normalizedPaths.Count);
        for (var i = 0; i < normalizedPaths.Count; i++)
        {
            lines.Add($"{i + 1}. {DescribeAttachment(normalizedPaths[i])}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string DescribeAttachment(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (!File.Exists(filePath))
            return $"{fileName} | path={filePath} | status=missing";

        try
        {
            var fileInfo = new FileInfo(filePath);
            var extension = string.IsNullOrWhiteSpace(fileInfo.Extension)
                ? "unknown"
                : fileInfo.Extension.TrimStart('.').ToLowerInvariant();

            var imageSize = TryGetImageSize(filePath);
            var sizeText = FormatByteSize(fileInfo.Length);

            if (imageSize.HasValue)
            {
                var (width, height) = imageSize.Value;
                return $"{fileName} | path={filePath} | type={extension} | size={sizeText} | resolution={width}x{height}";
            }

            return $"{fileName} | path={filePath} | type={extension} | size={sizeText} | resolution=unknown";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read attachment metadata: {AttachmentPath}", filePath);
            return $"{fileName} | path={filePath} | status=metadata_unavailable";
        }
    }

    private static (int Width, int Height)? TryGetImageSize(string filePath)
    {
        try
        {
            using var image = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            if (image.Empty())
                return null;

            return (image.Width, image.Height);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatByteSize(long byteSize)
    {
        if (byteSize < 1024)
            return $"{byteSize}B";
        if (byteSize < 1024 * 1024)
            return $"{(byteSize / 1024d).ToString("F1", CultureInfo.InvariantCulture)}KB";

        return $"{(byteSize / 1024d / 1024d).ToString("F2", CultureInfo.InvariantCulture)}MB";
    }

    private sealed record AttachmentSelectionResult(
        IReadOnlyList<string> SendablePaths,
        GenerateFlowAttachmentReport Report);

    private async Task<TemplatePriorityContext> BuildTemplatePriorityContextAsync(
        AiFlowGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var mergedText = $"{request.Description} {request.AdditionalContext}".Trim();
        if (!TryMatchTemplateFirstScenario(mergedText, out var matchedKeywords))
            return TemplatePriorityContext.None;

        IReadOnlyList<FlowTemplate> templates;
        try
        {
            templates = await _templateService.GetTemplatesAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load templates for template-first routing.");
            templates = Array.Empty<FlowTemplate>();
        }

        var selectedTemplate = SelectTemplateForWireScenario(templates, matchedKeywords);
        var reason = matchedKeywords.Count == 0
            ? "命中高频场景关键词"
            : $"命中关键词：{string.Join("、", matchedKeywords)}";
        var confidence = Math.Min(0.99, 0.62 + (matchedKeywords.Count * 0.08) + (selectedTemplate != null ? 0.1 : 0));

        return new TemplatePriorityContext(
            IsTemplateFirst: true,
            Template: selectedTemplate,
            MatchReason: reason,
            MatchMode: "template-first",
            Confidence: confidence,
            MatchedKeywords: matchedKeywords);
    }

    private static bool TryMatchTemplateFirstScenario(string text, out List<string> matchedKeywords)
    {
        matchedKeywords = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim().ToLowerInvariant();
        foreach (var keyword in _templateFirstKeywords)
        {
            if (normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matchedKeywords.Add(keyword);
            }
        }

        return matchedKeywords.Count > 0;
    }

    private static FlowTemplate? SelectTemplateForWireScenario(
        IReadOnlyList<FlowTemplate> templates,
        IReadOnlyCollection<string> matchedKeywords)
    {
        if (templates.Count == 0)
            return null;

        FlowTemplate? bestTemplate = null;
        var bestScore = int.MinValue;
        foreach (var template in templates)
        {
            var score = 0;
            foreach (var hint in _wireTemplateHints)
            {
                if (ContainsIgnoreCase(template.Name, hint))
                    score += 4;
                if (ContainsIgnoreCase(template.Description, hint))
                    score += 2;
                if ((template.Tags ?? new List<string>()).Any(tag => ContainsIgnoreCase(tag, hint)))
                    score += 3;
            }

            foreach (var keyword in matchedKeywords)
            {
                if (ContainsIgnoreCase(template.Name, keyword))
                    score += 2;
                if ((template.Tags ?? new List<string>()).Any(tag => ContainsIgnoreCase(tag, keyword)))
                    score += 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTemplate = template;
            }
        }

        return bestScore > 0 ? bestTemplate : null;
    }

    private static bool ContainsIgnoreCase(string? value, string? expected)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expected))
            return false;
        return value.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static AiRecommendedTemplateInfo? ResolveRecommendedTemplate(
        AiGeneratedFlowJson generatedFlow,
        TemplatePriorityContext templatePriority)
    {
        var modelTemplate = generatedFlow.RecommendedTemplate;
        if (modelTemplate != null && !string.IsNullOrWhiteSpace(modelTemplate.TemplateName))
        {
            modelTemplate.MatchMode = string.IsNullOrWhiteSpace(modelTemplate.MatchMode)
                ? "template-first"
                : modelTemplate.MatchMode;
            if (modelTemplate.Confidence <= 0)
                modelTemplate.Confidence = templatePriority.Confidence > 0 ? templatePriority.Confidence : 0.75;
            if (string.IsNullOrWhiteSpace(modelTemplate.MatchReason))
                modelTemplate.MatchReason = templatePriority.MatchReason;
            return modelTemplate;
        }

        if (!templatePriority.IsTemplateFirst)
            return null;

        return new AiRecommendedTemplateInfo
        {
            TemplateId = templatePriority.Template?.Id == Guid.Empty
                ? null
                : templatePriority.Template?.Id.ToString(),
            TemplateName = templatePriority.Template?.Name ?? "端子线序检测",
            MatchReason = templatePriority.MatchReason,
            MatchMode = templatePriority.MatchMode,
            Confidence = templatePriority.Confidence
        };
    }

    private static List<AiPendingParameterInfo> BuildPendingParameters(
        AiGeneratedFlowJson generatedFlow,
        IReadOnlyDictionary<string, string>? actualOperatorIdMap = null)
    {
        var merged = new Dictionary<string, (HashSet<string> Names, string ActualOperatorId)>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in generatedFlow.PendingParameters ?? new List<AiPendingParameterInfo>())
        {
            if (string.IsNullOrWhiteSpace(item.OperatorId))
                continue;

            if (!merged.TryGetValue(item.OperatorId, out var entry))
            {
                entry = (
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    ResolveActualOperatorId(item.OperatorId, item.ActualOperatorId, actualOperatorIdMap));
                merged[item.OperatorId] = entry;
            }
            else if (string.IsNullOrWhiteSpace(entry.ActualOperatorId))
            {
                merged[item.OperatorId] = (
                    entry.Names,
                    ResolveActualOperatorId(item.OperatorId, item.ActualOperatorId, actualOperatorIdMap));
                entry = merged[item.OperatorId];
            }

            foreach (var name in item.ParameterNames ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(name))
                    entry.Names.Add(name);
            }
        }

        foreach (var pair in generatedFlow.ParametersNeedingReview ?? new Dictionary<string, List<string>>())
        {
            if (!merged.TryGetValue(pair.Key, out var entry))
            {
                entry = (
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    ResolveActualOperatorId(pair.Key, null, actualOperatorIdMap));
                merged[pair.Key] = entry;
            }

            foreach (var name in pair.Value)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    entry.Names.Add(name);
            }
        }

        return merged
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new AiPendingParameterInfo
            {
                OperatorId = item.Key,
                ActualOperatorId = item.Value.ActualOperatorId,
                ParameterNames = item.Value.Names.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();
    }

    private static string ResolveActualOperatorId(
        string pendingOperatorId,
        string? existingActualOperatorId,
        IReadOnlyDictionary<string, string>? actualOperatorIdMap)
    {
        if (!string.IsNullOrWhiteSpace(existingActualOperatorId))
            return existingActualOperatorId;

        if (actualOperatorIdMap != null &&
            actualOperatorIdMap.TryGetValue(pendingOperatorId, out var actualOperatorId) &&
            !string.IsNullOrWhiteSpace(actualOperatorId))
        {
            return actualOperatorId;
        }

        return string.Empty;
    }

    private static List<AiMissingResourceInfo> BuildMissingResources(
        AiGeneratedFlowJson generatedFlow,
        TemplatePriorityContext templatePriority)
    {
        var resources = new Dictionary<string, AiMissingResourceInfo>(StringComparer.OrdinalIgnoreCase);
        void AddResource(string type, string key, string description)
        {
            var resourceKey = $"{type}|{key}";
            if (resources.ContainsKey(resourceKey))
                return;

            resources[resourceKey] = new AiMissingResourceInfo
            {
                ResourceType = type,
                ResourceKey = key,
                Description = description
            };
        }

        foreach (var item in generatedFlow.MissingResources ?? new List<AiMissingResourceInfo>())
        {
            if (string.IsNullOrWhiteSpace(item.ResourceType) || string.IsNullOrWhiteSpace(item.ResourceKey))
                continue;

            AddResource(
                item.ResourceType.Trim(),
                item.ResourceKey.Trim(),
                string.IsNullOrWhiteSpace(item.Description) ? "缺少必要资源" : item.Description.Trim());
        }

        foreach (var op in generatedFlow.Operators ?? new List<AiGeneratedOperator>())
        {
            var parameters = op.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (op.OperatorType.Equals("DeepLearning", StringComparison.OrdinalIgnoreCase))
            {
                if (IsMissingParameter(parameters, "ModelPath"))
                {
                    AddResource("Model", "DeepLearning.ModelPath", "缺少可用模型文件路径");
                }
            }

            if (op.OperatorType.Contains("Communication", StringComparison.OrdinalIgnoreCase))
            {
                if (IsMissingParameter(parameters, "IpAddress"))
                {
                    AddResource("PLC", $"{op.OperatorType}.IpAddress", "缺少 PLC 通信地址");
                }

                if (IsMissingParameter(parameters, "Port"))
                {
                    AddResource("PLC", $"{op.OperatorType}.Port", "缺少 PLC 通信端口");
                }
            }
        }

        if (templatePriority.IsTemplateFirst && templatePriority.Template == null)
        {
            AddResource("Template", "WireSequence.Template", "当前未找到可直接复用的线序模板，请先保存模板资产");
        }

        return resources.Values.ToList();
    }

    private static bool IsMissingParameter(IReadOnlyDictionary<string, string> parameters, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!parameters.TryGetValue(key, out var value))
                continue;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            var normalized = value.Trim();
            if (!normalized.Equals("todo", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("tbd", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("your_", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("to_be_filled", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record TemplatePriorityContext(
        bool IsTemplateFirst,
        FlowTemplate? Template,
        string MatchReason,
        string MatchMode,
        double Confidence,
        IReadOnlyList<string> MatchedKeywords)
    {
        public static TemplatePriorityContext None { get; } =
            new(false, null, string.Empty, string.Empty, 0, Array.Empty<string>());
    }

    private string BuildRetryMessage(string originalMessage, AiValidationResult failedValidation, string? lastRawResponse)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Regenerate the workflow JSON using the same user request, but repair the previous attempt precisely instead of starting blindly.");
        sb.AppendLine();
        sb.AppendLine("Original request:");
        sb.AppendLine(originalMessage);
        sb.AppendLine();

        var repairTargets = BuildRepairTargets(failedValidation);
        if (repairTargets.Count > 0)
        {
            sb.AppendLine("Repair priorities:");
            foreach (var target in repairTargets.Take(4))
            {
                sb.AppendLine($"- {target}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Structured issues found in the previous attempt:");
        foreach (var issue in failedValidation.Diagnostics.Take(10))
        {
            var fieldText = issue.RelatedFields.Count > 0
                ? $" | fields: {string.Join(", ", issue.RelatedFields)}"
                : string.Empty;
            var repairHint = string.IsNullOrWhiteSpace(issue.RepairHint)
                ? string.Empty
                : $" | action: {issue.RepairHint}";
            sb.AppendLine($"- [{issue.Severity}/{issue.Category}/{issue.Code}] {issue.Message}{fieldText}{repairHint}");
        }

        if (failedValidation.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Warnings to improve if possible:");
            foreach (var warning in failedValidation.Warnings)
                sb.AppendLine($"- {warning}");
        }

        if (!string.IsNullOrWhiteSpace(lastRawResponse))
        {
            sb.AppendLine();
            sb.AppendLine("Previous assistant output summary:");
            sb.AppendLine(SummarizeLastOutput(lastRawResponse));
            sb.AppendLine();
            sb.AppendLine("Previous assistant output to fix:");
            sb.AppendLine("```json");
            sb.AppendLine(TrimRetryOutput(lastRawResponse));
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("Keep already valid operators, connections, and parameters unchanged where possible.");
        sb.AppendLine("Return a complete corrected JSON object only. Do not add explanations or markdown outside the JSON.");

        return sb.ToString();
    }

    private static string BuildFinalValidationErrorMessage(AiValidationResult? validation, int retryCount)
    {
        if (validation?.PrimaryError != null)
        {
            return $"AI generated workflow did not pass validation (retried {retryCount} times): " +
                   $"[{validation.PrimaryError.Category}/{validation.PrimaryError.Code}] {validation.PrimaryError.Message}";
        }

        return $"AI generated workflow did not pass validation (retried {retryCount} times): " +
               string.Join("; ", validation?.Errors ?? new List<string>());
    }

    private static List<AiAttemptDiagnostic> BuildAttemptDiagnostics(
        int attemptNumber,
        string stage,
        AiValidationResult validation,
        string? lastRawResponse)
    {
        return
        [
            new AiAttemptDiagnostic
            {
                AttemptNumber = attemptNumber,
                Stage = stage,
                Summary = BuildAttemptSummary(validation),
                OutputSummary = SummarizeLastOutput(lastRawResponse),
                Issues = validation.Diagnostics.Select(CloneDiagnostic).ToList()
            }
        ];
    }

    private static string BuildAttemptSummary(AiValidationResult validation)
    {
        var errorCount = validation.Diagnostics.Count(item => item.Severity == AiValidationSeverity.Error);
        var warningCount = validation.Diagnostics.Count(item => item.Severity == AiValidationSeverity.Warning);
        if (validation.PrimaryError != null)
        {
            return $"主失败点：[{validation.PrimaryError.Category}/{validation.PrimaryError.Code}] " +
                   $"{validation.PrimaryError.Message}（errors={errorCount}, warnings={warningCount}）";
        }

        if (warningCount > 0)
            return $"本轮无阻断性错误，但有 {warningCount} 条警告需要关注。";

        return "本轮未记录结构化诊断。";
    }

    private static AiFailureSummary BuildFailureSummary(
        AiValidationResult? validation,
        int retryCount,
        string message,
        string? lastRawResponse,
        string fallbackCode,
        string fallbackCategory)
    {
        var primary = validation?.PrimaryError;
        return new AiFailureSummary
        {
            Category = primary?.Category ?? fallbackCategory,
            Code = primary?.Code ?? fallbackCode,
            Message = message,
            RepairTarget = BuildRepairTargets(validation).FirstOrDefault()
                ?? "根据最近一次诊断修复工作流 JSON 后重试。",
            RetryCount = retryCount,
            LastOutputSummary = SummarizeLastOutput(lastRawResponse)
        };
    }

    private static List<string> BuildRepairTargets(AiValidationResult? validation)
    {
        if (validation == null)
            return new List<string>();

        return validation.Diagnostics
            .Where(item => item.Severity == AiValidationSeverity.Error)
            .Select(item => string.IsNullOrWhiteSpace(item.RepairHint) ? item.Message : item.RepairHint!)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TrimRetryOutput(string rawResponse)
    {
        const int maxLength = 6000;
        if (string.IsNullOrWhiteSpace(rawResponse))
            return string.Empty;

        var trimmed = rawResponse.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + "\n...<truncated>";
    }

    private static string TrimTemplateFlowJson(string flowJson)
    {
        const int maxLength = 8000;
        if (string.IsNullOrWhiteSpace(flowJson))
            return string.Empty;

        var trimmed = flowJson.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + "\n...<truncated>";
    }

    private void RecordFailureResponse(string sessionId, string errorMessage, string? lastRawResponse)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(errorMessage))
            return;

        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"上次生成失败：{errorMessage}");

        if (!string.IsNullOrWhiteSpace(lastRawResponse))
        {
            summary.AppendLine();
            summary.AppendLine("最近一次模型输出片段：");
            summary.AppendLine(TrimRetryOutput(lastRawResponse));
        }

        _conversationalFlowService.RecordAssistantResponse(sessionId, summary.ToString().Trim(), null);
    }

    private static string SummarizeLastOutput(string? lastRawResponse)
    {
        if (string.IsNullOrWhiteSpace(lastRawResponse))
            return "最近一次模型未返回可用正文。";

        var normalized = NormalizeJsonEnvelope(lastRawResponse);
        try
        {
            var parsed = JsonSerializer.Deserialize<AiGeneratedFlowJson>(normalized, _jsonOptions);
            if (parsed != null)
            {
                return $"最近一次输出包含 {parsed.Operators?.Count ?? 0} 个算子、" +
                       $"{parsed.Connections?.Count ?? 0} 条连线，说明文本长度 {parsed.Explanation?.Length ?? 0}。";
            }
        }
        catch
        {
            // fallback below
        }

        var trimmed = TrimRetryOutput(lastRawResponse)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        if (trimmed.Length > 160)
            trimmed = trimmed[..160] + "...";

        return $"最近一次输出未能解析为标准工作流 JSON，长度 {lastRawResponse.Trim().Length} 字符，片段：{trimmed}";
    }

    private static string NormalizeJsonEnvelope(string rawResponse)
    {
        var json = rawResponse.Trim();
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            json = json[7..];
        else if (json.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            json = json[3..];

        if (json.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            json = json[..^3];

        json = json.Trim();
        if (json.StartsWith("{", StringComparison.Ordinal))
            return json;

        var match = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Singleline);
        return match.Success ? match.Value : json;
    }

    private static AiValidationDiagnostic CloneDiagnostic(AiValidationDiagnostic source)
    {
        return new AiValidationDiagnostic
        {
            Severity = source.Severity,
            Code = source.Code,
            Category = source.Category,
            Message = source.Message,
            RelatedFields = source.RelatedFields.ToList(),
            OperatorId = source.OperatorId,
            ParameterName = source.ParameterName,
            SourceTempId = source.SourceTempId,
            SourcePortName = source.SourcePortName,
            TargetTempId = source.TargetTempId,
            TargetPortName = source.TargetPortName,
            RepairHint = source.RepairHint
        };
    }

    private AiGeneratedFlowJson? ParseAiResponse(string rawResponse)
    {
        try
        {
            _logger.LogDebug("AI 原始响应前 500 字符：{Preview}",
                rawResponse.Length > 500 ? rawResponse[..500] + "..." : rawResponse);

            // 清理可能的 Markdown 代码块包装
            var json = NormalizeJsonEnvelope(rawResponse);

            // 如果清理后仍不是以 { 开头，尝试从响应中提取 JSON 对象
            // （推理模型可能在 JSON 前后附带解释文字）
            if (!json.StartsWith("{"))
            {
                _logger.LogWarning("AI 响应不是纯 JSON，尝试提取嵌入的 JSON 对象...");
                var match = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    json = match.Value;
                    _logger.LogInformation("Extracted JSON object from AI response. Length={Length}", json.Length);
                }
                else
                {
                    _logger.LogError(null, "AI 响应中找不到 JSON 对象，响应内容前 200 字符：{Content}",
                        json.Length > 200 ? json.Substring(0, 200) : json);
                    return null;
                }
            }

            return JsonSerializer.Deserialize<AiGeneratedFlowJson>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("解析 AI 响应 JSON 失败：{Error}，响应内容前 300 字符：{Content}",
                ex.Message,
                rawResponse.Length > 300 ? rawResponse[..300] + "..." : rawResponse);
            return null;
        }
    }

    private (OperatorFlowDto Flow, Dictionary<string, string> ActualOperatorIdMap) ConvertToFlowDto(
        AiGeneratedFlowJson generated,
        string userDescription)
    {
        // tempId 鈫?(IdGuid, Metadata) 鐨勬槧灏?
        var opInfoMapping = new Dictionary<string, (Guid Id, OperatorMetadata Meta)>();

        // tempId 鈫?(InputPorts: Name->Guid, OutputPorts: Name->Guid)
        var portMapping = new Dictionary<string, (Dictionary<string, Guid> Inputs, Dictionary<string, Guid> Outputs)>();

        foreach (var op in generated.Operators)
        {
            var type = Enum.Parse<OperatorType>(op.OperatorType);
            var metadata = _operatorFactory.GetMetadata(type) ?? throw new InvalidOperationException($"Operator {type} is not registered.");
            var operatorId = Guid.NewGuid();
            opInfoMapping[op.TempId] = (operatorId, metadata);

            var inputPorts = new Dictionary<string, Guid>();
            foreach (var p in metadata.InputPorts)
                inputPorts[p.Name] = Guid.NewGuid();

            var outputPorts = new Dictionary<string, Guid>();
            foreach (var p in metadata.OutputPorts)
                outputPorts[p.Name] = Guid.NewGuid();

            portMapping[op.TempId] = (inputPorts, outputPorts);
        }

        var operators = generated.Operators.Select(op =>
        {
            var (operatorId, metadata) = opInfoMapping[op.TempId];
            var (inputs, outputs) = portMapping[op.TempId];

            return new OperatorDto
            {
                Id = operatorId,
                Name = op.DisplayName,
                Type = metadata.Type,
                X = 0, // 由 AutoLayoutService 填充
                Y = 0,
                IsEnabled = true,
                InputPorts = metadata.InputPorts.Select(p => new PortDto
                {
                    Id = inputs[p.Name],
                    Name = p.Name,
                    Direction = PortDirection.Input,
                    DataType = p.DataType,
                    IsRequired = p.IsRequired
                }).ToList(),
                OutputPorts = metadata.OutputPorts.Select(p => new PortDto
                {
                    Id = outputs[p.Name],
                    Name = p.Name,
                    Direction = PortDirection.Output,
                    DataType = p.DataType
                }).ToList(),
                Parameters = metadata.Parameters.Select(p => new ParameterDto
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    DisplayName = p.DisplayName,
                    Description = p.Description,
                    DataType = p.DataType,
                    DefaultValue = p.DefaultValue,
                    IsRequired = p.IsRequired,
                    Options = p.Options?.Select(opt => new Acme.Product.Core.ValueObjects.ParameterOption
                    {
                        Label = opt.Label,
                        Value = opt.Value
                    }).ToList(),
                    Value = op.Parameters.TryGetValue(p.Name, out var val) ? val : null
                }).ToList()
            };
        }).ToList();

        var connections = generated.Connections?.Select(conn =>
        {
            // 源端口必须从 OutputPorts 查找
            var outputs = portMapping[conn.SourceTempId].Outputs;
            if (!outputs.TryGetValue(conn.SourcePortName, out var srcPortId))
            {
                throw new InvalidOperationException(
                   $"源算子 {conn.SourceTempId} 不存在输出端口 '{conn.SourcePortName}'");
            }

            // 目标端口必须从 InputPorts 查找
            var inputs = portMapping[conn.TargetTempId].Inputs;
            if (!inputs.TryGetValue(conn.TargetPortName, out var tgtPortId))
            {
                throw new InvalidOperationException(
                    $"目标算子 {conn.TargetTempId} 不存在输入端口 '{conn.TargetPortName}'");
            }

            return new OperatorConnectionDto
            {
                Id = Guid.NewGuid(),
                SourceOperatorId = opInfoMapping[conn.SourceTempId].Id,
                SourcePortId = srcPortId,
                TargetOperatorId = opInfoMapping[conn.TargetTempId].Id,
                TargetPortId = tgtPortId
            };
        }).ToList() ?? new List<OperatorConnectionDto>();

        return (
            new OperatorFlowDto
            {
                Id = Guid.NewGuid(),
                Name = $"AI生成 - {userDescription}",
                Operators = operators,
                Connections = connections
            },
            opInfoMapping.ToDictionary(
                item => item.Key,
                item => item.Value.Id.ToString(),
                StringComparer.OrdinalIgnoreCase));
    }

    private OperatorFlow ConvertDtoToEntity(OperatorFlowDto dto)
    {
        // 简单转换为用于测试跑分的内部结构
        var flow = new OperatorFlow(dto.Name);
        typeof(OperatorFlow).GetProperty("Id")?.SetValue(flow, dto.Id);

        flow.Operators = dto.Operators.Select(o =>
        {
            var op = _operatorFactory.CreateOperator(o.Type, o.Name, o.X, o.Y);
            typeof(Operator).GetProperty("Id")?.SetValue(op, o.Id);

            // 简单复制核心参数
            foreach (var pDto in o.Parameters)
            {
                var targetParam = op.Parameters.FirstOrDefault(p => p.Name == pDto.Name);
                if (targetParam != null && pDto.Value != null)
                    targetParam.SetValue(pDto.Value);
            }
            return op;
        }).ToList();

        flow.Connections = dto.Connections.Select(c =>
        {
            var conn = new Acme.Product.Core.ValueObjects.OperatorConnection(c.SourceOperatorId, c.SourcePortId, c.TargetOperatorId, c.TargetPortId);
            typeof(Acme.Product.Core.ValueObjects.OperatorConnection).GetProperty("Id")?.SetValue(conn, c.Id);
            return conn;
        }).ToList();

        return flow;
    }
}


