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
        PropertyNameCaseInsensitive = true
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
        int retryCount = 0;

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Calling AI API, attempt {Attempt}", attempt + 1);

                // 推送：正在调用 AI（带重试次数）
                if (attempt > 0)
                    onProgress?.Invoke($"AI 响应未通过校验或出错，正在重试（第 {attempt + 1}/{options.MaxRetries} 次）...");
                else
                    onProgress?.Invoke("正在请求 AI 模型生成方案...");

                // 构建完整的上下文消息
                var messages = new List<ChatMessage> { currentUserMessage };
                if (attempt > 0)
                {
                    messages.Add(new ChatMessage("user", BuildRetryMessage(userMessage, lastValidation!)));
                }

                // 调用 API（使用流式接口）
                var completionResult = await _aiOrchestrator.StreamCompleteAsync(
                    systemPrompt,
                    messages,
                    chunk => onStreamChunk?.Invoke(chunk),
                    activeModel,
                    cancellationToken);
                var rawResponse = completionResult.Content;
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
                    lastValidation.AddError("AI 返回的内容不是合法的 JSON 格式");
                    retryCount++;
                    continue;
                }

                // 推送：校验结果
                onProgress?.Invoke("正在校验生成的算子和参数有效性...");
                // 校验
                lastValidation = _validator.Validate(generatedFlow);
                if (lastValidation.IsValid)
                {
                    // 校验通过，转换为 DTO 并返回
                    var flowDto = ConvertToFlowDto(generatedFlow, request.Description);
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

                    _conversationalFlowService.RecordAssistantResponse(
                        conversationContext.SessionId,
                        generatedFlow.Explanation,
                        JsonSerializer.Serialize(generatedFlow, _jsonOptions),
                        JsonSerializer.Serialize(flowDto, _jsonOptions));

                    var recommendedTemplate = ResolveRecommendedTemplate(generatedFlow, templatePriority);
                    var pendingParameters = BuildPendingParameters(generatedFlow);
                    var missingResources = BuildMissingResources(generatedFlow, templatePriority);

                    return new AiFlowGenerationResult
                    {
                        Success = true,
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
                _logger.LogWarning("AI API 调用超时");
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = "AI generation timed out. Please retry."
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
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"AI service call failed: {ex.Message}"
                };
            }
        }

        // 所有重试均失败
        return new AiFlowGenerationResult
        {
            Success = false,
            ErrorMessage = $"AI generated workflow did not pass validation (retried {retryCount} times): " +
                          string.Join("; ", lastValidation?.Errors ?? new List<string>()),
            RetryCount = retryCount
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

    private static List<AiPendingParameterInfo> BuildPendingParameters(AiGeneratedFlowJson generatedFlow)
    {
        var merged = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in generatedFlow.PendingParameters ?? new List<AiPendingParameterInfo>())
        {
            if (string.IsNullOrWhiteSpace(item.OperatorId))
                continue;

            if (!merged.TryGetValue(item.OperatorId, out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                merged[item.OperatorId] = names;
            }

            foreach (var name in item.ParameterNames ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }

        foreach (var pair in generatedFlow.ParametersNeedingReview ?? new Dictionary<string, List<string>>())
        {
            if (!merged.TryGetValue(pair.Key, out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                merged[pair.Key] = names;
            }

            foreach (var name in pair.Value)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }

        return merged
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item => new AiPendingParameterInfo
            {
                OperatorId = item.Key,
                ParameterNames = item.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();
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

                if (IsMissingParameter(parameters, "LabelsPath"))
                {
                    AddResource("Label", "DeepLearning.LabelsPath", "缺少标签文件路径");
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

    private static bool IsMissingParameter(IReadOnlyDictionary<string, string> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
            return true;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim();
        return normalized.Equals("todo", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("tbd", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("your_", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("to_be_filled", StringComparison.OrdinalIgnoreCase);
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

    private string BuildRetryMessage(string originalMessage, AiValidationResult failedValidation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(originalMessage);
        sb.AppendLine();
        sb.AppendLine("Your previous output has validation errors. Please regenerate with fixes.");
        foreach (var error in failedValidation.Errors)
            sb.AppendLine($"- {error}");
        sb.AppendLine();
        sb.AppendLine("Regenerate the complete JSON instead of partial modifications.");

        return sb.ToString();
    }

    private AiGeneratedFlowJson? ParseAiResponse(string rawResponse)
    {
        try
        {
            _logger.LogDebug("AI 原始响应前 500 字符：{Preview}",
                rawResponse.Length > 500 ? rawResponse[..500] + "..." : rawResponse);

            // 清理可能的 Markdown 代码块包装
            var json = rawResponse.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                json = json[7..];
            else if (json.StartsWith("```"))
                json = json[3..];

            if (json.EndsWith("```"))
                json = json[..^3];

            json = json.Trim();

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

    private OperatorFlowDto ConvertToFlowDto(AiGeneratedFlowJson generated, string userDescription)
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

        return new OperatorFlowDto
        {
            Id = Guid.NewGuid(),
            Name = $"AI生成 - {userDescription}",
            Operators = operators,
            Connections = connections
        };
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


