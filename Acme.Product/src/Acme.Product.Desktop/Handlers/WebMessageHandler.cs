// WebMessageHandler.cs
// 鍙戦€佷簨浠跺埌鍓嶇
// 浣滆€咃細铇呰姕鍚?

using Acme.Product.Contracts.Messages;
using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.Interfaces;
using Acme.Product.Desktop.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;
using Acme.Product.Desktop.Handlers;
using Acme.Product.Infrastructure.Services;
using System.Net.Http;

namespace Acme.Product.Desktop.Handlers;

/// <summary>
/// WebView2 娑堟伅澶勭悊鍣?
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
        WriteIndented = false
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
    /// 澶勭悊娑堟伅锛堜緵 WebView2Host 璋冪敤锛?
    /// </summary>
    public async Task<WebMessageResponse> HandleAsync(WebMessage message)
    {
        try
        {
            _logger.LogInformation("[WebMessageHandler] 澶勭悊娑堟伅: {MessageType}", message.Type);

            var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

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
                    return new WebMessageResponse { RequestId = message.Id, Success = false, Error = "鏈煡娑堟伅绫诲瀷" };
            }

            return new WebMessageResponse { RequestId = message.Id, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 澶勭悊娑堟伅澶辫触");
            return new WebMessageResponse { RequestId = message.Id, Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// 鍒濆鍖?WebView
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
    /// 澶勭悊鏀跺埌鐨勬秷鎭紙鍚屾浜嬩欢澶勭悊鍣級
    /// </summary>
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // 浣跨敤SafeFireAndForget纭繚寮傛寮傚父琚崟鑾凤紝閬垮厤async void闂
        HandleWebMessageAsync(e).SafeFireAndForget(_logger);
    }

    /// <summary>
    /// 寮傛澶勭悊Web娑堟伅
    /// </summary>
    private async Task HandleWebMessageAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var messageJson = e.WebMessageAsJson;
            string messageType = string.Empty;
            int? requestId = null;

            try
            {
                using (var doc = JsonDocument.Parse(messageJson))
                {
                    // 銆愪慨澶嶃€戞寜浼樺厛绾ф鏌ュ彲鑳界殑娑堟伅绫诲瀷瀛楁
                    // 鍓嶇淇鍚庝娇鐢?messageType锛屼絾淇濈暀瀵规棫鏍煎紡 type 鐨勫吋瀹?
                    if (doc.RootElement.TryGetProperty("messageType", out var typeProp) ||
                        doc.RootElement.TryGetProperty("MessageType", out typeProp) ||
                        doc.RootElement.TryGetProperty("type", out typeProp) ||
                        doc.RootElement.TryGetProperty("Type", out typeProp))
                    {
                        messageType = typeProp.GetString() ?? string.Empty;
                    }

                    if (doc.RootElement.TryGetProperty("requestId", out var requestIdProp) &&
                        requestIdProp.ValueKind == JsonValueKind.Number &&
                        requestIdProp.TryGetInt32(out var parsedRequestId))
                    {
                        requestId = parsedRequestId;
                    }
                }
            }
            catch (JsonException)
            {
                // 蹇界暐闈濲SON娑堟伅
                return;
            }

            if (string.IsNullOrEmpty(messageType))
                return;

            _logger.LogInformation("[WebMessageHandler] 鏀跺埌娑堟伅: {MessageType}", messageType);

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

                case "GenerateFlow":
                case "GenerateFlowCommand":
                    await HandleGenerateFlowCommand(messageJson, requestId);
                    break;

                case "settings.get":
                    await HandleSettingsGetQuery(requestId);
                    break;

                case "settings.save":
                    await HandleSettingsSaveCommand(messageJson, requestId);
                    break;

                case "handeye:solve":
                    await HandleHandEyeSolveCommand(messageJson);
                    break;

                case "handeye:save":
                    await HandleHandEyeSaveCommand(messageJson);
                    break;

                case "SystemStatsQuery":
                    await HandleSystemStatsQuery(requestId);
                    break;

                case "HardwareStatusQuery":
                    await HandleHardwareStatusQuery(requestId);
                    break;

                case "ActivityLogQuery":
                    await HandleActivityLogQuery(messageJson, requestId);
                    break;

                case "ProjectListQuery":
                    await HandleProjectListQuery(requestId);
                    break;

                case "ProjectCreateCommand":
                    await HandleProjectCreateCommand(messageJson, requestId);
                    break;

                case "ProjectDeleteCommand":
                    await HandleProjectDeleteCommand(messageJson, requestId);
                    break;

                case "ProjectOpenCommand":
                    await HandleProjectOpenCommand(messageJson, requestId);
                    break;

                case "ResultsQuery":
                    await HandleResultsQuery(messageJson, requestId);
                    break;

                case "ResultsExportCommand":
                    await HandleResultsExportCommand(messageJson, requestId);
                    break;

                case "FalsePositiveCommand":
                    await HandleFalsePositiveCommand(messageJson, requestId);
                    break;

                default:
                    _logger.LogWarning("[WebMessageHandler] 鏈煡娑堟伅绫诲瀷: {MessageType}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] 澶勭悊娑堟伅澶辫触");
        }
    }

    /// <summary>
    /// 澶勭悊鎵ц绠楀瓙鍛戒护
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
            // 鍒涘缓 Scope
            using var scope = _scopeFactory.CreateScope();
            var flowService = scope.ServiceProvider.GetRequiredService<IFlowExecutionService>();

            // 鍒涘缓绠楀瓙瀹炰緥
            var op = _operatorFactory.CreateOperator(
                Enum.Parse<Core.Enums.OperatorType>(command.OperatorId.ToString()),
                "TempOperator",
                0, 0);

            // 鎵ц绠楀瓙
            var result = await flowService.ExecuteOperatorAsync(op, command.Inputs);

            // 鍙戦€佺粨鏋滀簨浠?
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
    /// 澶勭悊鏇存柊娴佺▼鍛戒护
    /// </summary>
    private async Task HandleUpdateFlowCommand(string messageJson)
    {
        var command = JsonSerializer.Deserialize<UpdateFlowCommand>(messageJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (command?.Flow == null)
            return;

        // 淇濆瓨娴佺▼鍒版暟鎹簱 - 閫氳繃FlowExecutionService鎴栦笓闂ㄧ殑娴佺▼绠＄悊鏈嶅姟
        // 瀹為檯瀹炵幇闇€瑕佹敞鍏ProjectRepository骞惰皟鐢║pdateFlowAsync
        // 杩欓噷鎻愪緵鍩虹瀹炵幇妗嗘灦
        _logger.LogInformation("娴佺▼鏇存柊璇锋眰: ProjectId={ProjectId}, OperatorCount={Count}",
            command.ProjectId, command.Flow.Operators?.Count ?? 0);

        await Task.CompletedTask;

        _logger.LogInformation("[WebMessageHandler] 娴佺▼宸叉洿鏂? {ProjectId}", command.ProjectId);
    }

    /// <summary>
    /// 澶勭悊寮€濮嬫娴嬪懡浠?
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
            // 鍒涘缓 Scope
            using var scope = _scopeFactory.CreateScope();
            var inspectionService = scope.ServiceProvider.GetRequiredService<IInspectionService>();

            byte[]? imageData = null;

            if (!string.IsNullOrEmpty(command.ImageBase64))
            {
                imageData = Convert.FromBase64String(command.ImageBase64);
            }

            // 鎵ц妫€娴?
            var result = imageData != null
                ? await inspectionService.ExecuteSingleAsync(command.ProjectId, imageData)
                : await inspectionService.ExecuteSingleAsync(command.ProjectId, command.CameraId ?? "default");

            // 鍙戦€佹娴嬪畬鎴愪簨浠?
            var eventData = new InspectionCompletedEvent
            {
                ResultId = result.Id,
                ProjectId = command.ProjectId,
                Status = result.Status.ToString(),
                Defects = result.Defects.Select(d => new Contracts.Messages.DefectData
                {
                    Type = d.Type.ToString(),
                    X = d.X,
                    Y = d.Y,
                    Width = d.Width,
                    Height = d.Height,
                    Confidence = d.ConfidenceScore,
                    Description = d.Description ?? string.Empty
                }).ToList(),
                ProcessingTimeMs = result.ProcessingTimeMs,
                ResultImageBase64 = result.OutputImage != null ? Convert.ToBase64String(result.OutputImage) : null,
                OutputData = string.IsNullOrEmpty(result.OutputDataJson)
                                ? null
                                : JsonSerializer.Deserialize<Dictionary<string, object>>(result.OutputDataJson, (JsonSerializerOptions?)null)
            };

            SendEvent(eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Inspection failed");
        }
    }

    /// <summary>
    /// 澶勭悊鍋滄妫€娴嬪懡浠?
    /// </summary>
    private async Task HandleStopInspectionCommand()
    {
        _logger.LogInformation("[WebMessageHandler] 妫€娴嬪凡鍋滄");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 澶勭悊閫夋嫨鏂囦欢鍛戒护
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
                _logger.LogWarning("[WebMessageHandler] 鏃犳硶瑙ｆ瀽 PickFileCommand");
                return;
            }

            // 銆愬叧閿慨澶嶃€戝湪鐙珛 STA 绾跨▼涓婃樉绀烘枃浠跺璇濇
            // 鍘熷洜锛歄penFileDialog.ShowDialog() 杩愯妯℃€佹秷鎭惊鐜紝浼氬姭鎸?UI 绾跨▼锛?
            // 瀵艰嚧 WebView2 鎵€闇€鐨?COM/IPC 娑堟伅鏃犳硶娉甸€併€俉ebView2 娴忚鍣ㄨ繘绋?
            // 妫€娴嬪埌瀹夸富闀挎椂闂存棤鍝嶅簲鍚庝細缁堟杩炴帴锛屽紩鍙戝穿婧冦€?
            // 瑙ｅ喅鏂规锛氬湪鐙珛 STA 绾跨▼杩愯瀵硅瘽妗嗭紝UI 绾跨▼瀹屽叏涓嶈闃诲銆?
            var (filePath, isCancelled) = await ShowFileDialogOnNewThreadAsync(command);

            string? previewImageBase64 = null;
            if (!isCancelled && command.IncludePreviewBase64 && !string.IsNullOrWhiteSpace(filePath))
            {
                previewImageBase64 = TryReadImagePreviewBase64(filePath);
            }

            var eventData = new FilePickedEvent
            {
                ParameterName = command.ParameterName,
                FilePath = filePath,
                IsCancelled = isCancelled,
                PreviewImageBase64 = previewImageBase64
            };

            SendEvent(eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "澶勭悊鏂囦欢閫夋嫨鍛戒护澶辫触");
        }
    }

    /// <summary>
    /// 鍦ㄧ嫭绔?STA 绾跨▼涓婃樉绀烘枃浠堕€夋嫨瀵硅瘽妗嗭紝閬垮厤闃诲 UI 绾跨▼鍜?WebView2 娑堟伅娉?
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
                    Title = "閫夋嫨鏂囦欢"
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
                _logger.LogError(ex, "File dialog thread failed");
                tcs.SetResult((null, true));
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    private string? TryReadImagePreviewBase64(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            // Keep an upper bound to avoid huge payload over WebView message channel.
            const long maxPreviewBytes = 64 * 1024 * 1024;
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length <= 0 || fileInfo.Length > maxPreviewBytes)
            {
                return null;
            }

            var bytes = File.ReadAllBytes(filePath);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WebMessageHandler] 璇诲彇棰勮鍥惧け璐? {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// 澶勭悊 AI 鐢熸垚宸ヤ綔娴佽姹?
    /// </summary>
    private async Task HandleGenerateFlowCommand(string messageJson, int? requestId)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            string description = string.Empty;

            if (TryReadString(doc.RootElement, "prompt", out var prompt) &&
                !string.IsNullOrWhiteSpace(prompt))
            {
                description = prompt.Trim();
            }
            else if (TryReadString(doc.RootElement, "description", out var legacyDescription) &&
                     !string.IsNullOrWhiteSpace(legacyDescription))
            {
                description = legacyDescription.Trim();
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new InvalidOperationException("Missing prompt/description");
            }

            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<Acme.Product.Infrastructure.AI.GenerateFlowMessageHandler>();

            var resultJson = await handler.HandleAsync(description,
                onProgress: (type, payload) =>
                {
                    // 鍦?UI 绾跨▼鎺ㄩ€佽繘搴︽秷鎭?
                    var progressJson = JsonSerializer.Serialize(new
                    {
                        messageType = type,
                        payload = JsonSerializer.Deserialize<object>(payload)
                    }, _jsonOptions);

                    if (_webViewControl?.InvokeRequired == true)
                    {
                        _webViewControl.Invoke(() => _webView?.PostWebMessageAsJson(progressJson));
                    }
                    else
                    {
                        _webView?.PostWebMessageAsJson(progressJson);
                    }
                });

            if (requestId.HasValue)
            {
                var data = JsonSerializer.Deserialize<object>(resultJson, _jsonOptions) ?? new { };
                SendRequestResponse(requestId.Value, data);
            }
            else
            {
                // 鍙戝洖鍓嶇锛堝師濮?JSON锛?
                if (_webViewControl?.InvokeRequired == true)
                {
                    _webViewControl.Invoke(() => _webView?.PostWebMessageAsJson(resultJson));
                }
                else
                {
                    _webView?.PostWebMessageAsJson(resultJson);
                }
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogWarning(ex, "AI service connection blocked");
            if (requestId.HasValue)
            {
                SendRequestResponse(
                    requestId.Value,
                    new { success = false },
                    "网络连接被阻止（可能是防火墙），请检查网络与代理设置");
                return;
            }

            SendProgressMessage("AiFirewallBlocked", new
            {
                message = "Network connection appears blocked (possibly by firewall).",
                detail = ex.Message,
                timestamp = DateTime.Now
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "AI 鏈嶅姟璇锋眰瓒呮椂");
            if (requestId.HasValue)
            {
                SendRequestResponse(
                    requestId.Value,
                    new { success = false },
                    "AI 请求超时，请稍后重试");
                return;
            }

            SendProgressMessage("AiFirewallBlocked", new
            {
                message = "AI 鏈嶅姟杩炴帴瓒呮椂锛堝彲鑳借闃茬伀澧欐嫤鎴級",
                detail = "璇锋鏌ョ綉缁滅幆澧冩垨浠ｇ悊璁剧疆",
                timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "澶勭悊 AI 鐢熸垚璇锋眰澶辫触");

            if (requestId.HasValue)
            {
                SendRequestResponse(
                    requestId.Value,
                    new { success = false },
                    $"System error: {ex.Message}");
                return;
            }

            var errorResponse = new GenerateFlowResponse
            {
                Success = false,
                ErrorMessage = $"System error: {ex.Message}"
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (_webViewControl?.InvokeRequired == true)
            {
                _webViewControl.Invoke(() => _webView?.PostWebMessageAsJson(json));
            }
            else
            {
                _webView?.PostWebMessageAsJson(json);
            }
        }
    }

    private async Task HandleSettingsGetQuery(int? requestId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = await configurationService.LoadAsync();

            var payload = new
            {
                settings = MapConfigToBridgeSettings(config)
            };

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("settings.get.result", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Load settings failed");

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new
                {
                    settings = MapConfigToBridgeSettings(new AppConfig())
                }, ex.Message);
                return;
            }

            SendProgressMessage("settings.get.result", new { error = ex.Message });
        }
    }

    private async Task HandleSettingsSaveCommand(string messageJson, int? requestId)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            if (!TryReadProperty(doc.RootElement, "settings", out var settingsElement) ||
                settingsElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Missing settings payload");
            }

            using var scope = _scopeFactory.CreateScope();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = await configurationService.LoadAsync();
            var incoming = JsonSerializer.Deserialize<BridgeSettingsModel>(
                settingsElement.GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new BridgeSettingsModel();

            ApplyBridgeSettingsToConfig(incoming, config);
            await configurationService.SaveAsync(config);

            var payload = new
            {
                success = true,
                settings = MapConfigToBridgeSettings(config)
            };

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("settings.save.result", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Save settings failed");

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { success = false }, ex.Message);
                return;
            }

            SendProgressMessage("settings.save.result", new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 澶勭悊鎵嬬溂鏍囧畾瑙ｇ畻璇锋眰
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

            // 鍙戦€佺粨鏋滃洖鍓嶇
            SendProgressMessage("handeye:solve:result", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "澶勭悊鎵嬬溂鏍囧畾瑙ｇ畻澶辫触");
            SendProgressMessage("handeye:solve:result", new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 澶勭悊鎵嬬溂鏍囧畾淇濆瓨璇锋眰
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

            // 鍙戦€佺粨鏋滃洖鍓嶇
            SendProgressMessage("handeye:save:result", new { success = isSaved });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "淇濆瓨鎵嬬溂鏍囧畾鏂囦欢澶辫触");
            SendProgressMessage("handeye:save:result", new { success = false, message = ex.Message });
        }
    }

    private async Task HandleSystemStatsQuery(int? requestId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var statsService = scope.ServiceProvider.GetRequiredService<ISystemStatsService>();
            var stats = await statsService.GetDashboardStatsAsync();

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, stats);
                return;
            }

            SendProgressMessage("SystemStatsResult", stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Query system stats failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { }, ex.Message);
                return;
            }

            SendProgressMessage("SystemStatsResult", new { error = ex.Message });
        }
    }

    private async Task HandleHardwareStatusQuery(int? requestId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var statsService = scope.ServiceProvider.GetRequiredService<ISystemStatsService>();
            var status = await statsService.GetHardwareStatusAsync();

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, status);
                return;
            }

            SendProgressMessage("HardwareStatusResult", status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Query hardware status failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { }, ex.Message);
                return;
            }

            SendProgressMessage("HardwareStatusResult", new { error = ex.Message });
        }
    }

    private async Task HandleActivityLogQuery(string messageJson, int? requestId)
    {
        try
        {
            var count = 10;
            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (TryReadInt(doc.RootElement, "count", out var parsedCount))
                {
                    count = Math.Max(1, parsedCount);
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var statsService = scope.ServiceProvider.GetRequiredService<ISystemStatsService>();
            var activities = await statsService.GetRecentActivitiesAsync(count);

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { activities });
                return;
            }

            SendProgressMessage("ActivityLogResult", new { activities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Query activity logs failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { }, ex.Message);
                return;
            }

            SendProgressMessage("ActivityLogResult", new { error = ex.Message });
        }
    }

    private async Task HandleProjectListQuery(int? requestId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
            var projects = await projectService.GetAllAsync();

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { projects });
                return;
            }

            SendProgressMessage("ProjectListResult", new { projects });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Query projects failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { }, ex.Message);
                return;
            }

            SendProgressMessage("ProjectListResult", new { error = ex.Message });
        }
    }

    private async Task HandleProjectCreateCommand(string messageJson, int? requestId)
    {
        try
        {
            string name = $"Project_{DateTime.UtcNow:yyyyMMddHHmmss}";
            string? description = null;

            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (TryReadString(doc.RootElement, "name", out var parsedName) && !string.IsNullOrWhiteSpace(parsedName))
                {
                    name = parsedName;
                }

                if (TryReadString(doc.RootElement, "description", out var parsedDescription))
                {
                    description = parsedDescription;
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
            var project = await projectService.CreateAsync(new CreateProjectRequest
            {
                Name = name,
                Description = description
            });

            var payload = new
            {
                success = true,
                projectId = project.Id,
                project
            };

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("ProjectCreateResult", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Create project failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { success = false }, ex.Message);
                return;
            }

            SendProgressMessage("ProjectCreateResult", new { success = false, error = ex.Message });
        }
    }

    private async Task HandleProjectDeleteCommand(string messageJson, int? requestId)
    {
        try
        {
            Guid projectId;
            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (!TryReadGuid(doc.RootElement, "projectId", out projectId))
                {
                    throw new InvalidOperationException("Missing projectId");
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
            await projectService.DeleteAsync(projectId);

            var payload = new { success = true, projectId };
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("ProjectDeleteResult", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Delete project failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { success = false }, ex.Message);
                return;
            }

            SendProgressMessage("ProjectDeleteResult", new { success = false, error = ex.Message });
        }
    }

    private async Task HandleProjectOpenCommand(string messageJson, int? requestId)
    {
        try
        {
            Guid projectId;
            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (!TryReadGuid(doc.RootElement, "projectId", out projectId))
                {
                    throw new InvalidOperationException("Missing projectId");
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
            var project = await projectService.GetByIdAsync(projectId);

            if (project == null)
            {
                throw new InvalidOperationException("Project not found");
            }

            var payload = new { success = true, project };
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("ProjectOpenResult", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Open project failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { success = false }, ex.Message);
                return;
            }

            SendProgressMessage("ProjectOpenResult", new { success = false, error = ex.Message });
        }
    }

    private async Task HandleResultsQuery(string messageJson, int? requestId)
    {
        try
        {
            Guid? projectId = null;
            string status = "all";
            string search = string.Empty;
            DateTime? startDate = null;
            DateTime? endDate = null;

            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (TryReadGuid(doc.RootElement, "projectId", out var parsedProjectId))
                {
                    projectId = parsedProjectId;
                }

                if (TryReadString(doc.RootElement, "status", out var parsedStatus) && !string.IsNullOrWhiteSpace(parsedStatus))
                {
                    status = parsedStatus.Trim().ToLowerInvariant();
                }

                if (TryReadString(doc.RootElement, "search", out var parsedSearch) && !string.IsNullOrWhiteSpace(parsedSearch))
                {
                    search = parsedSearch.Trim().ToLowerInvariant();
                }

                if (TryReadDateTime(doc.RootElement, "startDate", out var parsedStart))
                {
                    startDate = parsedStart;
                }

                if (TryReadDateTime(doc.RootElement, "endDate", out var parsedEnd))
                {
                    endDate = parsedEnd;
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var resultRepository = scope.ServiceProvider.GetRequiredService<IInspectionResultRepository>();

            IEnumerable<InspectionResult> results;
            if (projectId.HasValue && startDate.HasValue && endDate.HasValue)
            {
                results = await resultRepository.GetByTimeRangeAsync(projectId.Value, startDate.Value, endDate.Value);
            }
            else if (projectId.HasValue)
            {
                results = await resultRepository.GetByProjectIdAsync(projectId.Value, 0, 500);
            }
            else
            {
                results = await resultRepository.FindAsync(result => !result.IsDeleted);
            }

            var mapped = results
                .Where(result => FilterByStatus(result.Status, status))
                .Where(result => FilterBySearch(result, search))
                .OrderByDescending(result => result.InspectionTime)
                .Take(500)
                .Select(MapInspectionRecord)
                .ToList();

            var payload = new { records = mapped };
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("ResultsQueryResult", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Query results failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { records = Array.Empty<object>() }, ex.Message);
                return;
            }

            SendProgressMessage("ResultsQueryResult", new { error = ex.Message });
        }
    }

    private async Task HandleResultsExportCommand(string messageJson, int? requestId)
    {
        try
        {
            Guid recordId;
            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (!TryReadGuid(doc.RootElement, "recordId", out recordId))
                {
                    throw new InvalidOperationException("Missing recordId");
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var resultRepository = scope.ServiceProvider.GetRequiredService<IInspectionResultRepository>();
            var record = await resultRepository.GetByIdAsync(recordId);

            if (record == null)
            {
                throw new InvalidOperationException("Record not found");
            }

            var exportPayload = new
            {
                record.Id,
                record.ProjectId,
                Status = record.Status.ToString(),
                record.ProcessingTimeMs,
                record.InspectionTime,
                Defects = record.Defects.Select(defect => new
                {
                    defect.Id,
                    Type = defect.Type.ToString(),
                    defect.X,
                    defect.Y,
                    defect.Width,
                    defect.Height,
                    defect.ConfidenceScore,
                    defect.Description
                })
            };

            var content = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var payload = new
            {
                success = true,
                fileName = $"inspection-report-{recordId:N}.json",
                content
            };

            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("ResultsExportResult", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Export result failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { success = false }, ex.Message);
                return;
            }

            SendProgressMessage("ResultsExportResult", new { success = false, error = ex.Message });
        }
    }

    private async Task HandleFalsePositiveCommand(string messageJson, int? requestId)
    {
        try
        {
            Guid recordId;
            using (var doc = JsonDocument.Parse(messageJson))
            {
                if (!TryReadGuid(doc.RootElement, "recordId", out recordId))
                {
                    throw new InvalidOperationException("Missing recordId");
                }
            }

            using var scope = _scopeFactory.CreateScope();
            var resultRepository = scope.ServiceProvider.GetRequiredService<IInspectionResultRepository>();
            var record = await resultRepository.GetByIdAsync(recordId);
            if (record == null)
            {
                throw new InvalidOperationException("Record not found");
            }

            var outputData = ParseOutputData(record.OutputDataJson);
            outputData["falsePositive"] = true;
            outputData["falsePositiveAt"] = DateTime.UtcNow;
            record.SetOutputDataJson(JsonSerializer.Serialize(outputData));
            await resultRepository.UpdateAsync(record);

            var payload = new { success = true, recordId };
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, payload);
                return;
            }

            SendProgressMessage("FalsePositiveResult", payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Mark false positive failed");
            if (requestId.HasValue)
            {
                SendRequestResponse(requestId.Value, new { success = false }, ex.Message);
                return;
            }

            SendProgressMessage("FalsePositiveResult", new { success = false, error = ex.Message });
        }
    }

    private static Dictionary<string, object?> ParseOutputData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            return parsed ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool FilterByStatus(InspectionStatus status, string queryStatus)
    {
        return queryStatus switch
        {
            "ok" => status == InspectionStatus.OK,
            "ng" => status == InspectionStatus.NG,
            "error" => status == InspectionStatus.Error,
            _ => true
        };
    }

    private static bool FilterBySearch(InspectionResult result, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        if (result.Id.ToString("N").Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(result.OutputDataJson))
        {
            return false;
        }

        return result.OutputDataJson.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static object MapInspectionRecord(InspectionResult result)
    {
        object? outputData = null;
        if (!string.IsNullOrWhiteSpace(result.OutputDataJson))
        {
            try
            {
                outputData = JsonSerializer.Deserialize<object>(result.OutputDataJson);
            }
            catch
            {
                outputData = null;
            }
        }

        return new
        {
            id = result.Id,
            projectId = result.ProjectId,
            status = result.Status.ToString(),
            processingTimeMs = result.ProcessingTimeMs,
            inspectionTime = result.InspectionTime,
            outputImage = result.OutputImage != null ? Convert.ToBase64String(result.OutputImage) : null,
            defects = result.Defects.Select(defect => new
            {
                id = defect.Id,
                type = defect.Type.ToString(),
                x = defect.X,
                y = defect.Y,
                width = defect.Width,
                height = defect.Height,
                confidenceScore = defect.ConfidenceScore,
                description = defect.Description
            }),
            outputData
        };
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string value)
    {
        if (TryReadProperty(root, propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadInt(JsonElement root, string propertyName, out int value)
    {
        if (TryReadProperty(root, propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadGuid(JsonElement root, string propertyName, out Guid value)
    {
        if (TryReadProperty(root, propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String && Guid.TryParse(property.GetString(), out value))
            {
                return true;
            }
        }

        value = Guid.Empty;
        return false;
    }

    private static bool TryReadDateTime(JsonElement root, string propertyName, out DateTime value)
    {
        if (TryReadProperty(root, propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(property.GetString(), out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryReadProperty(JsonElement root, string propertyName, out JsonElement property)
    {
        if (root.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        if (root.TryGetProperty("payload", out var payload) &&
            payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static BridgeSettingsModel MapConfigToBridgeSettings(AppConfig config)
    {
        var theme = config.General.Theme?.Trim().ToLowerInvariant();
        if (theme is not ("light" or "dark" or "system"))
        {
            theme = "system";
        }

        return new BridgeSettingsModel
        {
            General = new BridgeGeneralSettings
            {
                Theme = theme,
                Language = string.IsNullOrWhiteSpace(config.General.Language) ? "zh-CN" : config.General.Language,
                AutoSaveInterval = Math.Clamp(config.General.AutoSaveIntervalMinutes, 0, 60)
            },
            Camera = new BridgeCameraSettings
            {
                DefaultResolution = string.IsNullOrWhiteSpace(config.CameraDefaults.DefaultResolution)
                    ? "1920x1080"
                    : config.CameraDefaults.DefaultResolution,
                ExposureTarget = Math.Clamp(config.CameraDefaults.ExposureTarget, 0, 255)
            },
            Communication = new BridgeCommunicationSettings
            {
                Protocol = NormalizeProtocol(config.Communication.Protocol),
                Host = string.IsNullOrWhiteSpace(config.Communication.PlcIpAddress)
                    ? "127.0.0.1"
                    : config.Communication.PlcIpAddress,
                Port = config.Communication.PlcPort > 0 ? config.Communication.PlcPort : 8080
            },
            Ai = new BridgeAiSettings
            {
                ApiKey = config.Ai.ApiKey ?? string.Empty,
                Model = string.IsNullOrWhiteSpace(config.Ai.Model) ? "DeepSeek-V3" : config.Ai.Model,
                TimeoutMs = config.Ai.TimeoutMs > 0 ? config.Ai.TimeoutMs : 30000
            }
        };
    }

    private static void ApplyBridgeSettingsToConfig(BridgeSettingsModel incoming, AppConfig config)
    {
        config.General.Theme = incoming.General.Theme is "light" or "dark" or "system"
            ? incoming.General.Theme
            : "system";
        config.General.Language = string.IsNullOrWhiteSpace(incoming.General.Language)
            ? "zh-CN"
            : incoming.General.Language.Trim();
        config.General.AutoSaveIntervalMinutes = Math.Clamp(incoming.General.AutoSaveInterval, 0, 60);

        config.CameraDefaults.DefaultResolution = string.IsNullOrWhiteSpace(incoming.Camera.DefaultResolution)
            ? "1920x1080"
            : incoming.Camera.DefaultResolution.Trim();
        config.CameraDefaults.ExposureTarget = Math.Clamp(incoming.Camera.ExposureTarget, 0, 255);

        config.Communication.Protocol = NormalizeProtocol(incoming.Communication.Protocol);
        config.Communication.PlcIpAddress = string.IsNullOrWhiteSpace(incoming.Communication.Host)
            ? "127.0.0.1"
            : incoming.Communication.Host.Trim();
        config.Communication.PlcPort = Math.Clamp(incoming.Communication.Port, 1, 65535);

        config.Ai.ApiKey = incoming.Ai.ApiKey ?? string.Empty;
        config.Ai.Model = string.IsNullOrWhiteSpace(incoming.Ai.Model)
            ? "DeepSeek-V3"
            : incoming.Ai.Model.Trim();
        config.Ai.TimeoutMs = Math.Clamp(incoming.Ai.TimeoutMs, 5000, 600000);
    }

    private static string NormalizeProtocol(string? rawProtocol)
    {
        if (string.IsNullOrWhiteSpace(rawProtocol))
        {
            return "TCP";
        }

        var protocol = rawProtocol.Trim().ToUpperInvariant();
        if (protocol.Contains("SERIAL"))
        {
            return "Serial";
        }

        if (protocol.Contains("HTTP"))
        {
            return "HTTP";
        }

        if (protocol.Contains("MQTT"))
        {
            return "MQTT";
        }

        if (protocol.Contains("PLC") ||
            protocol.Contains("MODBUS") ||
            protocol.Contains("FINS") ||
            protocol.Contains("MC") ||
            protocol.Contains("S7"))
        {
            return "PLC";
        }

        return "TCP";
    }

    private sealed class BridgeSettingsModel
    {
        public BridgeGeneralSettings General { get; set; } = new();
        public BridgeCameraSettings Camera { get; set; } = new();
        public BridgeCommunicationSettings Communication { get; set; } = new();
        public BridgeAiSettings Ai { get; set; } = new();
    }

    private sealed class BridgeGeneralSettings
    {
        public string Theme { get; set; } = "system";
        public string Language { get; set; } = "zh-CN";
        public int AutoSaveInterval { get; set; } = 5;
    }

    private sealed class BridgeCameraSettings
    {
        public string DefaultResolution { get; set; } = "1920x1080";
        public int ExposureTarget { get; set; } = 120;
    }

    private sealed class BridgeCommunicationSettings
    {
        public string Protocol { get; set; } = "TCP";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8080;
    }

    private sealed class BridgeAiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "DeepSeek-V3";
        public int TimeoutMs { get; set; } = 30000;
    }

    /// <summary>
    /// 鍙戦€佷簨浠跺埌鍓嶇
    /// </summary>
    private void SendEvent<T>(T eventData)
    {
        try
        {
            var json = JsonSerializer.Serialize(eventData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // 纭繚鍦?UI 绾跨▼鎵ц
            if (_webViewControl?.InvokeRequired == true)
            {
                _webViewControl.Invoke(() => _webView?.PostWebMessageAsJson(json));
            }
            else
            {
                _webView?.PostWebMessageAsJson(json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WebMessageHandler] Failed to send event");
        }
    }

    private void SendRequestResponse(int requestId, object data, string? error = null)
    {
        var responsePayload = JsonSerializer.Serialize(new
        {
            requestId,
            data,
            error
        }, _jsonOptions);

        if (_webViewControl?.InvokeRequired == true)
        {
            _webViewControl.Invoke(() => _webView?.PostWebMessageAsJson(responsePayload));
        }
        else
        {
            _webView?.PostWebMessageAsJson(responsePayload);
        }
    }

    private void SendProgressMessage(string type, object payload)
    {
        var json = JsonSerializer.Serialize(new
        {
            messageType = type,
            payload
        }, _jsonOptions);

        if (_webViewControl?.InvokeRequired == true)
        {
            _webViewControl.Invoke(() => _webView?.PostWebMessageAsJson(json));
        }
        else
        {
            _webView?.PostWebMessageAsJson(json);
        }
    }
}

