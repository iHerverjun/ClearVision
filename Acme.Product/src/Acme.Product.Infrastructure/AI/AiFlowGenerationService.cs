using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.DTOs;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Acme.Product.Core.Entities;
using Acme.Product.Infrastructure.AI.DryRun;
using Acme.Product.Contracts.Messages;
using OpenCvSharp;
using System.Globalization;

namespace Acme.Product.Infrastructure.AI;

public class AiFlowGenerationService : IAiFlowGenerationService
{
    private readonly AiApiClient _apiClient;
    private readonly PromptBuilder _promptBuilder;
    private readonly IConversationalFlowService _conversationalFlowService;
    private readonly IAiFlowValidator _validator;
    private readonly AutoLayoutService _layoutService;
    private readonly IOperatorFactory _operatorFactory;
    private readonly AiConfigStore _configStore;
    private readonly DryRunService _dryRunService;
    private readonly Microsoft.Extensions.Logging.ILogger<AiFlowGenerationService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private const int MaxMultimodalAttachmentCount = 4;

    public AiFlowGenerationService(
        AiApiClient apiClient,
        PromptBuilder promptBuilder,
        IConversationalFlowService conversationalFlowService,
        IAiFlowValidator validator,
        AutoLayoutService layoutService,
        IOperatorFactory operatorFactory,
        AiConfigStore configStore,
        DryRunService dryRunService,
        Microsoft.Extensions.Logging.ILogger<AiFlowGenerationService> logger)
    {
        _apiClient = apiClient;
        _promptBuilder = promptBuilder;
        _conversationalFlowService = conversationalFlowService;
        _validator = validator;
        _layoutService = layoutService;
        _operatorFactory = operatorFactory;
        _configStore = configStore;
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
        // 鎺ㄩ€侊細鏋勫缓鎻愮ず璇?
        onProgress?.Invoke("姝ｅ湪鍒嗘瀽闇€姹傚苟鏋勫缓鎻愮ず璇?..");
        var conversationContext = _conversationalFlowService.PrepareContext(request);
        var systemPrompt = _promptBuilder.BuildSystemPrompt(request.Description);
        var userMessage = BuildUserMessage(
            request,
            conversationContext.ExistingFlowJson,
            conversationContext.Intent,
            conversationContext.PromptContext);
        var attachmentSelection = AnalyzeMultimodalAttachments(request.Attachments, MaxMultimodalAttachmentCount);
        if (request.Attachments is { Count: > 0 })
        {
            onAttachmentReport?.Invoke(attachmentSelection.Report);
        }
        var initialUserMessage = BuildUserChatMessage(userMessage, attachmentSelection.SendablePaths);

        // 璇诲彇褰撳墠閰嶇疆蹇収
        var options = _configStore.Get();

        AiGeneratedFlowJson? generatedFlow = null;
        AiValidationResult? lastValidation = null;
        int retryCount = 0;

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Calling AI API, attempt {Attempt}", attempt + 1);

                // 鎺ㄩ€侊細姝ｅ湪璋冪敤 AI (甯﹂噸璇曟鏁?
                if (attempt > 0)
                    onProgress?.Invoke($"AI 鍝嶅簲鏈€氳繃鏍￠獙鎴栧嚭閿欙紝姝ｅ湪閲嶈瘯锛堢 {attempt + 1}/{options.MaxRetries} 娆★級...");
                else
                    onProgress?.Invoke("姝ｅ湪璇锋眰 AI 妯″瀷鐢熸垚鏂规...");

                // 鏋勫缓瀹屾暣鐨勪笂涓嬫枃娑堟伅
                var messages = new List<ChatMessage> { initialUserMessage };
                if (attempt > 0)
                {
                    messages.Add(new ChatMessage("user", BuildRetryMessage(userMessage, lastValidation!)));
                }

                // 璋冪敤 API (浣跨敤娴佸紡鎺ュ彛)
                var completionResult = await _apiClient.StreamCompleteAsync(
                    systemPrompt,
                    messages,
                    chunk => onStreamChunk?.Invoke(chunk),
                    null,
                    cancellationToken);
                var rawResponse = completionResult.Content;
                _logger.LogDebug("AI 鍘熷鍝嶅簲闀垮害锛歿Length}", rawResponse.Length);
                if (!string.IsNullOrEmpty(completionResult.Reasoning))
                {
                    _logger.LogDebug("AI 鎬濈淮閾撅細{Reasoning}", completionResult.Reasoning[..Math.Min(200, completionResult.Reasoning.Length)] + "...");
                }

                // 鎺ㄩ€侊細瑙ｆ瀽缁撴灉
                onProgress?.Invoke("鏀跺埌 AI 鍝嶅簲锛屾鍦ㄨВ鏋?JSON 鏁版嵁...");
                // 瑙ｆ瀽 AI 杈撳嚭鐨?JSON
                generatedFlow = ParseAiResponse(rawResponse);
                if (generatedFlow == null)
                {
                    lastValidation = new AiValidationResult();
                    lastValidation.AddError("AI 杩斿洖鐨勫唴瀹逛笉鏄悎娉曠殑 JSON 鏍煎紡");
                    retryCount++;
                    continue;
                }

                // 鎺ㄩ€侊細鏍￠獙缁撴灉
                onProgress?.Invoke("姝ｅ湪鏍￠獙鐢熸垚鐨勭畻瀛愬拰鍙傛暟鏈夋晥鎬?..");
                // 鏍￠獙
                lastValidation = _validator.Validate(generatedFlow);
                if (lastValidation.IsValid)
                {
                    // 鏍￠獙閫氳繃锛岃浆鎹负 DTO 骞惰繑鍥?
                    var flowDto = ConvertToFlowDto(generatedFlow, request.Description);
                    _layoutService.ApplyLayout(flowDto);

                    onProgress?.Invoke("姝ｅ湪杩涜 Dry-Run 娌欑洅瀹夊叏鏍￠獙涓庡垎鏀鐩栫巼缁熻...");

                    // S6-003: 杞崲骞跺湪铏氭嫙娌欑洅涓繍琛屼互鏀堕泦瑕嗙洊鐜?
                    object? dryRunReport = null;
                    try
                    {
                        var flowEntity = ConvertDtoToEntity(flowDto); // 鏆傛椂闇€杞崲涓?Entity 渚涗豢鐪熶娇鐢?
                        var drResult = await _dryRunService.RunAsync(
                            flowEntity,
                            new Dictionary<string, object>(), // 绌鸿緭鍏?
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
                        _logger.LogWarning(ex, "DryRun 棰勬紨闃舵寮傚父锛岃烦杩囪鐩栫巼閲囬泦");
                    }

                    _conversationalFlowService.RecordAssistantResponse(
                        conversationContext.SessionId,
                        generatedFlow.Explanation,
                        JsonSerializer.Serialize(generatedFlow, _jsonOptions));

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
                        DryRunResult = dryRunReport
                    };
                }

                _logger.LogWarning("AI 鐢熸垚鍐呭鏍￠獙澶辫触锛岄敊璇細{Errors}",
                    string.Join("; ", lastValidation.Errors));
                retryCount++;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI API 璋冪敤瓒呮椂");
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = "AI generation timed out. Please retry."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI API 璋冪敤澶辫触");
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"AI service call failed: {ex.Message}"
                };
            }
        }

        // 鎵€鏈夐噸璇曞潎澶辫触
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
        string promptContext)
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
            sb.AppendLine("浼氳瘽涓婁笅鏂囷細");
            sb.AppendLine(promptContext);
        }

        if (!string.IsNullOrWhiteSpace(existingFlow))
        {
            sb.AppendLine();
            sb.AppendLine("浠ヤ笅鏄敤鎴峰綋鍓嶇殑宸ヤ綔娴?JSON锛岃鍦ㄦ鍩虹涓婂鐞嗭細");
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

    private static AttachmentSelectionResult AnalyzeMultimodalAttachments(IReadOnlyList<string>? attachments, int maxCount)
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
                if (info.Length <= 0 || info.Length > AiApiClient.MaxImageBytes)
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
            _logger.LogDebug("AI 鍘熷鍝嶅簲鍓?500 瀛楃锛歿Preview}",
                rawResponse.Length > 500 ? rawResponse[..500] + "..." : rawResponse);

            // 娓呯悊鍙兘鐨?Markdown 浠ｇ爜鍧楀寘瑁?
            var json = rawResponse.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                json = json[7..];
            else if (json.StartsWith("```"))
                json = json[3..];

            if (json.EndsWith("```"))
                json = json[..^3];

            json = json.Trim();

            // 濡傛灉娓呯悊鍚庝粛涓嶆槸浠?{ 寮€澶达紝灏濊瘯浠庡搷搴斾腑鎻愬彇 JSON 瀵硅薄
            // 锛堟帹鐞嗘ā鍨嬪彲鑳藉湪 JSON 鍓嶅悗鍔犱簡瑙ｉ噴鏂囧瓧锛?
            if (!json.StartsWith("{"))
            {
                _logger.LogWarning("AI 鍝嶅簲涓嶆槸绾?JSON锛屽皾璇曟彁鍙栧祵鍏ョ殑 JSON 瀵硅薄...");
                var match = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    json = match.Value;
                    _logger.LogInformation("Extracted JSON object from AI response. Length={Length}", json.Length);
                }
                else
                {
                    _logger.LogError(null, "AI 鍝嶅簲涓壘涓嶅埌 JSON 瀵硅薄锛屽搷搴斿唴瀹瑰墠 200 瀛楃锛歿Content}",
                        json.Length > 200 ? json.Substring(0, 200) : json);
                    return null;
                }
            }

            return JsonSerializer.Deserialize<AiGeneratedFlowJson>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("瑙ｆ瀽 AI 鍝嶅簲 JSON 澶辫触锛歿Error}锛屽搷搴斿唴瀹瑰墠 300 瀛楃锛歿Content}",
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
                X = 0, // 鐢?AutoLayoutService 濉厖
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
            // 婧愮鍙ｅ繀椤讳粠 OutputPorts 鏌ユ壘
            var outputs = portMapping[conn.SourceTempId].Outputs;
            if (!outputs.TryGetValue(conn.SourcePortName, out var srcPortId))
            {
                throw new InvalidOperationException(
                   $"婧愮畻瀛?{conn.SourceTempId} 涓嶅瓨鍦ㄨ緭鍑虹鍙?'{conn.SourcePortName}'");
            }

            // 鐩爣绔彛蹇呴』浠?InputPorts 鏌ユ壘
            var inputs = portMapping[conn.TargetTempId].Inputs;
            if (!inputs.TryGetValue(conn.TargetPortName, out var tgtPortId))
            {
                throw new InvalidOperationException(
                    $"鐩爣绠楀瓙 {conn.TargetTempId} 涓嶅瓨鍦ㄨ緭鍏ョ鍙?'{conn.TargetPortName}'");
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
            Name = $"AI鐢熸垚 - {userDescription}",
            Operators = operators,
            Connections = connections
        };
    }

    private OperatorFlow ConvertDtoToEntity(OperatorFlowDto dto)
    {
        // 绠€鍗曡浆鎹负鐢ㄤ簬娴嬭瘯璺戝垎鐨勫唴閮ㄧ粨鏋?
        var flow = new OperatorFlow(dto.Name);
        typeof(OperatorFlow).GetProperty("Id")?.SetValue(flow, dto.Id);

        flow.Operators = dto.Operators.Select(o =>
        {
            var op = _operatorFactory.CreateOperator(o.Type, o.Name, o.X, o.Y);
            typeof(Operator).GetProperty("Id")?.SetValue(op, o.Id);

            // 绠€鍗曞鍒朵竴涓嬫牳蹇冨弬鏁?
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

