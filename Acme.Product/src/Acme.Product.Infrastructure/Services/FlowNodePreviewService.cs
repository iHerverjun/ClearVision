using System.Text.Json;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Services;

public sealed class FlowNodePreviewService : IFlowNodePreviewService
{
    private readonly ILogger<FlowNodePreviewService> _logger;
    private readonly IFlowExecutionService _flowExecution;
    private readonly IPreviewMetricsAnalyzer _metricsAnalyzer;

    public FlowNodePreviewService(
        ILogger<FlowNodePreviewService> logger,
        IFlowExecutionService flowExecution,
        IPreviewMetricsAnalyzer metricsAnalyzer)
    {
        _logger = logger;
        _flowExecution = flowExecution;
        _metricsAnalyzer = metricsAnalyzer;
    }

    public async Task<FlowNodePreviewWithMetricsResult> PreviewWithMetricsAsync(
        OperatorFlow flow,
        Guid targetNodeId,
        byte[]? inputImage,
        CancellationToken ct = default)
    {
        var targetOperator = flow.Operators.FirstOrDefault(item => item.Id == targetNodeId);
        if (targetOperator == null)
        {
            return new FlowNodePreviewWithMetricsResult
            {
                Success = false,
                TargetNodeId = targetNodeId,
                ErrorMessage = $"未找到目标节点: {targetNodeId}"
            };
        }

        var missingResources = CollectMissingResources(flow, targetNodeId);
        if (missingResources.Count > 0)
        {
            return new FlowNodePreviewWithMetricsResult
            {
                Success = false,
                TargetNodeId = targetNodeId,
                ErrorMessage = "线序预览缺少必要资源，无法继续执行。",
                MissingResources = missingResources,
                DiagnosticCodes = missingResources
                    .Select(item => item.DiagnosticCode)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        var debugSessionId = Guid.NewGuid();
        var debugOptions = new DebugOptions
        {
            DebugSessionId = debugSessionId,
            EnableIntermediateCache = true,
            BreakAtOperatorId = targetNodeId,
            ImageFormat = ".png"
        };

        var inputData = BuildInputData(flow, targetNodeId, inputImage);
        var result = await _flowExecution.ExecuteFlowDebugAsync(flow, debugOptions, inputData, ct);

        if (!result.IntermediateResults.TryGetValue(targetNodeId, out var nodeOutput))
        {
            nodeOutput = _flowExecution.GetDebugIntermediateResult(debugSessionId, targetNodeId);
        }

        if (nodeOutput == null)
        {
            return BuildFailureResult(flow, targetNodeId, result, missingResources);
        }

        var targetDebugResult = result.DebugOperatorResults.FirstOrDefault(item => item.OperatorId == targetNodeId);
        var previewImageBytes = TryGetOutputImageBytes(nodeOutput)
            ?? TryGetImageBytesFromSnapshot(targetDebugResult?.InputSnapshot);
        var inputSnapshotImage = ResolveInputImageBytes(flow, targetNodeId, result, targetDebugResult, inputImage);
        var sanitizedOutputs = BuildResponseOutputData(nodeOutput);
        var metrics = AnalyzePreviewMetrics(previewImageBytes, sanitizedOutputs);
        var diagnosticCodes = BuildDiagnosticCodes(metrics, missingResources);

        return new FlowNodePreviewWithMetricsResult
        {
            Success = result.IsSuccess,
            TargetNodeId = targetNodeId,
            InputImage = inputSnapshotImage,
            PreviewImage = previewImageBytes,
            Outputs = sanitizedOutputs,
            Metrics = metrics,
            Suggestions = metrics?.Suggestions?.ToList() ?? new List<ParameterSuggestion>(),
            MissingResources = missingResources,
            DiagnosticCodes = diagnosticCodes,
            ErrorMessage = result.ErrorMessage,
            ExecutedOperators = result.DebugOperatorResults
                .Select(item => new ExecutedOperatorTrace
                {
                    OperatorId = item.OperatorId,
                    OperatorName = item.OperatorName,
                    ExecutionOrder = item.ExecutionOrder,
                    ExecutionTimeMs = item.ExecutionTimeMs,
                    IsSuccess = item.IsSuccess
                })
                .ToList()
        };
    }

    private PreviewMetrics? AnalyzePreviewMetrics(byte[]? previewImageBytes, Dictionary<string, object> outputData)
    {
        if (previewImageBytes == null || previewImageBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var image = Cv2.ImDecode(previewImageBytes, ImreadModes.Unchanged);
            if (image.Empty())
            {
                return null;
            }

            return _metricsAnalyzer.Analyze(image, outputData);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FlowNodePreview] 无法分析预览指标");
            return null;
        }
    }

    private static Dictionary<string, object>? BuildInputData(OperatorFlow flow, Guid targetNodeId, byte[]? inputImage)
    {
        if (inputImage == null || inputImage.Length == 0)
        {
            return null;
        }

        if (HasUpstreamOperatorType(flow, targetNodeId, OperatorType.ImageAcquisition))
        {
            return null;
        }

        return new Dictionary<string, object>
        {
            ["Image"] = inputImage
        };
    }

    private static List<PreviewMissingResource> CollectMissingResources(OperatorFlow flow, Guid targetNodeId)
    {
        var relevantOperators = CollectRelevantOperators(flow, targetNodeId);
        var missing = new List<PreviewMissingResource>();

        foreach (var op in relevantOperators.Where(item => item.Type == OperatorType.DeepLearning))
        {
            var modelPath = GetStringParam(op, "ModelPath");
            if (string.IsNullOrWhiteSpace(modelPath) || !PathExists(modelPath))
            {
                missing.Add(new PreviewMissingResource
                {
                    ResourceType = "Model",
                    ResourceKey = "DeepLearning.ModelPath",
                    Description = string.IsNullOrWhiteSpace(modelPath)
                        ? "缺少模型文件路径"
                        : $"模型文件不存在：{modelPath}",
                    DiagnosticCode = "missing_model"
                });
            }

            var labelsPath = GetStringParam(op, "LabelsPath", "LabelFile");
            var targetClasses = GetStringParam(op, "TargetClasses");
            if (!DeepLearningLabelResolver.AreLabelsResolvable(labelsPath, modelPath, targetClasses, out _))
            {
                missing.Add(new PreviewMissingResource
                {
                    ResourceType = "Label",
                    ResourceKey = "DeepLearning.LabelsPath",
                    Description = string.IsNullOrWhiteSpace(labelsPath)
                        ? "缺少可用的标签文件，且未找到可匹配目标类别的内置标签"
                        : $"标签文件不存在，且未找到可匹配目标类别的内置标签：{labelsPath}",
                    DiagnosticCode = "missing_labels"
                });
            }
        }

        return missing
            .GroupBy(item => item.ResourceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static FlowNodePreviewWithMetricsResult BuildFailureResult(
        OperatorFlow flow,
        Guid targetNodeId,
        FlowDebugExecutionResult result,
        List<PreviewMissingResource> missingResources)
    {
        var targetResult = result.DebugOperatorResults.FirstOrDefault(item => item.OperatorId == targetNodeId);
        var failedOperator = targetResult?.IsSuccess == false
            ? targetResult
            : result.DebugOperatorResults.FirstOrDefault(item => !item.IsSuccess);
        var inputImage = TryGetImageBytesFromSnapshot(targetResult?.InputSnapshot)
            ?? TryGetImageBytesFromSnapshot(failedOperator?.InputSnapshot)
            ?? ResolveInputImageBytes(flow, targetNodeId, result, targetResult, null);
        var outputs = targetResult?.OutputSnapshot ?? failedOperator?.OutputSnapshot ?? new Dictionary<string, object>();

        return new FlowNodePreviewWithMetricsResult
        {
            Success = false,
            TargetNodeId = targetNodeId,
            InputImage = inputImage,
            Outputs = BuildResponseOutputData(outputs),
            MissingResources = missingResources,
            DiagnosticCodes = missingResources
                .Select(item => item.DiagnosticCode)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ErrorMessage = BuildMissingNodeOutputDetail(result, targetNodeId),
            FailedOperatorId = failedOperator?.OperatorId,
            FailedOperatorName = failedOperator?.OperatorName,
            ExecutedOperators = result.DebugOperatorResults
                .Select(item => new ExecutedOperatorTrace
                {
                    OperatorId = item.OperatorId,
                    OperatorName = item.OperatorName,
                    ExecutionOrder = item.ExecutionOrder,
                    ExecutionTimeMs = item.ExecutionTimeMs,
                    IsSuccess = item.IsSuccess
                })
                .ToList()
        };
    }

    private static List<string> BuildDiagnosticCodes(
        PreviewMetrics? metrics,
        IReadOnlyCollection<PreviewMissingResource> missingResources)
    {
        var codes = new List<string>();

        if (metrics != null)
        {
            foreach (var diagnostic in metrics.Diagnostics)
            {
                var mapped = diagnostic switch
                {
                    PreviewDiagnosticTags.MissingExpectedClass => "missing_expected_class",
                    PreviewDiagnosticTags.DuplicateDetectedClass => "duplicate_detected_class",
                    PreviewDiagnosticTags.DetectionCountMismatch => "detection_count_mismatch",
                    PreviewDiagnosticTags.LowDetectionConfidence => "low_detection_confidence",
                    PreviewDiagnosticTags.OrderMismatch => "order_mismatch",
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(mapped))
                {
                    codes.Add(mapped);
                }
            }
        }

        codes.AddRange(missingResources
            .Select(item => item.DiagnosticCode)
            .Where(item => !string.IsNullOrWhiteSpace(item)));

        return codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<Guid> CollectRelevantOperatorIds(OperatorFlow flow, Guid targetNodeId)
    {
        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(targetNodeId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var connection in flow.Connections.Where(item => item.TargetOperatorId == current))
            {
                stack.Push(connection.SourceOperatorId);
            }
        }

        return visited;
    }

    private static List<Operator> CollectRelevantOperators(OperatorFlow flow, Guid targetNodeId)
    {
        var relevantIds = CollectRelevantOperatorIds(flow, targetNodeId);
        return flow.Operators
            .Where(item => relevantIds.Contains(item.Id))
            .ToList();
    }

    private static bool HasUpstreamOperatorType(OperatorFlow flow, Guid targetNodeId, OperatorType type)
    {
        var relevantIds = CollectRelevantOperatorIds(flow, targetNodeId);
        return flow.Operators.Any(item => relevantIds.Contains(item.Id) && item.Type == type);
    }

    private static bool PathExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var normalized = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(path);
            return File.Exists(normalized);
        }
        catch
        {
            return false;
        }
    }

    private static string GetStringParam(Operator op, params string[] names)
    {
        foreach (var name in names)
        {
            var parameter = op.Parameters.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var raw = parameter?.GetValue()?.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
        }

        return string.Empty;
    }

    private static Dictionary<string, object> BuildResponseOutputData(Dictionary<string, object> nodeOutput)
    {
        var response = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in nodeOutput)
        {
            if (string.Equals(key, "Image", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            response[key] = value;
        }

        return response;
    }

    private static byte[]? TryGetOutputImageBytes(Dictionary<string, object> nodeOutput)
    {
        if (!nodeOutput.TryGetValue("Image", out var imageValue) || imageValue == null)
        {
            return null;
        }

        return imageValue switch
        {
            ImageWrapper wrapper => wrapper.GetBytes(),
            Mat mat when !mat.Empty() => mat.ToBytes(".png"),
            byte[] bytes => bytes,
            string base64 when !string.IsNullOrWhiteSpace(base64) => TryDecodeBase64(base64),
            JsonElement element when element.ValueKind == JsonValueKind.String => TryDecodeBase64(element.GetString()),
            _ => null
        };
    }

    private static byte[]? TryGetImageBytesFromSnapshot(Dictionary<string, object>? snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }

        return TryGetOutputImageBytes(snapshot);
    }

    private static byte[]? TryDecodeBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildMissingNodeOutputDetail(FlowDebugExecutionResult result, Guid targetNodeId)
    {
        var targetResult = result.DebugOperatorResults.FirstOrDefault(item => item.OperatorId == targetNodeId);
        if (targetResult != null && !targetResult.IsSuccess)
        {
            return $"目标节点 '{targetResult.OperatorName}' 执行失败: {targetResult.ErrorMessage ?? "未知错误"}";
        }

        var failedOperator = result.DebugOperatorResults.FirstOrDefault(item => !item.IsSuccess);
        if (failedOperator != null)
        {
            return $"上游或目标节点 '{failedOperator.OperatorName}' 执行失败: {failedOperator.ErrorMessage ?? "未知错误"}";
        }

        return !string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result.ErrorMessage!
            : $"无法获取节点 {targetNodeId} 的输出，可能节点执行失败或未被执行";
    }
    private static byte[]? ResolveInputImageBytes(
        OperatorFlow flow,
        Guid targetNodeId,
        FlowDebugExecutionResult result,
        OperatorDebugResult? targetDebugResult,
        byte[]? externalInputImage)
    {
        return TryGetImageBytesFromSnapshot(targetDebugResult?.InputSnapshot)
            ?? TryGetNearestImageAcquisitionOutputBytes(flow, targetNodeId, result)
            ?? externalInputImage;
    }

    private static byte[]? TryGetNearestImageAcquisitionOutputBytes(
        OperatorFlow flow,
        Guid targetNodeId,
        FlowDebugExecutionResult result)
    {
        var relevantIds = CollectRelevantOperatorIds(flow, targetNodeId);
        var imageAcquisitionIds = flow.Operators
            .Where(item => relevantIds.Contains(item.Id) && item.Type == OperatorType.ImageAcquisition)
            .Select(item => item.Id)
            .ToHashSet();

        if (imageAcquisitionIds.Count == 0)
        {
            return null;
        }

        foreach (var debugResult in result.DebugOperatorResults
                     .Where(item => imageAcquisitionIds.Contains(item.OperatorId))
                     .OrderByDescending(item => item.ExecutionOrder))
        {
            var bytes = TryGetImageBytesFromSnapshot(debugResult.OutputSnapshot)
                ?? TryGetImageBytesFromSnapshot(debugResult.InputSnapshot);
            if (bytes != null && bytes.Length > 0)
            {
                return bytes;
            }
        }

        foreach (var operatorId in imageAcquisitionIds)
        {
            if (result.IntermediateResults.TryGetValue(operatorId, out var outputData))
            {
                var bytes = TryGetOutputImageBytes(outputData);
                if (bytes != null && bytes.Length > 0)
                {
                    return bytes;
                }
            }
        }

        return null;
    }
}
