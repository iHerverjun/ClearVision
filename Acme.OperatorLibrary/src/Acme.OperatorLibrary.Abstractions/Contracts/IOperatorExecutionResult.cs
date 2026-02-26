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
