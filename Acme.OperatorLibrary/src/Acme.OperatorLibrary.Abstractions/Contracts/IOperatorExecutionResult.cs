// IOperatorExecutionResult.cs
// 算子执行结果接口
// 定义算子输出、状态与错误信息的数据契约
// 作者：蘅芜君
using System.Collections.Generic;

namespace Acme.OperatorLibrary.Abstractions.Contracts;

/// <summary>
/// Lightweight, host-agnostic execution result contract.
/// </summary>
public interface IOperatorExecutionResult
{
    bool IsSuccess { get; }

    IDictionary<string, object?>? OutputData { get; }

    long ExecutionTimeMs { get; }

    string? ErrorMessage { get; }
}
