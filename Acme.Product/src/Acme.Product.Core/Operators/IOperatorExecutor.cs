// IOperatorExecutor.cs
// 错误信息
// 作者：蘅芜君

using Acme.Product.Core.Entities;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Core.Operators;

/// <summary>
/// 算子执行器接口
/// </summary>
public interface IOperatorExecutor
{
    /// <summary>
    /// 算子类型
    /// </summary>
    Enums.OperatorType OperatorType { get; }

    /// <summary>
    /// 执行算子
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="inputs">输入数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<OperatorExecutionOutput> ExecuteAsync(
        Operator @operator,
        Dictionary<string, object>? inputs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证算子参数
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <returns>验证结果</returns>
    ValidationResult ValidateParameters(Operator @operator);
}

/// <summary>
/// 算子执行输出
/// </summary>
public class OperatorExecutionOutput
{
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

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperatorExecutionOutput Success(Dictionary<string, object>? outputData = null, long executionTimeMs = 0)
    {
        return new OperatorExecutionOutput
        {
            IsSuccess = true,
            OutputData = outputData,
            ExecutionTimeMs = executionTimeMs
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OperatorExecutionOutput Failure(string errorMessage)
    {
        return new OperatorExecutionOutput
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(string error)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = new List<string> { error }
        };
    }
}
