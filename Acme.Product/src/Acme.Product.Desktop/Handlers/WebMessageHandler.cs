// WebMessageHandler.cs
// 发送事件到前端
// 作者：蘅芜君

using Acme.Product.Contracts.Messages;
using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Desktop.Extensions;
using Acme.Product.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acme.Product.Infrastructure.Services;
using System.Net.Http;
using System.Linq;

namespace Acme.Product.Desktop.Handlers;

/// <summary>
/// WebView2 消息处理器
/// </summary>
public class WebMessageHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOperatorFactory _operatorFactory;
    private readonly ILogger<WebMessageHandler> _logger;
    private WebView2? _webViewControl;
    private CoreWebView2? _webView;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public WebMessageHandler(
        IServiceScopeFactory scopeFactory,
        IOperatorFactory operatorFactory,
        ILogger<WebMessageHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _operatorFactory = operatorFactory;
        _logger = logger;
    }

    /// <summary>
    /// 处理消息（供 WebView2Host 调用）
    /// </summary>
    public async Task<WebMessageResponse> HandleAsync(WebMessage message)
    {
        try
        {
            _logger.LogInformation("[WebMessageHandler] 处理消息: {MessageType}", message.Type);

            var messageJson = ExtractCommandJson(message);

            switch (message.Type)
            {
                case nameof(ExecuteOperatorCommand):
                    await HandleExecuteOperatorCommand(messageJson);
                    break;
                case nameof(UpdateFlowCommand):
                    await HandleUpdateFlowCommand(messageJson);
                    break;
                case nameof(StartInspectionCommand):
                    await HandleStartInspectionCommand(messageJson);
                    break;
                case nameof(StopInspectionCommand):
                    await HandleStopInspectionCommand();
                    break;
                case nameof(PickFileCommand):
                    await HandlePickFileCommand(messageJson);
                    break;
                default:
                    return new WebMessageResponse { RequestId = message.Id, Success = false, Error = "未知消息类型" };
            }

            return new WebMessageResponse { RequestId = message.Id, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 处理消息失败");
            return new WebMessageResponse { RequestId = message.Id, Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// 初始化 WebView
    /// </summary>
    public void Initialize(WebView2 webViewControl)
    {
        if (webViewControl?.CoreWebView2 == null)
            throw new InvalidOperationException("WebView2 content is not initialized.");

        _webViewControl = webViewControl;
        _webView = webViewControl.CoreWebView2;
        _webView.WebMessageReceived += OnWebMessageReceived;
    }

    /// <summary>
    /// 处理收到的消息（同步事件处理器）
    /// </summary>
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // 使用SafeFireAndForget确保异步异常被捕获，避免async void问题
        HandleWebMessageAsync(e).SafeFireAndForget(_logger);
    }

    /// <summary>
    /// 异步处理Web消息
    /// </summary>
    private async Task HandleWebMessageAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var messageJson = e.WebMessageAsJson;
            string messageType = string.Empty;

            try
            {
                using (var doc = JsonDocument.Parse(messageJson))
                {
                    // 【修复】按优先级检查可能的消息类型字段
                    // 前端修复后使用 messageType，但保留对旧格式 type 的兼容
                    if (doc.RootElement.TryGetProperty("messageType", out var typeProp) ||
                        doc.RootElement.TryGetProperty("MessageType", out typeProp) ||
                        doc.RootElement.TryGetProperty("type", out typeProp) ||
                        doc.RootElement.TryGetProperty("Type", out typeProp))
                    {
                        messageType = typeProp.GetString() ?? string.Empty;
                    }
                }
            }
            catch (JsonException)
            {
                // 忽略非JSON消息
                return;
            }

            if (string.IsNullOrEmpty(messageType))
                return;

            _logger.LogInformation("[WebMessageHandler] 收到消息: {MessageType}", messageType);

            switch (messageType)
            {
                case nameof(ExecuteOperatorCommand):
                    await HandleExecuteOperatorCommand(messageJson);
                    break;

                case nameof(UpdateFlowCommand):
                    await HandleUpdateFlowCommand(messageJson);
                    break;

                case nameof(StartInspectionCommand):
                    await HandleStartInspectionCommand(messageJson);
                    break;

                case nameof(StopInspectionCommand):
                    await HandleStopInspectionCommand();
                    break;

                case nameof(PickFileCommand):
                    await HandlePickFileCommand(messageJson);
                    break;

                case "ListAiSessions":
                    await HandleListAiSessionsCommand();
                    break;

                case "GetAiSession":
                    await HandleGetAiSessionCommand(messageJson);
                    break;

                case "DeleteAiSession":
                    await HandleDeleteAiSessionCommand(messageJson);
                    break;

                case "GenerateFlow":
                    await HandleGenerateFlowCommand(messageJson);
                    break;

                case "handeye:solve":
                    await HandleHandEyeSolveCommand(messageJson);
                    break;

                case "handeye:save":
                    await HandleHandEyeSaveCommand(messageJson);
                    break;

                default:
                    _logger.LogWarning("[WebMessageHandler] 未知消息类型: {MessageType}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 处理消息失败");
        }
    }

    /// <summary>
    /// 处理执行算子命令
    /// </summary>
    private async Task HandleExecuteOperatorCommand(string messageJson)
    {
        var command = JsonSerializer.Deserialize<ExecuteOperatorCommand>(messageJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (command == null)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var flowService = scope.ServiceProvider.GetRequiredService<IFlowExecutionService>();
            var op = await ResolveOperatorAsync(scope.ServiceProvider, command.OperatorId);

            var result = await flowService.ExecuteOperatorAsync(op, NormalizeDictionary(command.Inputs));

            var eventData = new OperatorExecutedEvent
            {
                OperatorId = command.OperatorId,
                OperatorName = op.Name,
                IsSuccess = result.IsSuccess,
                OutputData = result.OutputData,
                ExecutionTimeMs = result.ExecutionTimeMs,
                ErrorMessage = result.ErrorMessage
            };

            SendEvent(eventData);
        }
        catch (Exception ex)
        {
            var eventData = new OperatorExecutedEvent
            {
                OperatorId = command.OperatorId,
                OperatorName = "Unknown",
                IsSuccess = false,
                ErrorMessage = ex.Message
            };

            SendEvent(eventData);
        }
    }

    /// <summary>
    /// 处理更新流程命令
    /// </summary>
    private async Task HandleUpdateFlowCommand(string messageJson)
    {
        var command = JsonSerializer.Deserialize<UpdateFlowCommand>(messageJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (command?.Flow == null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var updateRequest = BuildUpdateFlowRequest(command.Flow);

        _logger.LogInformation(
            "[WebMessageHandler] 流程更新请求: ProjectId={ProjectId}, OperatorCount={OperatorCount}, ConnectionCount={ConnectionCount}",
            command.ProjectId,
            updateRequest.Operators.Count,
            updateRequest.Connections.Count);

        await projectService.UpdateFlowAsync(command.ProjectId, updateRequest);

        _logger.LogInformation("[WebMessageHandler] 流程已更新: {ProjectId}", command.ProjectId);
    }

    /// <summary>
    /// 处理开始检测命令
    /// </summary>
    private async Task HandleStartInspectionCommand(string messageJson)
    {
        var command = JsonSerializer.Deserialize<StartInspectionCommand>(messageJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (command == null)
            return;

        try
        {
            // 创建 Scope
            using var scope = _scopeFactory.CreateScope();
            var inspectionService = scope.ServiceProvider.GetRequiredService<IInspectionService>();

            byte[]? imageData = null;

            if (!string.IsNullOrEmpty(command.ImageBase64))
            {
                imageData = Convert.FromBase64String(command.ImageBase64);
            }

            // 执行检测
            var result = imageData != null
                ? await inspectionService.ExecuteSingleAsync(command.ProjectId, imageData)
                : await inspectionService.ExecuteSingleAsync(command.ProjectId, command.CameraId ?? "default");

            NotifyInspectionResult(result, command.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 检测失败");
        }
    }

    /// <summary>
    /// 处理停止检测命令
    /// </summary>
    private Task HandleStopInspectionCommand()
    {
        throw new NotSupportedException(
            "StopInspectionCommand 已停用。请改用 /api/inspection/realtime/stop HTTP 接口以避免假成功。");
    }

    private static string ExtractCommandJson(WebMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Payload))
        {
            return message.Payload;
        }

        return JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private async Task<Operator> ResolveOperatorAsync(IServiceProvider serviceProvider, Guid operatorId)
    {
        var projectRepository = serviceProvider.GetRequiredService<IProjectRepository>();
        var projectService = serviceProvider.GetRequiredService<ProjectService>();

        foreach (var project in await projectRepository.GetAllAsync())
        {
            var projectDto = await projectService.GetByIdAsync(project.Id);
            var operatorDto = projectDto?.Flow?.Operators?.FirstOrDefault(op => op.Id == operatorId);
            if (operatorDto != null)
            {
                var flowDto = new OperatorFlowDto
                {
                    Name = projectDto?.Flow?.Name ?? "WebMessageFlow",
                    Operators = new List<OperatorDto> { operatorDto }
                };

                return flowDto.ToEntity().Operators.Single();
            }
        }

        var dbContext = serviceProvider.GetRequiredService<VisionDbContext>();
        var databaseOperator = await dbContext.Operators
            .Include(op => op.InputPorts)
            .Include(op => op.OutputPorts)
            .Include(op => op.Parameters)
            .FirstOrDefaultAsync(op => op.Id == operatorId);

        return databaseOperator
            ?? throw new KeyNotFoundException($"未找到算子 {operatorId}");
    }

    private UpdateFlowRequest BuildUpdateFlowRequest(FlowData flowData)
    {
        var operators = flowData.Operators.Select(BuildOperatorDto).ToList();
        var operatorsById = operators.ToDictionary(op => op.Id);
        var connections = new List<OperatorConnectionDto>();

        foreach (var connection in flowData.Connections)
        {
            if (!operatorsById.TryGetValue(connection.SourceOperatorId, out var sourceOperator))
            {
                throw new InvalidOperationException($"流程中不存在源算子 {connection.SourceOperatorId}");
            }

            if (!operatorsById.TryGetValue(connection.TargetOperatorId, out var targetOperator))
            {
                throw new InvalidOperationException($"流程中不存在目标算子 {connection.TargetOperatorId}");
            }

            var sourcePort = sourceOperator.OutputPorts.FirstOrDefault(port =>
                string.Equals(port.Name, connection.SourcePort, StringComparison.OrdinalIgnoreCase));
            if (sourcePort == null)
            {
                throw new InvalidOperationException(
                    $"算子 {sourceOperator.Name} 上不存在输出端口 {connection.SourcePort}");
            }

            var targetPort = targetOperator.InputPorts.FirstOrDefault(port =>
                string.Equals(port.Name, connection.TargetPort, StringComparison.OrdinalIgnoreCase));
            if (targetPort == null)
            {
                throw new InvalidOperationException(
                    $"算子 {targetOperator.Name} 上不存在输入端口 {connection.TargetPort}");
            }

            connections.Add(new OperatorConnectionDto
            {
                Id = Guid.NewGuid(),
                SourceOperatorId = sourceOperator.Id,
                SourcePortId = sourcePort.Id,
                TargetOperatorId = targetOperator.Id,
                TargetPortId = targetPort.Id
            });
        }

        return new UpdateFlowRequest
        {
            Operators = operators,
            Connections = connections
        };
    }

    private OperatorDto BuildOperatorDto(OperatorData operatorData)
    {
        if (!Enum.TryParse<OperatorType>(operatorData.Type, true, out var operatorType))
        {
            throw new InvalidOperationException($"不支持的算子类型: {operatorData.Type}");
        }

        var @operator = _operatorFactory.CreateOperator(
            operatorType,
            string.IsNullOrWhiteSpace(operatorData.Name) ? operatorType.ToString() : operatorData.Name,
            operatorData.X,
            operatorData.Y);

        typeof(Operator).GetProperty(nameof(Operator.Id))?.SetValue(@operator, operatorData.Id);

        if (operatorData.Parameters != null)
        {
            foreach (var (name, value) in operatorData.Parameters)
            {
                var parameter = @operator.Parameters.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

                if (parameter != null)
                {
                    parameter.SetValue(NormalizeJsonValue(value));
                }
            }
        }

        return new OperatorDto
        {
            Id = @operator.Id,
            Name = @operator.Name,
            Type = @operator.Type,
            X = operatorData.X,
            Y = operatorData.Y,
            InputPorts = @operator.InputPorts.Select(port => new PortDto
            {
                Id = port.Id,
                Name = port.Name,
                Direction = port.Direction,
                DataType = port.DataType,
                IsRequired = port.IsRequired
            }).ToList(),
            OutputPorts = @operator.OutputPorts.Select(port => new PortDto
            {
                Id = port.Id,
                Name = port.Name,
                Direction = port.Direction,
                DataType = port.DataType,
                IsRequired = port.IsRequired
            }).ToList(),
            Parameters = @operator.Parameters.Select(parameter => new ParameterDto
            {
                Id = parameter.Id,
                Name = parameter.Name,
                DisplayName = parameter.DisplayName,
                Description = parameter.Description,
                DataType = parameter.DataType,
                Value = parameter.Value,
                DefaultValue = parameter.DefaultValue,
                MinValue = parameter.MinValue,
                MaxValue = parameter.MaxValue,
                IsRequired = parameter.IsRequired,
                Options = parameter.Options
            }).ToList(),
            IsEnabled = @operator.IsEnabled,
            ExecutionStatus = @operator.ExecutionStatus,
            ExecutionTimeMs = @operator.ExecutionTimeMs,
            ErrorMessage = @operator.ErrorMessage
        };
    }

    private static Dictionary<string, object>? NormalizeDictionary(Dictionary<string, object>? values)
    {
        if (values == null)
        {
            return null;
        }

        return values.ToDictionary(
            item => item.Key,
            item => NormalizeJsonValue(item.Value) ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
    }

    private static object? NormalizeJsonValue(object? value)
    {
        return value switch
        {
            JsonElement element => NormalizeJsonElement(element),
            Dictionary<string, object> dictionary => NormalizeDictionary(dictionary),
            IEnumerable<object> sequence => sequence.Select(NormalizeJsonValue).ToList(),
            _ => value
        };
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeJsonElement(property.Value) ?? string.Empty),
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// 处理选择文件命令
    /// </summary>
    private async Task HandlePickFileCommand(string messageJson)
    {
        try
        {
            var command = JsonSerializer.Deserialize<PickFileCommand>(messageJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (command == null)
            {
                _logger.LogWarning("[WebMessageHandler] 无法解析 PickFileCommand");
                return;
            }

            // 【关键修复】在独立 STA 线程上显示文件对话框
            // 原因：OpenFileDialog.ShowDialog() 运行模态消息循环，会劫持 UI 线程，
            // 导致 WebView2 所需的 COM/IPC 消息无法泵送。WebView2 浏览器进程
            // 检测到宿主长时间无响应后会终止连接，引发崩溃。
            // 解决方案：在独立 STA 线程运行对话框，UI 线程完全不被阻塞。
            var (filePath, isCancelled) = await ShowFileDialogOnNewThreadAsync(command);

            var eventData = new FilePickedEvent
            {
                ParameterName = command.ParameterName,
                FilePath = filePath,
                IsCancelled = isCancelled
            };

            SendEvent(eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理文件选择命令失败");
        }
    }

    /// <summary>
    /// 在独立 STA 线程上显示文件选择对话框，避免阻塞 UI 线程和 WebView2 消息泵
    /// </summary>
    private Task<(string? filePath, bool isCancelled)> ShowFileDialogOnNewThreadAsync(PickFileCommand command)
    {
        var tcs = new TaskCompletionSource<(string?, bool)>();

        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new System.Windows.Forms.OpenFileDialog
                {
                    Filter = command.Filter,
                    Title = "选择文件"
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    tcs.SetResult((dialog.FileName, false));
                }
                else
                {
                    tcs.SetResult((null, true));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件选择对话框线程异常");
                tcs.SetResult((null, true));
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    /// <summary>
    /// 处理列出 AI 历史会话请求
    /// </summary>
    private Task HandleListAiSessionsCommand()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var conversationalFlowService = scope.ServiceProvider.GetRequiredService<Acme.Product.Infrastructure.AI.IConversationalFlowService>();
            var sessions = conversationalFlowService.ListSessions();

            SendProgressMessage("ListAiSessionsResult", new
            {
                success = true,
                sessions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 ListAiSessions 失败");
            SendProgressMessage("ListAiSessionsResult", new
            {
                success = false,
                errorMessage = $"获取历史会话失败：{ex.Message}",
                sessions = Array.Empty<Acme.Product.Infrastructure.AI.ConversationSessionSummary>()
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理获取单个 AI 历史会话请求
    /// </summary>
    private Task HandleGetAiSessionCommand(string messageJson)
    {
        try
        {
            var sessionId = ExtractSessionId(messageJson);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                SendProgressMessage("GetAiSessionResult", new
                {
                    success = false,
                    errorMessage = "缺少 sessionId"
                });
                return Task.CompletedTask;
            }

            using var scope = _scopeFactory.CreateScope();
            var conversationalFlowService = scope.ServiceProvider.GetRequiredService<Acme.Product.Infrastructure.AI.IConversationalFlowService>();
            var session = conversationalFlowService.GetSession(sessionId);

            if (session == null)
            {
                SendProgressMessage("GetAiSessionResult", new
                {
                    success = false,
                    errorMessage = "会话不存在"
                });
                return Task.CompletedTask;
            }

            SendProgressMessage("GetAiSessionResult", new
            {
                success = true,
                session
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 GetAiSession 失败");
            SendProgressMessage("GetAiSessionResult", new
            {
                success = false,
                errorMessage = $"读取会话失败：{ex.Message}"
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理删除 AI 历史会话请求
    /// </summary>
    private Task HandleDeleteAiSessionCommand(string messageJson)
    {
        try
        {
            var sessionId = ExtractSessionId(messageJson);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                SendProgressMessage("DeleteAiSessionResult", new
                {
                    success = false,
                    errorMessage = "缺少 sessionId"
                });
                return Task.CompletedTask;
            }

            using var scope = _scopeFactory.CreateScope();
            var conversationalFlowService = scope.ServiceProvider.GetRequiredService<Acme.Product.Infrastructure.AI.IConversationalFlowService>();
            var deleted = conversationalFlowService.DeleteSession(sessionId);
            if (!deleted)
            {
                SendProgressMessage("DeleteAiSessionResult", new
                {
                    success = false,
                    errorMessage = "会话不存在",
                    sessionId
                });
                return Task.CompletedTask;
            }

            SendProgressMessage("DeleteAiSessionResult", new
            {
                success = true,
                sessionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 DeleteAiSession 失败");
            SendProgressMessage("DeleteAiSessionResult", new
            {
                success = false,
                errorMessage = $"删除会话失败：{ex.Message}"
            });
        }

        return Task.CompletedTask;
    }

    private static string? ExtractSessionId(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (doc.RootElement.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object)
            {
                if (payload.TryGetProperty("sessionId", out var payloadSessionId) ||
                    payload.TryGetProperty("SessionId", out payloadSessionId))
                {
                    return payloadSessionId.GetString();
                }
            }

            if (doc.RootElement.TryGetProperty("sessionId", out var sessionId) ||
                doc.RootElement.TryGetProperty("SessionId", out sessionId))
            {
                return sessionId.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore invalid json
        }

        return null;
    }

    /// <summary>
    /// 处理 AI 生成工作流请求
    /// </summary>
    private async Task HandleGenerateFlowCommand(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var payload = doc.RootElement.GetProperty("payload");
            var description = payload.GetProperty("description").GetString() ?? "";
            var hint = payload.TryGetProperty("hint", out var hintElement)
                ? hintElement.GetString()
                : null;
            var sessionId = payload.TryGetProperty("sessionId", out var sessionElement)
                ? sessionElement.GetString()
                : null;
            var existingFlowJson = payload.TryGetProperty("existingFlowJson", out var flowElement)
                ? flowElement.ValueKind == JsonValueKind.String
                    ? flowElement.GetString()
                    : flowElement.GetRawText()
                : null;
            var attachments = payload.TryGetProperty("attachments", out var attachmentElement) &&
                              attachmentElement.ValueKind == JsonValueKind.Array
                ? attachmentElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Cast<string>()
                    .ToList()
                : null;

            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<Acme.Product.Infrastructure.AI.GenerateFlowMessageHandler>();

            // 在后台线程执行 AI 生成，避免 WebView2/UI 线程被流式解析和回调压满。
            var resultJson = await Task.Run(() => handler.HandleAsync(
                description,
                sessionId,
                existingFlowJson,
                hint,
                attachments,
                onMessage: (type, payload) =>
                {
                    // payload 已是 JSON 字符串，直接拼接外层 envelope，避免反序列化再序列化的额外开销。
                    var progressJson = $"{{\"messageType\":{JsonSerializer.Serialize(type)},\"payload\":{payload}}}";
                    PostWebMessageJson(progressJson);
                }));

            // 发回前端（原始 JSON）
            PostWebMessageJson(resultJson);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogWarning(ex, "AI 服务连接被拦截");
            SendProgressMessage("AiFirewallBlocked", new
            {
                message = "检测到网络连接被阻断（可能被防火墙拦截）",
                detail = ex.Message,
                timestamp = DateTime.Now
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "AI 服务请求超时");
            SendProgressMessage("AiFirewallBlocked", new
            {
                message = "AI 服务连接超时（可能被防火墙拦截）",
                detail = "请检查网络环境或代理设置",
                timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理 AI 生成请求失败");

            var errorResponse = new GenerateFlowResponse
            {
                Success = false,
                ErrorMessage = $"系统错误：{ex.Message}"
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            PostWebMessageJson(json);
        }
    }

    /// <summary>
    /// 处理手眼标定解算请求
    /// </summary>
    private async Task HandleHandEyeSolveCommand(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var payload = doc.RootElement.GetProperty("payload");

            var points = new List<CalibrationPoint>();
            foreach (var pointElement in payload.EnumerateArray())
            {
                points.Add(new CalibrationPoint
                {
                    PixelX = pointElement.GetProperty("pixelX").GetDouble(),
                    PixelY = pointElement.GetProperty("pixelY").GetDouble(),
                    PhysicalX = pointElement.GetProperty("physicalX").GetDouble(),
                    PhysicalY = pointElement.GetProperty("physicalY").GetDouble()
                });
            }

            using var scope = _scopeFactory.CreateScope();
            var calibService = scope.ServiceProvider.GetRequiredService<IHandEyeCalibrationService>();
            var result = await calibService.SolveAsync(points);

            // 发送结果回前端
            SendProgressMessage("handeye:solve:result", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理手眼标定解算失败");
            SendProgressMessage("handeye:solve:result", new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 处理手眼标定保存请求
    /// </summary>
    private async Task HandleHandEyeSaveCommand(string messageJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var payload = doc.RootElement.GetProperty("payload");
            var resultElement = payload.GetProperty("result");
            var fileName = payload.GetProperty("fileName").GetString() ?? "hand_eye_calib.json";

            var result = new HandEyeCalibrationResult
            {
                Success = resultElement.GetProperty("success").GetBoolean(),
                OriginX = resultElement.GetProperty("originX").GetDouble(),
                OriginY = resultElement.GetProperty("originY").GetDouble(),
                ScaleX = resultElement.GetProperty("scaleX").GetDouble(),
                ScaleY = resultElement.GetProperty("scaleY").GetDouble()
            };

            using var scope = _scopeFactory.CreateScope();
            var calibService = scope.ServiceProvider.GetRequiredService<IHandEyeCalibrationService>();
            var isSaved = await calibService.SaveCalibrationAsync(result, fileName);

            // 发送结果回前端
            SendProgressMessage("handeye:save:result", new { success = isSaved });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存手眼标定文件失败");
            SendProgressMessage("handeye:save:result", new { success = false, message = ex.Message });
        }
    }

    public void NotifyInspectionResult(InspectionResult result, Guid projectId)
    {
        try
        {
            Dictionary<string, object>? outputData = null;
            if (!string.IsNullOrEmpty(result.OutputDataJson))
            {
                try
                {
                    outputData = JsonSerializer.Deserialize<Dictionary<string, object>>(result.OutputDataJson, (JsonSerializerOptions?)null);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "[WebMessageHandler] 解析 OutputDataJson 失败: ResultId={ResultId}", result.Id);
                }
            }

            var message = new
            {
                type = "inspectionCompleted",
                messageType = "inspectionCompleted",
                resultId = result.Id,
                projectId,
                status = result.Status.ToString(),
                defects = result.Defects.Select(d => new
                {
                    type = d.Type.ToString(),
                    x = d.X,
                    y = d.Y,
                    width = d.Width,
                    height = d.Height,
                    confidence = d.ConfidenceScore,
                    description = d.Description ?? string.Empty
                }).ToList(),
                processingTimeMs = result.ProcessingTimeMs,
                outputImage = result.OutputImage != null ? Convert.ToBase64String(result.OutputImage) : null,
                outputData,
                outputDataJson = result.OutputDataJson
            };

            PostWebMessageJson(JsonSerializer.Serialize(message, _jsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 推送检测结果失败: ResultId={ResultId}", result.Id);
        }
    }

    /// <summary>
    /// 发送事件到前端
    /// </summary>
    private void SendEvent<T>(T eventData)
    {
        try
        {
            var json = JsonSerializer.Serialize(eventData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            PostWebMessageJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 发送事件失败");
        }
    }

    private void SendProgressMessage(string type, object payload)
    {
        var json = JsonSerializer.Serialize(new
        {
            messageType = type,
            payload
        }, _jsonOptions);

        PostWebMessageJson(json);
    }

    private void PostWebMessageJson(string json)
    {
        var webViewControl = _webViewControl;
        var webView = _webView;
        if (webViewControl == null || webView == null || webViewControl.IsDisposed)
            return;

        if (webViewControl.InvokeRequired)
        {
            _ = webViewControl.BeginInvoke(new Action(() =>
            {
                if (webViewControl.IsDisposed)
                    return;

                try
                {
                    webView.PostWebMessageAsJson(json);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[WebMessageHandler] 异步推送 WebMessage 失败");
                }
            }));
            return;
        }

        webView.PostWebMessageAsJson(json);
    }
}
