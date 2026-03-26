// PreviewNodeEndpoints.cs
// 预览工作流中指定节点的输出
// 【Phase 3】复用调试缓存机制，执行上游子图到目标节点
// 作者：架构修复方案 v2

using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Infrastructure.Operators;
using Acme.Product.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System.Collections;
using System.Text.Json;

namespace Acme.Product.Desktop.Endpoints;

/// <summary>
/// 节点预览端点
/// </summary>
public static class PreviewNodeEndpoints
{
    /// <summary>
    /// 映射节点预览端点
    /// </summary>
    public static IEndpointRouteBuilder MapPreviewNodeEndpoints(this IEndpointRouteBuilder app)
    {
        // 【Phase 3】预览工作流中指定节点的输出
        app.MapPost("/api/flows/preview-node", async (
            PreviewNodeRequest request,
            IFlowExecutionService flowService,
            IProjectRepository projectRepository,
            ILogger<object> logger) =>
        {
            try
            {
                logger.LogInformation(
                    "[PreviewNode] 请求预览节点: Project={ProjectId}, Node={NodeId}, Session={DebugSessionId}",
                    request.ProjectId, request.TargetNodeId, request.DebugSessionId);

                // 从数据库加载流程，或直接使用前端传来的流程数据
                Acme.Product.Core.Entities.OperatorFlow? flow;
                
                if (request.FlowData?.Operators?.Count > 0)
                {
                    // 使用前端传来的流程数据
                    flow = FlowEntityMapper.ToEntity(request.FlowData, "PreviewFlow");
                }
                else
                {
                    // 从数据库加载
                    var project = await projectRepository.GetWithFlowAsync(request.ProjectId);
                    flow = project?.Flow;
                }

                if (flow == null)
                {
                    return Results.Problem(
                        detail: "无法获取流程数据",
                        statusCode: 400);
                }

                // 应用参数覆盖（如果有）
                if (request.Parameters != null && request.Parameters.Count > 0)
                {
                    var targetOp = flow.Operators.FirstOrDefault(o => o.Id == request.TargetNodeId);
                    if (targetOp != null)
                    {
                        foreach (var param in request.Parameters)
                        {
                            var existingParam = targetOp.Parameters.FirstOrDefault(p => p.Name == param.Key);
                            if (existingParam != null)
                            {
                                existingParam.SetValue(param.Value);
                            }
                        }
                    }
                }

                // 构建调试选项
                var debugOptions = new DebugOptions
                {
                    DebugSessionId = request.DebugSessionId,
                    EnableIntermediateCache = true,
                    BreakAtOperatorId = request.TargetNodeId,  // 【Phase 3】执行到目标节点后停止
                    ImageFormat = request.ImageFormat ?? ".png"
                };

                // 准备输入数据
                Dictionary<string, object>? inputData = null;
                if (!string.IsNullOrEmpty(request.InputImageBase64)
                    && !HasUpstreamOperatorType(flow, request.TargetNodeId, OperatorType.ImageAcquisition))
                {
                    var imageData = Convert.FromBase64String(request.InputImageBase64);
                    inputData = new Dictionary<string, object>
                    {
                        ["Image"] = imageData
                    };
                }

                // 执行调试流程（自动执行上游子图到目标节点）
                var result = await flowService.ExecuteFlowDebugAsync(
                    flow,
                    debugOptions,
                    inputData,
                    CancellationToken.None);

                // 获取目标节点的输出
                if (!result.IntermediateResults.TryGetValue(request.TargetNodeId, out var nodeOutput))
                {
                    // 如果中间结果中没有，尝试从缓存获取
                    nodeOutput = flowService.GetDebugIntermediateResult(request.DebugSessionId, request.TargetNodeId);
                }

                if (nodeOutput == null)
                {
                    return Results.Ok(BuildFailureResponse(request, flow, result, request.TargetNodeId));
                }

                // 提取输出图像
                var outputImageBytes = TryGetOutputImageBytes(nodeOutput);
                var outputImageBase64 = outputImageBytes != null
                    ? Convert.ToBase64String(outputImageBytes)
                    : null;
                var sanitizedOutputData = BuildResponseOutputData(nodeOutput);
                var metrics = BuildPreviewMetrics(sanitizedOutputData, outputImageBytes, result.ErrorMessage);

                logger.LogInformation(
                    "[PreviewNode] 预览完成: Project={ProjectId}, Node={NodeId}, Success={Success}",
                    request.ProjectId, request.TargetNodeId, result.IsSuccess);

                var targetDebugResult = result.DebugOperatorResults.FirstOrDefault(r => r.OperatorId == request.TargetNodeId);
                var inputImageBytes = ResolveInputImageBytes(flow, request.TargetNodeId, result, targetDebugResult, request.InputImageBase64);
                var inputImageBase64 = inputImageBytes != null ? Convert.ToBase64String(inputImageBytes) : null;

                return Results.Ok(new PreviewNodeResponse
                {
                    Success = result.IsSuccess,
                    ProjectId = request.ProjectId,
                    TargetNodeId = request.TargetNodeId,
                    DebugSessionId = request.DebugSessionId,
                    InputImageBase64 = inputImageBase64,
                    OutputData = sanitizedOutputData,
                    OutputImageBase64 = outputImageBase64,
                    ExecutionTimeMs = result.ExecutionTimeMs,
                    ErrorMessage = result.ErrorMessage,
                    Metrics = metrics,
                    ExecutedOperators = result.DebugOperatorResults.Select(r => new ExecutedOperatorInfo
                    {
                        OperatorId = r.OperatorId,
                        OperatorName = r.OperatorName,
                        ExecutionOrder = r.ExecutionOrder,
                        ExecutionTimeMs = r.ExecutionTimeMs,
                        IsSuccess = r.IsSuccess
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[PreviewNode] 预览节点失败: {ProjectId}, {NodeId}",
                    request.ProjectId, request.TargetNodeId);
                return Results.Problem(
                    detail: $"预览节点失败: {ex.Message}",
                    statusCode: 500);
            }
        });

        return app;
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

    private static bool HasUpstreamOperatorType(
        Acme.Product.Core.Entities.OperatorFlow flow,
        Guid targetNodeId,
        OperatorType type)
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
                var sourceOperator = flow.Operators.FirstOrDefault(item => item.Id == connection.SourceOperatorId);
                if (sourceOperator == null)
                {
                    continue;
                }

                if (sourceOperator.Type == type)
                {
                    return true;
                }

                stack.Push(sourceOperator.Id);
            }
        }

        return false;
    }

    private static string BuildMissingNodeOutputDetail(
        FlowDebugExecutionResult result,
        Guid targetNodeId)
    {
        var targetResult = result.DebugOperatorResults
            .FirstOrDefault(item => item.OperatorId == targetNodeId);

        if (targetResult != null && !targetResult.IsSuccess)
        {
            return $"目标节点 '{targetResult.OperatorName}' 执行失败: {targetResult.ErrorMessage ?? "未知错误"}";
        }

        var failedOperator = result.DebugOperatorResults
            .FirstOrDefault(item => !item.IsSuccess);

        if (failedOperator != null)
        {
            return $"上游或目标节点 '{failedOperator.OperatorName}' 执行失败: {failedOperator.ErrorMessage ?? "未知错误"}";
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            return result.ErrorMessage!;
        }

        return $"无法获取节点 {targetNodeId} 的输出，可能节点执行失败或未被执行";
    }

    private static PreviewNodeResponse BuildFailureResponse(
        PreviewNodeRequest request,
        Acme.Product.Core.Entities.OperatorFlow flow,
        FlowDebugExecutionResult result,
        Guid targetNodeId)
    {
        var targetResult = result.DebugOperatorResults
            .FirstOrDefault(item => item.OperatorId == targetNodeId);
        var failedOperator = targetResult?.IsSuccess == false
            ? targetResult
            : result.DebugOperatorResults.FirstOrDefault(item => !item.IsSuccess);

        var failureMessage = BuildMissingNodeOutputDetail(result, targetNodeId);
        var inputImageBytes = TryGetImageBytesFromSnapshot(targetResult?.InputSnapshot)
            ?? TryGetImageBytesFromSnapshot(failedOperator?.InputSnapshot)
            ?? ResolveInputImageBytes(flow, targetNodeId, result, targetResult, request.InputImageBase64);
        var inputImageBase64 = inputImageBytes != null ? Convert.ToBase64String(inputImageBytes) : null;

        return new PreviewNodeResponse
        {
            Success = false,
            ProjectId = request.ProjectId,
            TargetNodeId = request.TargetNodeId,
            DebugSessionId = request.DebugSessionId,
            InputImageBase64 = inputImageBase64,
            OutputData = targetResult?.OutputSnapshot ?? failedOperator?.OutputSnapshot,
            OutputImageBase64 = null,
            ExecutionTimeMs = result.ExecutionTimeMs,
            ErrorMessage = failureMessage,
            FailedOperatorId = failedOperator?.OperatorId,
            FailedOperatorName = failedOperator?.OperatorName,
            FailedOperatorType = null,
            Metrics = null,
            ExecutedOperators = result.DebugOperatorResults.Select(r => new ExecutedOperatorInfo
            {
                OperatorId = r.OperatorId,
                OperatorName = r.OperatorName,
                ExecutionOrder = r.ExecutionOrder,
                ExecutionTimeMs = r.ExecutionTimeMs,
                IsSuccess = r.IsSuccess
            }).ToList()
        };
    }

    private static byte[]? ResolveInputImageBytes(
        Acme.Product.Core.Entities.OperatorFlow flow,
        Guid targetNodeId,
        FlowDebugExecutionResult result,
        OperatorDebugResult? targetDebugResult,
        string? requestInputImageBase64)
    {
        return TryGetImageBytesFromSnapshot(targetDebugResult?.InputSnapshot)
            ?? TryGetNearestImageAcquisitionOutputBytes(flow, targetNodeId, result)
            ?? TryDecodeBase64(requestInputImageBase64);
    }

    private static byte[]? TryGetNearestImageAcquisitionOutputBytes(
        Acme.Product.Core.Entities.OperatorFlow flow,
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

    private static HashSet<Guid> CollectRelevantOperatorIds(
        Acme.Product.Core.Entities.OperatorFlow flow,
        Guid targetNodeId)
    {
        var visited = new HashSet<Guid> { targetNodeId };
        var stack = new Stack<Guid>();
        stack.Push(targetNodeId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var connection in flow.Connections.Where(item => item.TargetOperatorId == current))
            {
                if (visited.Add(connection.SourceOperatorId))
                {
                    stack.Push(connection.SourceOperatorId);
                }
            }
        }

        return visited;
    }

    private static PreviewFeedbackMetrics BuildPreviewMetrics(
        Dictionary<string, object> outputData,
        byte[]? outputImageBytes,
        string? errorMessage)
    {
        var detectionSummary = DetectionOutputInspector.Inspect(outputData);
        var areas = ExtractBlobAreas(outputData);
        if (areas.Count == 0 && detectionSummary.Detections.Count > 0)
        {
            areas = detectionSummary.Detections
                .Select(detection => (double)detection.Area)
                .ToList();
        }

        return new PreviewFeedbackMetrics
        {
            BlobCount = ResolveBlobCount(outputData, areas.Count, detectionSummary),
            AreaStats = areas.Count == 0
                ? null
                : new PreviewAreaStats
                {
                    Min = areas.Min(),
                    Max = areas.Max(),
                    Mean = areas.Average()
                },
            DetectionCount = detectionSummary.HasDetectionSemantics ? detectionSummary.DetectionCount : null,
            ObjectCount = detectionSummary.DeclaredCount ?? (detectionSummary.HasDetectionSemantics ? detectionSummary.DetectionCount : null),
            PerClassCount = detectionSummary.PerClassCount.Count > 0 ? detectionSummary.PerClassCount : null,
            SortedLabels = detectionSummary.ActualOrder.Count > 0 ? detectionSummary.ActualOrder : null,
            MinConfidence = detectionSummary.MinConfidence,
            MissingLabels = detectionSummary.MissingLabels.Count > 0 ? detectionSummary.MissingLabels : null,
            DuplicateLabels = detectionSummary.DuplicateLabels.Count > 0 ? detectionSummary.DuplicateLabels : null,
            Diagnostics = CreateDetectionDiagnostics(detectionSummary),
            BinaryRatio = ComputeBinaryRatio(outputImageBytes),
            ErrorMessage = errorMessage
        };
    }

    private static int ResolveBlobCount(
        Dictionary<string, object> outputData,
        int fallbackCount,
        DetectionOutputSummary detectionSummary)
    {
        foreach (var key in new[] { "BlobCount", "blobCount", "DefectCount", "defectCount", "DetectionCount", "detectionCount", "ObjectCount", "objectCount" })
        {
            if (outputData.TryGetValue(key, out var value) && TryReadInt(value, out var count))
            {
                return count;
            }
        }

        foreach (var key in new[] { "DetectionList", "detectionList", "Objects", "objects", "Defects", "defects", "Blobs", "blobs" })
        {
            if (outputData.TryGetValue(key, out var value) && TryGetCollectionCount(value, out var count))
            {
                return count;
            }
        }

        if (detectionSummary.HasDetectionSemantics)
        {
            return detectionSummary.DetectionCount;
        }

        return fallbackCount;
    }

    private static List<double> ExtractBlobAreas(Dictionary<string, object> outputData)
    {
        foreach (var key in new[] { "Defects", "defects", "Blobs", "blobs" })
        {
            if (!outputData.TryGetValue(key, out var value) || value == null)
            {
                continue;
            }

            var areas = new List<double>();
            foreach (var item in EnumerateItems(value))
            {
                if (TryReadArea(item, out var area))
                {
                    areas.Add(area);
                }
            }

            if (areas.Count > 0)
            {
                return areas;
            }
        }

        return new List<double>();
    }

    private static IEnumerable<object?> EnumerateItems(object value)
    {
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }

    private static bool TryReadArea(object? item, out double area)
    {
        if (TryReadDoubleField(item, "Area", out area) || TryReadDoubleField(item, "area", out area))
        {
            return true;
        }

        if ((TryReadDoubleField(item, "Width", out var width) || TryReadDoubleField(item, "width", out width)) &&
            (TryReadDoubleField(item, "Height", out var height) || TryReadDoubleField(item, "height", out height)))
        {
            area = width * height;
            return true;
        }

        area = 0;
        return false;
    }

    private static bool TryReadDoubleField(object? item, string fieldName, out double value)
    {
        if (item is IDictionary<string, object> typedDictionary &&
            typedDictionary.TryGetValue(fieldName, out var dictionaryValue))
        {
            return TryReadDouble(dictionaryValue, out value);
        }

        if (item is IDictionary dictionary && dictionary.Contains(fieldName))
        {
            return TryReadDouble(dictionary[fieldName], out value);
        }

        if (item is JsonElement element &&
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(fieldName, out var property))
        {
            return TryReadDouble(property, out value);
        }

        value = 0;
        return false;
    }

    private static bool TryReadInt(object? value, out int number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue:
                number = (int)longValue;
                return true;
            case double doubleValue:
                number = (int)doubleValue;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var jsonInt):
                number = jsonInt;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryReadDouble(object? value, out double number)
    {
        switch (value)
        {
            case double doubleValue:
                number = doubleValue;
                return true;
            case float floatValue:
                number = floatValue;
                return true;
            case int intValue:
                number = intValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case decimal decimalValue:
                number = (double)decimalValue;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var jsonDouble):
                number = jsonDouble;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryGetCollectionCount(object? value, out int count)
    {
        if (value is DetectionList detectionList)
        {
            count = detectionList.Count;
            return true;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            count = element.GetArrayLength();
            return true;
        }

        if (value is ICollection collection)
        {
            count = collection.Count;
            return true;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            count = enumerable.Cast<object?>().Count();
            return true;
        }

        count = 0;
        return false;
    }

    private static List<string>? CreateDetectionDiagnostics(DetectionOutputSummary detectionSummary)
    {
        if (!detectionSummary.HasDetectionSemantics)
        {
            return null;
        }

        var diagnostics = new List<string>();
        var expectedCount = detectionSummary.ExpectedCount ?? detectionSummary.ExpectedLabels.Count;

        if (detectionSummary.MissingLabels.Count > 0)
        {
            diagnostics.Add(PreviewDiagnosticTags.MissingExpectedClass);
        }

        if (detectionSummary.DuplicateLabels.Count > 0)
        {
            diagnostics.Add(PreviewDiagnosticTags.DuplicateDetectedClass);
        }

        if (expectedCount > 0 && detectionSummary.DetectionCount != expectedCount)
        {
            diagnostics.Add(PreviewDiagnosticTags.DetectionCountMismatch);
        }

        if (detectionSummary.MinConfidence.HasValue &&
            detectionSummary.MinConfidence.Value < detectionSummary.RequiredMinConfidence)
        {
            diagnostics.Add(PreviewDiagnosticTags.LowDetectionConfidence);
        }

        if (detectionSummary.ExpectedLabels.Count > 0 &&
            !detectionSummary.ExpectedLabels.SequenceEqual(detectionSummary.ActualOrder, StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add(PreviewDiagnosticTags.OrderMismatch);
        }

        return diagnostics.Count > 0 ? diagnostics : null;
    }

    private static double ComputeBinaryRatio(byte[]? outputImageBytes)
    {
        if (outputImageBytes == null || outputImageBytes.Length == 0)
        {
            return 0;
        }

        try
        {
            using var decoded = Cv2.ImDecode(outputImageBytes, ImreadModes.Unchanged);
            if (decoded.Empty())
            {
                return 0;
            }

            using var grayscale = decoded.Channels() == 1
                ? decoded.Clone()
                : decoded.CvtColor(ColorConversionCodes.BGR2GRAY);

            var nonZero = Cv2.CountNonZero(grayscale);
            return Math.Round(nonZero / (double)(grayscale.Rows * grayscale.Cols), 4);
        }
        catch
        {
            return 0;
        }
    }
}

public class PreviewFeedbackMetrics
{
    public int BlobCount { get; set; }
    public PreviewAreaStats? AreaStats { get; set; }
    public int? DetectionCount { get; set; }
    public int? ObjectCount { get; set; }
    public Dictionary<string, int>? PerClassCount { get; set; }
    public List<string>? SortedLabels { get; set; }
    public double? MinConfidence { get; set; }
    public List<string>? MissingLabels { get; set; }
    public List<string>? DuplicateLabels { get; set; }
    public List<string>? Diagnostics { get; set; }
    public double BinaryRatio { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PreviewAreaStats
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
}

/// <summary>
/// 预览节点请求
/// </summary>
public class PreviewNodeRequest
{
    /// <summary>
    /// 项目ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 目标节点ID（要预览的算子）
    /// </summary>
    public Guid TargetNodeId { get; set; }

    /// <summary>
    /// 调试会话ID（用于缓存复用）
    /// </summary>
    public Guid DebugSessionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 流程数据（包含所有算子和连接）
    /// </summary>
    public UpdateFlowRequest? FlowData { get; set; }

    /// <summary>
    /// 输入图像（Base64），可选
    /// </summary>
    public string? InputImageBase64 { get; set; }

    /// <summary>
    /// 目标节点的新参数（覆盖原参数）
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// 输出图像格式，默认 .png
    /// </summary>
    public string? ImageFormat { get; set; }
}

/// <summary>
/// 预览节点响应
/// </summary>
public class PreviewNodeResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 项目ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 目标节点ID
    /// </summary>
    public Guid TargetNodeId { get; set; }

    /// <summary>
    /// 调试会话ID
    /// </summary>
    public Guid DebugSessionId { get; set; }

    /// <summary>
    /// 输入图像（Base64），用于失败态下回显已到达目标节点的输入
    /// </summary>
    public string? InputImageBase64 { get; set; }

    /// <summary>
    /// 节点输出数据
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// 输出图像（Base64）
    /// </summary>
    public string? OutputImageBase64 { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
    public Guid? FailedOperatorId { get; set; }
    public string? FailedOperatorName { get; set; }
    public string? FailedOperatorType { get; set; }
    public PreviewFeedbackMetrics? Metrics { get; set; }

    /// <summary>
    /// 执行的算子列表（上游子图）
    /// </summary>
    public List<ExecutedOperatorInfo>? ExecutedOperators { get; set; }
}

/// <summary>
/// 执行的算子信息
/// </summary>
public class ExecutedOperatorInfo
{
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public int ExecutionOrder { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool IsSuccess { get; set; }
}

/// <summary>
/// 流程数据传输对象（用于前端序列化）
/// </summary>
