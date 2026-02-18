// TaskExtensions.cs
// 安全地触发异步任务而不等待结果，捕获并记录异常（带泛型结果版本）
// 作者：蘅芜君

using Microsoft.Extensions.Logging;

namespace Acme.Product.Desktop.Extensions;

/// <summary>
/// 任务扩展方法
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// 安全地触发异步任务而不等待结果，捕获并记录异常
    /// 用于将async void事件处理器转换为安全的异步模式
    /// </summary>
    /// <param name="task">要执行的任务</param>
    /// <param name="logger">日志记录器</param>
    public static async void SafeFireAndForget(this Task task, ILogger logger)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "异步任务执行过程中发生未捕获异常");
        }
    }

    /// <summary>
    /// 安全地触发异步任务而不等待结果，捕获并记录异常（带泛型结果版本）
    /// </summary>
    /// <param name="task">要执行的任务</param>
    /// <param name="logger">日志记录器</param>
    public static async void SafeFireAndForget<T>(this Task<T> task, ILogger logger)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "异步任务执行过程中发生未捕获异常");
        }
    }
}
