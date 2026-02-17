using Acme.Product.Core.Enums;

namespace Acme.Product.Application.DTOs;

/// <summary>
/// 检测结果数据传输对象
/// </summary>
public class InspectionResultDto
{
    /// <summary>
    /// 结果ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 检测状态
    /// </summary>
    public InspectionStatus Status { get; set; }

    /// <summary>
    /// 缺陷列表
    /// </summary>
    public List<DefectDto> Defects { get; set; } = new();

    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// 图像ID
    /// </summary>
    public Guid? ImageId { get; set; }

    /// <summary>
    /// 置信度分数（0-1）
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 检测时间戳
    /// </summary>
    public DateTime InspectionTime { get; set; }

    /// <summary>
    /// 输出图像（Base64编码）
    /// </summary>
    public string? OutputImage { get; set; }
}

/// <summary>
/// 缺陷数据传输对象
/// </summary>
public class DefectDto
{
    /// <summary>
    /// 缺陷ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 缺陷类型
    /// </summary>
    public DefectType Type { get; set; }

    /// <summary>
    /// X坐标
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y坐标
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 置信度分数（0-1）
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// 缺陷描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 图像标注数据
    /// </summary>
    public string? AnnotationData { get; set; }
}

/// <summary>
/// 执行检测请求
/// </summary>
public class ExecuteInspectionRequest
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 图像数据（Base64编码）
    /// </summary>
    public string? ImageBase64 { get; set; }

    /// <summary>
    /// 相机ID（如果使用相机采集）
    /// </summary>
    public string? CameraId { get; set; }

    /// <summary>
    /// 流程数据（包含前端编辑过的算子参数）
    /// </summary>
    public OperatorFlowDto? FlowData { get; set; }
}

/// <summary>
/// 启动实时检测请求
/// 【第二优先级】支持相机驱动和流程驱动两种模式
/// </summary>
public class StartRealtimeInspectionRequest
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// 相机ID（可选）
    /// </summary>
    public string? CameraId { get; set; }

    /// <summary>
    /// 运行模式：camera（相机驱动）/ flow（流程驱动）
    /// </summary>
    public string? RunMode { get; set; }

    /// <summary>
    /// 流程数据（流程驱动模式下需要）
    /// </summary>
    public OperatorFlowDto? FlowData { get; set; }
}

/// <summary>
/// 停止实时检测请求
/// </summary>
public class StopRealtimeInspectionRequest
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; set; }
}
