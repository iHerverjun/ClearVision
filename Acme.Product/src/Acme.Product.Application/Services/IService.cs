// IService.cs
// 操作结果封装，用于统一的成功// 功能实现失败处理。
// 作者：蘅芜君

namespace Acme.Product.Application.Services;

/// <summary>
/// 应用服务标记接口。
/// 用于服务扫描注册。
/// </summary>
public interface IApplicationService
{
}

/// <summary>
/// 命令接口，表示会产生副作用的操作。
/// </summary>
/// <typeparam name="TResult">命令执行结果类型</typeparam>
public interface ICommand<TResult>
{
}

/// <summary>
/// 查询接口，表示只读操作。
/// </summary>
/// <typeparam name="TResult">查询结果类型</typeparam>
public interface IQuery<TResult>
{
}

/// <summary>
/// 操作结果封装，用于统一的成功/失败处理。
/// </summary>
/// <typeparam name="T">结果数据类型</typeparam>
public sealed record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string[] Errors { get; init; } = [];

    private Result() { }

    public static Result<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value
    };

    public static Result<T> Failure(params string[] errors) => new()
    {
        IsSuccess = false,
        Errors = errors
    };
}
