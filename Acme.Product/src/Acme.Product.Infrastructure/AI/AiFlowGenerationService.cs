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
        CancellationToken cancellationToken = default)
    {
        // 推送：构建提示词
        onProgress?.Invoke("正在分析需求并构建提示词...");
        var conversationContext = _conversationalFlowService.PrepareContext(request);
        var systemPrompt = _promptBuilder.BuildSystemPrompt(request.Description);
        var userMessage = BuildUserMessage(
            request,
            conversationContext.ExistingFlowJson,
            conversationContext.Intent,
            conversationContext.PromptContext);

        // 读取当前配置快照
        var options = _configStore.Get();

        AiGeneratedFlowJson? generatedFlow = null;
        AiValidationResult? lastValidation = null;
        int retryCount = 0;

        for (int attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("调用 AI API，第 {Attempt} 次尝试", attempt + 1);

                // 推送：正在调用 AI (带重试次数)
                if (attempt > 0)
                    onProgress?.Invoke($"AI 响应未通过校验或出错，正在重试（第 {attempt + 1}/{options.MaxRetries} 次）...");
                else
                    onProgress?.Invoke("正在请求 AI 模型生成方案...");

                // 构建完整的上下文消息
                var messages = new List<ChatMessage> { new ChatMessage("user", userMessage) };
                if (attempt > 0)
                {
                    messages.Add(new ChatMessage("user", BuildRetryMessage(userMessage, lastValidation!)));
                }

                // 调用 API (使用流式接口)
                var completionResult = await _apiClient.StreamCompleteAsync(
                    systemPrompt,
                    messages,
                    chunk => onStreamChunk?.Invoke(chunk),
                    null,
                    cancellationToken);
                var rawResponse = completionResult.Content;
                _logger.LogDebug("AI 原始响应长度：{Length}", rawResponse.Length);
                if (!string.IsNullOrEmpty(completionResult.Reasoning))
                {
                    _logger.LogDebug("AI 思维链：{Reasoning}", completionResult.Reasoning[..Math.Min(200, completionResult.Reasoning.Length)] + "...");
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

                    onProgress?.Invoke("正在进行 Dry-Run 沙盒安全校验与分支覆盖率统计...");

                    // S6-003: 转换并在虚拟沙盒中运行以收集覆盖率
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
                    ErrorMessage = "AI 生成超时，请检查网络连接或稍后重试。"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI API 调用失败");
                return new AiFlowGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"AI 服务调用失败：{ex.Message}"
                };
            }
        }

        // 所有重试均失败
        return new AiFlowGenerationResult
        {
            Success = false,
            ErrorMessage = $"AI 生成的工作流未通过校验（已重试 {retryCount} 次）：" +
                          string.Join("；", lastValidation?.Errors ?? new List<string>()),
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
        sb.AppendLine($"请根据以下描述生成工作流：");
        sb.AppendLine();
        sb.AppendLine(request.Description);

        if (!string.IsNullOrWhiteSpace(request.AdditionalContext))
        {
            sb.AppendLine();
            sb.AppendLine($"补充信息：{request.AdditionalContext}");
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
                sb.AppendLine("要求：仅增量修改用户明确提出的部分，未提及的节点与连线尽量保持不变。");
            }
            else if (intent == ConversationIntent.Explain)
            {
                sb.AppendLine("要求：保持 operators 与 connections 结构不变，重点完善 explanation 字段。");
            }
        }

        sb.AppendLine();
        sb.AppendLine("请严格按照规定的 JSON 格式输出，不要包含任何其他文字。");

        return sb.ToString();
    }

    private string BuildRetryMessage(string originalMessage, AiValidationResult failedValidation)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(originalMessage);
        sb.AppendLine();
        sb.AppendLine("【你上次的输出存在以下错误，请修正后重新生成】");
        foreach (var error in failedValidation.Errors)
            sb.AppendLine($"- {error}");
        sb.AppendLine();
        sb.AppendLine("请重新生成完整的 JSON，不要只修改部分内容。");

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
            // （推理模型可能在 JSON 前后加了解释文字）
            if (!json.StartsWith("{"))
            {
                _logger.LogWarning("AI 响应不是纯 JSON，尝试提取嵌入的 JSON 对象...");
                var match = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    json = match.Value;
                    _logger.LogInformation("成功从 AI 响应中提取出 JSON 对象（长度 {Length}）", json.Length);
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
        // tempId → (IdGuid, Metadata) 的映射
        var opInfoMapping = new Dictionary<string, (Guid Id, OperatorMetadata Meta)>();

        // tempId → (InputPorts: Name->Guid, OutputPorts: Name->Guid)
        var portMapping = new Dictionary<string, (Dictionary<string, Guid> Inputs, Dictionary<string, Guid> Outputs)>();

        foreach (var op in generated.Operators)
        {
            var type = Enum.Parse<OperatorType>(op.OperatorType);
            var metadata = _operatorFactory.GetMetadata(type) ?? throw new InvalidOperationException($"算子 {type} 未注册");
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

            // 简单复制一下核心参数
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
