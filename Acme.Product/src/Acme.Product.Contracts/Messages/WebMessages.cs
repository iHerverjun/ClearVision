// WebMessages.cs
// 是否可恢复
// 作者：蘅芜君

using System.Text.Json.Serialization;

namespace Acme.Product.Contracts.Messages;

/// <summary>
/// 消息基类
/// </summary>
public abstract class MessageBase
{
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("type")]
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// 消息ID
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 命令基类（前端 -> 后端）
/// </summary>
public abstract class CommandBase : MessageBase
{
    public CommandBase()
    {
        MessageType = GetType().Name;
    }
}

/// <summary>
/// 事件基类（后端 -> 前端）
/// </summary>
public abstract class EventBase : MessageBase
{
    public EventBase()
    {
        MessageType = GetType().Name;
    }
}

/// <summary>
/// 执行算子命令
/// </summary>
public class ExecuteOperatorCommand : CommandBase
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid OperatorId { get; set; }

    /// <summary>
    /// 输入数据
    /// </summary>
    public Dictionary<string, object>? Inputs { get; set; }
}

/// <summary>
/// 更新流程命令
/// </summary>
public class UpdateFlowCommand : CommandBase
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 流程数据
    /// </summary>
    public FlowData? Flow { get; set; }
}

/// <summary>
/// 开始检测命令
/// </summary>
public class StartInspectionCommand : CommandBase
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 图像数据（Base64）
    /// </summary>
    public string? ImageBase64 { get; set; }

    /// <summary>
    /// 相机ID
    /// </summary>
    public string? CameraId { get; set; }
}

/// <summary>
/// 停止检测命令
/// </summary>
public class StopInspectionCommand : CommandBase
{
}

/// <summary>
/// 图像采集完成事件
/// </summary>
public class ImageAcquiredEvent : EventBase
{
    /// <summary>
    /// 图像ID
    /// </summary>
    public Guid ImageId { get; set; }

    /// <summary>
    /// 图像数据（Base64）
    /// </summary>
    public string ImageBase64 { get; set; } = string.Empty;

    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// 算子执行完成事件
/// </summary>
public class OperatorExecutedEvent : EventBase
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid OperatorId { get; set; }

    /// <summary>
    /// 算子名称
    /// </summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 输出数据
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 检测完成事件
/// </summary>
public class InspectionCompletedEvent : EventBase
{
    /// <summary>
    /// 结果ID
    /// </summary>
    public Guid ResultId { get; set; }

    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 检测状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 缺陷列表
    /// </summary>
    public List<DefectData> Defects { get; set; } = new();

    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// 结果图像（Base64，可选）
    /// </summary>
    public string? ResultImageBase64 { get; set; }

    /// <summary>
    /// 输出的额外数据字典（文本、数值等）
    /// </summary>
    public Dictionary<string, object>? OutputData { get; set; }

    /// <summary>
    /// 显式分析语义数据
    /// </summary>
    public AnalysisData? AnalysisData { get; set; }
}

/// <summary>
/// 显式分析语义数据
/// </summary>
public class AnalysisData
{
    public int Version { get; set; } = 1;

    public List<AnalysisCard> Cards { get; set; } = new();

    public AnalysisSummary? Summary { get; set; }
}

/// <summary>
/// 分析卡片
/// </summary>
public class AnalysisCard
{
    public string Id { get; set; } = string.Empty;

    public string Category { get; set; } = "generic";

    public Guid SourceOperatorId { get; set; }

    public string SourceOperatorType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "OK";

    public int Priority { get; set; }

    public List<AnalysisField> Fields { get; set; } = new();

    public Dictionary<string, object?>? Meta { get; set; }
}

/// <summary>
/// 分析字段
/// </summary>
public class AnalysisField
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public object? Value { get; set; }

    public string? Unit { get; set; }

    public string? DisplayHint { get; set; }

    public string? Status { get; set; }
}

/// <summary>
/// 分析汇总
/// </summary>
public class AnalysisSummary
{
    public int CardCount { get; set; }

    public List<string> Categories { get; set; } = new();
}

/// <summary>
/// 进度通知事件
/// </summary>
public class ProgressNotificationEvent : EventBase
{
    /// <summary>
    /// 进度百分比（0-100）
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// 当前算子名称
    /// </summary>
    public string? CurrentOperatorName { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// 流程数据
/// </summary>
public class FlowData
{
    public List<OperatorData> Operators { get; set; } = new();
    public List<ConnectionData> Connections { get; set; } = new();
}

/// <summary>
/// 算子数据
/// </summary>
public class OperatorData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// 连接数据
/// </summary>
public class ConnectionData
{
    public Guid SourceOperatorId { get; set; }
    public string SourcePort { get; set; } = string.Empty;
    public Guid TargetOperatorId { get; set; }
    public string TargetPort { get; set; } = string.Empty;
}

/// <summary>
/// 缺陷数据
/// </summary>
public class DefectData
{
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Confidence { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 图像数据流事件 - 用于实时推送处理过程中的图像
/// </summary>
public class ImageStreamEvent : EventBase
{
    /// <summary>
    /// 流类型：preview(预览), result(结果), intermediate(中间结果)
    /// </summary>
    public string StreamType { get; set; } = "preview";

    /// <summary>
    /// 图像数据（Base64，可选压缩）
    /// </summary>
    public string ImageBase64 { get; set; } = string.Empty;

    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 所属算子ID（中间结果）
    /// </summary>
    public Guid? OperatorId { get; set; }

    /// <summary>
    /// 算子名称
    /// </summary>
    public string? OperatorName { get; set; }

    /// <summary>
    /// 帧序号（用于实时视频流）
    /// </summary>
    public int FrameIndex { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public long TimestampMs { get; set; }

    /// <summary>
    /// 是否压缩
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// 压缩格式：jpeg, png
    /// </summary>
    public string? CompressionFormat { get; set; }

    /// <summary>
    /// 压缩质量（0-100，仅JPEG）
    /// </summary>
    public int CompressionQuality { get; set; } = 85;
}

/// <summary>
/// 算子中间结果事件 - 用于显示处理过程中的中间图像
/// </summary>
public class OperatorIntermediateResultEvent : EventBase
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid OperatorId { get; set; }

    /// <summary>
    /// 算子名称
    /// </summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>
    /// 算子在流程中的序号
    /// </summary>
    public int OperatorIndex { get; set; }

    /// <summary>
    /// 输入图像（Base64）
    /// </summary>
    public string? InputImageBase64 { get; set; }

    /// <summary>
    /// 输出图像（Base64）
    /// </summary>
    public string? OutputImageBase64 { get; set; }

    /// <summary>
    /// 处理参数
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// 处理状态
    /// </summary>
    public string Status { get; set; } = "Processing";

    /// <summary>
    /// 执行耗时（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// 错误通知事件
/// </summary>
public class ErrorNotificationEvent : EventBase
{
    /// <summary>
    /// 错误级别：warning, error, fatal
    /// </summary>
    public string Level { get; set; } = "error";

    /// <summary>
    /// 错误代码
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 详细错误信息
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// 相关算子ID
    /// </summary>
    public Guid? OperatorId { get; set; }

    /// <summary>
    /// 是否可恢复
    /// </summary>
    public bool IsRecoverable { get; set; } = false;
}
