// OperatorBase.cs
// 算子执行器抽象基类 - 提供统一的参数获取、输入检查、日志记录和执行计时功能
// 作者：蘅芜君

using System.Diagnostics;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.ValueObjects;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// 算子执行器抽象基类
/// 提供统一的参数获取、输入检查、日志记录和执行计时功能
/// </summary>
public abstract class OperatorBase : IOperatorExecutor
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// 算子类型
    /// </summary>
    public abstract OperatorType OperatorType { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    protected OperatorBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行算子（包装方法，提供统一的计时和日志）
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="inputs">输入数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    public async Task<OperatorExecutionOutput> ExecuteAsync(
        Operator @operator,
        Dictionary<string, object>? inputs = null,
        CancellationToken cancellationToken = default)
    {
        // Sprint 1 Task 1.1: 使用带生命周期管理的方法
        return await ExecuteWithLifecycleAsync(@operator, inputs, cancellationToken);
    }

    /// <summary>
    /// 执行算子（带生命周期管理 - Sprint 1 Task 1.1）。
    /// 
    /// 自动管理 ImageWrapper 的引用计数：
    /// 1. 执行前：输入中的 ImageWrapper 引用计数已由上游 AddRef
    /// 2. 执行后：自动 Release 所有输入中的 ImageWrapper
    /// 
    /// 算子开发约定（框架层通过 Code Review 强制检查）：
    ///
    /// 读操作：image.MatReadOnly  — 不触发 Clone/Pool，多并发安全
    ///
    /// 写操作：var dst = image.GetWritableMat(); // 可能从 Pool 取（CoW）
    ///          Cv2.SomeFilter(dst, dst, ...);    // 就地修改
    ///          return new ImageWrapper(dst);     // 输出，引用计数重置为 1
    ///          // 若不作为输出，需归还：MatPool.Shared.Return(dst)
    ///
    /// 禁止在算子内部手动调用 AddRef() / Release()。
    /// 禁止在算子内部直接调用 mat.Dispose()。
    /// </summary>
    public async Task<OperatorExecutionOutput> ExecuteWithLifecycleAsync(
        Operator @operator,
        Dictionary<string, object>? inputs = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.LogDebug("[{OperatorType}] 开始执行, 算子ID={OperatorId}, 名称={OperatorName}",
            OperatorType, @operator.Id, @operator.Name);

        try
        {
            // 检查取消请求
            cancellationToken.ThrowIfCancellationRequested();

            // 执行核心逻辑
            var result = await ExecuteCoreAsync(@operator, inputs, cancellationToken);
            stopwatch.Stop();

            // 设置执行时间
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            if (result.IsSuccess)
            {
                Logger.LogInformation(
                    "[{OperatorType}] 执行完成, 算子ID={OperatorId}, 耗时={ElapsedMs}ms, 成功={IsSuccess}",
                    OperatorType, @operator.Id, stopwatch.ElapsedMilliseconds, true);
            }
            else
            {
                Logger.LogWarning(
                    "[{OperatorType}] 执行失败, 算子ID={OperatorId}, 耗时={ElapsedMs}ms, 错误={ErrorMessage}",
                    OperatorType, @operator.Id, stopwatch.ElapsedMilliseconds, result.ErrorMessage);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Logger.LogInformation(
                "[{OperatorType}] 执行被取消, 算子ID={OperatorId}, 耗时={ElapsedMs}ms",
                OperatorType, @operator.Id, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex,
                "[{OperatorType}] 执行异常, 算子ID={OperatorId}, 耗时={ElapsedMs}ms, 错误={ErrorMessage}",
                OperatorType, @operator.Id, stopwatch.ElapsedMilliseconds, ex.Message);

            return OperatorExecutionOutput.Failure($"执行失败: {ex.Message}");
        }
        finally
        {
            // Sprint 1 Task 1.1: 释放输入中的 ImageWrapper 引用
            if (inputs != null)
            {
                foreach (var value in inputs.Values)
                {
                    if (value is ImageWrapper img)
                    {
                        img.Release();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 执行算子核心逻辑（子类实现）
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="inputs">输入数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    protected abstract Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken);

    /// <summary>
    /// 验证算子参数（子类实现）
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <returns>验证结果</returns>
    public abstract ValidationResult ValidateParameters(Operator @operator);

    #region 参数获取方法

    /// <summary>
    /// 获取参数值（支持类型转换）
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>参数值</returns>
    protected T GetParam<T>(Operator @operator, string paramName, T defaultValue)
    {
        var param = @operator.Parameters.FirstOrDefault(p => p.Name == paramName);
        if (param?.Value == null)
        {
            return defaultValue;
        }

        try
        {
            // 处理 System.Text.Json 反序列化的 JsonElement
            if (param.Value is System.Text.Json.JsonElement jsonElement)
            {
                return ConvertJsonElement<T>(jsonElement, defaultValue);
            }

            return (T)Convert.ChangeType(param.Value, typeof(T));
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex,
                "[{OperatorType}] 参数转换失败: {ParamName}, 值={Value}, 目标类型={TargetType}",
                OperatorType, paramName, param.Value, typeof(T).Name);
            return defaultValue;
        }
    }

    /// <summary>
    /// 获取可选参数值（可能为 null）
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <returns>参数值，不存在时返回 null</returns>
    protected T? GetParamOrNull<T>(Operator @operator, string paramName) where T : struct
    {
        var param = @operator.Parameters.FirstOrDefault(p => p.Name == paramName);
        if (param?.Value == null)
        {
            return null;
        }

        try
        {
            if (param.Value is System.Text.Json.JsonElement jsonElement)
            {
                return ConvertJsonElement<T?>(jsonElement, null);
            }

            return (T)Convert.ChangeType(param.Value, typeof(T));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取字符串参数
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>参数值</returns>
    protected string GetStringParam(Operator @operator, string paramName, string defaultValue = "")
    {
        return GetParam(@operator, paramName, defaultValue);
    }

    /// <summary>
    /// 获取整型参数
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>参数值</returns>
    protected int GetIntParam(Operator @operator, string paramName, int defaultValue, int? min = null, int? max = null)
    {
        var value = GetParam(@operator, paramName, defaultValue);

        if (min.HasValue && value < min.Value)
            value = min.Value;
        if (max.HasValue && value > max.Value)
            value = max.Value;

        return value;
    }

    /// <summary>
    /// 获取双精度浮点参数
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>参数值</returns>
    protected double GetDoubleParam(Operator @operator, string paramName, double defaultValue, double? min = null, double? max = null)
    {
        var value = GetParam(@operator, paramName, defaultValue);

        if (min.HasValue && value < min.Value)
            value = min.Value;
        if (max.HasValue && value > max.Value)
            value = max.Value;

        return value;
    }

    /// <summary>
    /// 获取单精度浮点参数
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>参数值</returns>
    protected float GetFloatParam(Operator @operator, string paramName, float defaultValue, float? min = null, float? max = null)
    {
        var value = GetParam(@operator, paramName, defaultValue);

        if (min.HasValue && value < min.Value)
            value = min.Value;
        if (max.HasValue && value > max.Value)
            value = max.Value;

        return value;
    }

    /// <summary>
    /// 获取布尔参数
    /// </summary>
    /// <param name="operator">算子实体</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>参数值</returns>
    protected bool GetBoolParam(Operator @operator, string paramName, bool defaultValue)
    {
        return GetParam(@operator, paramName, defaultValue);
    }

    #endregion

    #region 输入处理方法

    /// <summary>
    /// 尝试从输入中获取图像数据
    /// 支持 ImageWrapper、byte[] 或 Mat 类型
    /// </summary>
    /// <param name="inputs">输入字典</param>
    /// <param name="key">图像键名，默认为 "Image"</param>
    /// <param name="image">输出图像包装器</param>
    /// <returns>是否成功获取</returns>
    protected bool TryGetInputImage(Dictionary<string, object>? inputs, string key, out ImageWrapper? image)
    {
        image = null;

        if (inputs == null)
        {
            Logger.LogDebug("[{OperatorType}] 输入字典为空", OperatorType);
            return false;
        }

        if (!inputs.TryGetValue(key, out var value))
        {
            Logger.LogDebug("[{OperatorType}] 未找到图像输入键: {Key}", OperatorType, key);
            return false;
        }

        if (ImageWrapper.TryGetFromObject(value, out image))
        {
            Logger.LogDebug("[{OperatorType}] 成功获取图像: {Key}, 类型={Type}",
                OperatorType, key, value?.GetType().Name ?? "null");
            return true;
        }

        Logger.LogWarning("[{OperatorType}] 图像类型不支持: {Key}, 类型={Type}",
            OperatorType, key, value?.GetType().Name ?? "null");
        return false;
    }

    /// <summary>
    /// 尝试从输入中获取图像数据（默认键名 "Image"）
    /// </summary>
    /// <param name="inputs">输入字典</param>
    /// <param name="image">输出图像包装器</param>
    /// <returns>是否成功获取</returns>
    protected bool TryGetInputImage(Dictionary<string, object>? inputs, out ImageWrapper? image)
    {
        return TryGetInputImage(inputs, "Image", out image);
    }

    /// <summary>
    /// 获取输入值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="inputs">输入字典</param>
    /// <param name="key">键名</param>
    /// <param name="value">输出值</param>
    /// <returns>是否成功获取</returns>
    protected bool TryGetInputValue<T>(Dictionary<string, object>? inputs, string key, out T? value)
    {
        value = default;

        if (inputs == null)
            return false;
        if (!inputs.TryGetValue(key, out var obj))
            return false;

        try
        {
            if (obj is T t)
            {
                value = t;
                return true;
            }

            value = (T?)Convert.ChangeType(obj, typeof(T));
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 输出处理方法 (P0: ImageWrapper零拷贝输出)

    /// <summary>
    /// 创建图像输出字典 - 使用ImageWrapper实现零拷贝传递 (P0优先级)
    /// </summary>
    /// <param name="mat">输出图像Mat</param>
    /// <param name="additionalData">附加数据</param>
    /// <returns>输出字典，包含ImageWrapper</returns>
    protected Dictionary<string, object> CreateImageOutput(Mat mat, Dictionary<string, object>? additionalData = null)
    {
        var output = new Dictionary<string, object>
        {
            { "Image", new ImageWrapper(mat) },
            { "Width", mat.Width },
            { "Height", mat.Height }
        };

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                if (!output.ContainsKey(kvp.Key))
                {
                    output[kvp.Key] = kvp.Value;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// 创建图像输出字典（兼容模式）- 支持ImageWrapper或byte[] (P0优先级)
    /// </summary>
    /// <param name="mat">输出图像Mat</param>
    /// <param name="useZeroCopy">是否使用零拷贝（ImageWrapper）</param>
    /// <param name="additionalData">附加数据</param>
    /// <returns>输出字典</returns>
    protected Dictionary<string, object> CreateImageOutput(Mat mat, bool useZeroCopy, Dictionary<string, object>? additionalData = null)
    {
        var output = new Dictionary<string, object>();

        if (useZeroCopy)
        {
            output["Image"] = new ImageWrapper(mat);
        }
        else
        {
            // 兼容模式：编码为byte[]
            output["Image"] = mat.ToBytes(".png");
        }

        output["Width"] = mat.Width;
        output["Height"] = mat.Height;

        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                if (!output.ContainsKey(kvp.Key))
                {
                    output[kvp.Key] = kvp.Value;
                }
            }
        }

        return output;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 转换 JsonElement 为目标类型
    /// </summary>
    private static T? ConvertJsonElement<T>(System.Text.Json.JsonElement element, T? defaultValue)
    {
        try
        {
            var targetType = typeof(T);

            if (targetType == typeof(string))
                return (T?)(object?)(element.ToString() ?? string.Empty);
            if (targetType == typeof(int) || targetType == typeof(int?))
                return (T?)(object)element.GetInt32();
            if (targetType == typeof(double) || targetType == typeof(double?))
                return (T?)(object)element.GetDouble();
            if (targetType == typeof(float) || targetType == typeof(float?))
                return (T?)(object)element.GetSingle();
            if (targetType == typeof(bool) || targetType == typeof(bool?))
                return (T?)(object)element.GetBoolean();
            if (targetType == typeof(long) || targetType == typeof(long?))
                return (T?)(object)element.GetInt64();

            // 其他类型尝试字符串转换
            return (T?)Convert.ChangeType(element.ToString(), targetType);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 在后台线程执行 CPU 密集型操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    protected Task<T> RunCpuBoundWork<T>(Func<T> action, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return action();
        }, cancellationToken);
    }

    /// <summary>
    /// 在后台线程执行 CPU 密集型操作（无返回值）
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected Task RunCpuBoundWork(Action action, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
        }, cancellationToken);
    }

    #endregion
}
