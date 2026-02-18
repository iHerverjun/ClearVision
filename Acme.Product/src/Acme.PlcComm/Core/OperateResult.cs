// OperateResult.cs
// 创建失败结果(使用默认错误码-1)
// 作者：蘅芜君

namespace Acme.PlcComm.Core;

/// <summary>
/// 统一的PLC操作结果封装，借鉴HSL的Result模式
/// </summary>
public class OperateResult
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误代码
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperateResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static OperateResult Failure(int code, string msg) => 
        new() { IsSuccess = false, ErrorCode = code, Message = msg };

    /// <summary>
    /// 创建失败结果(使用默认错误码-1)
    /// </summary>
    public static OperateResult Failure(string msg) => 
        new() { IsSuccess = false, ErrorCode = -1, Message = msg };
}

/// <summary>
/// 泛型操作结果，包含返回数据
/// </summary>
public class OperateResult<T> : OperateResult
{
    /// <summary>
    /// 操作返回的数据内容
    /// </summary>
    public T? Content { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static OperateResult<T> Success(T content) => 
        new() { IsSuccess = true, Content = content };

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static new OperateResult<T> Failure(int code, string msg) => 
        new() { IsSuccess = false, ErrorCode = code, Message = msg };

    /// <summary>
    /// 创建失败结果(使用默认错误码-1)
    /// </summary>
    public static new OperateResult<T> Failure(string msg) => 
        new() { IsSuccess = false, ErrorCode = -1, Message = msg };
}
