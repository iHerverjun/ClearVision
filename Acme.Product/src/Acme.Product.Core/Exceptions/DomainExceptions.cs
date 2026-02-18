// DomainExceptions.cs
// 相机ID
// 作者：蘅芜君

namespace Acme.Product.Core.Exceptions;

/// <summary>
/// 领域异常基类 - 所有领域层异常的基类
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// 错误详情
    /// </summary>
    public Dictionary<string, object>? Details { get; }

    protected DomainException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected DomainException(string message, string errorCode, Dictionary<string, object>? details)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }

    protected DomainException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// 视觉检测领域异常
/// </summary>
public class VisionException : DomainException
{
    public VisionException(string message)
        : base(message, "VISION_ERROR")
    {
    }

    public VisionException(string message, Dictionary<string, object>? details)
        : base(message, "VISION_ERROR", details)
    {
    }

    public VisionException(string message, Exception innerException)
        : base(message, "VISION_ERROR", innerException)
    {
    }
}

/// <summary>
/// 算子执行异常
/// </summary>
public class OperatorExecutionException : DomainException
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid OperatorId { get; }

    /// <summary>
    /// 算子名称
    /// </summary>
    public string OperatorName { get; }

    public OperatorExecutionException(Guid operatorId, string operatorName, string message)
        : base($"算子 '{operatorName}' 执行失败: {message}", "OPERATOR_EXECUTION_ERROR")
    {
        OperatorId = operatorId;
        OperatorName = operatorName;
    }

    public OperatorExecutionException(Guid operatorId, string operatorName, string message, Exception innerException)
        : base($"算子 '{operatorName}' 执行失败: {message}", "OPERATOR_EXECUTION_ERROR", innerException)
    {
        OperatorId = operatorId;
        OperatorName = operatorName;
    }
}

/// <summary>
/// 流程执行异常
/// </summary>
public class FlowExecutionException : DomainException
{
    /// <summary>
    /// 流程ID
    /// </summary>
    public Guid FlowId { get; }

    public FlowExecutionException(Guid flowId, string message)
        : base($"流程执行失败: {message}", "FLOW_EXECUTION_ERROR")
    {
        FlowId = flowId;
    }

    public FlowExecutionException(Guid flowId, string message, Exception innerException)
        : base($"流程执行失败: {message}", "FLOW_EXECUTION_ERROR", innerException)
    {
        FlowId = flowId;
    }
}

/// <summary>
/// 工程不存在异常
/// </summary>
public class ProjectNotFoundException : DomainException
{
    /// <summary>
    /// 工程ID
    /// </summary>
    public Guid ProjectId { get; }

    public ProjectNotFoundException(Guid projectId)
        : base($"工程不存在: {projectId}", "PROJECT_NOT_FOUND")
    {
        ProjectId = projectId;
    }
}

/// <summary>
/// 算子不存在异常
/// </summary>
public class OperatorNotFoundException : DomainException
{
    /// <summary>
    /// 算子ID
    /// </summary>
    public Guid OperatorId { get; }

    public OperatorNotFoundException(Guid operatorId)
        : base($"算子不存在: {operatorId}", "OPERATOR_NOT_FOUND")
    {
        OperatorId = operatorId;
    }
}

/// <summary>
/// 验证异常
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>
    /// 验证错误列表
    /// </summary>
    public List<ValidationError> Errors { get; }

    public ValidationException(string message, List<ValidationError> errors)
        : base(message, "VALIDATION_ERROR")
    {
        Errors = errors;
    }
}

/// <summary>
/// 验证错误
/// </summary>
public class ValidationError
{
    public string PropertyName { get; }
    public string ErrorMessage { get; }

    public ValidationError(string propertyName, string errorMessage)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// 图像处理异常
/// </summary>
public class ImageProcessingException : DomainException
{
    public ImageProcessingException(string message)
        : base(message, "IMAGE_PROCESSING_ERROR")
    {
    }

    public ImageProcessingException(string message, Exception innerException)
        : base(message, "IMAGE_PROCESSING_ERROR", innerException)
    {
    }
}

/// <summary>
/// 相机异常
/// </summary>
public class CameraException : DomainException
{
    /// <summary>
    /// 相机ID
    /// </summary>
    public string? CameraId { get; }

    public CameraException(string message)
        : base(message, "CAMERA_ERROR")
    {
    }

    public CameraException(string message, string cameraId)
        : base($"相机 '{cameraId}' 错误: {message}", "CAMERA_ERROR")
    {
        CameraId = cameraId;
    }

    public CameraException(string message, Exception innerException)
        : base(message, "CAMERA_ERROR", innerException)
    {
    }
}
