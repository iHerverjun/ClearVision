// OperatorDescriptorModel.cs
// 算子描述模型
// 提供 IOperatorDescriptor 的默认实现与序列化载体
// 作者：蘅芜君
using System.Collections.Generic;
using Acme.OperatorLibrary.Abstractions.Contracts;

namespace Acme.OperatorLibrary.Abstractions.Models;

/// <summary>
/// Lightweight descriptor model used by package consumers without depending on host entities.
/// </summary>
public sealed class OperatorDescriptorModel : IOperatorDescriptor
{
    public int Type { get; init; }

    public string TypeName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string? IconName { get; init; }

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public IReadOnlyList<IOperatorPort> InputPorts { get; init; } = [];

    public IReadOnlyList<IOperatorPort> OutputPorts { get; init; } = [];

    public IReadOnlyList<IOperatorParameter> Parameters { get; init; } = [];
}

public sealed class OperatorPortModel : IOperatorPort
{
    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public bool IsRequired { get; init; }

    public string? Description { get; init; }
}

public sealed class OperatorParameterModel : IOperatorParameter
{
    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string DataType { get; init; } = string.Empty;

    public object? DefaultValue { get; init; }

    public object? MinValue { get; init; }

    public object? MaxValue { get; init; }

    public bool IsRequired { get; init; } = true;

    public string? Description { get; init; }

    public IReadOnlyList<IOperatorParameterOption> Options { get; init; } = [];
}

public sealed class OperatorParameterOptionModel : IOperatorParameterOption
{
    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}
