// IOperatorDescriptor.cs
// 算子描述接口
// 定义算子元数据与端口信息的统一契约
// 作者：蘅芜君
using System.Collections.Generic;

namespace Acme.OperatorLibrary.Abstractions.Contracts;

public interface IOperatorDescriptor
{
    int Type { get; }

    string TypeName { get; }

    string DisplayName { get; }

    string Description { get; }

    string Category { get; }

    string? IconName { get; }

    IReadOnlyList<string> Keywords { get; }

    IReadOnlyList<IOperatorPort> InputPorts { get; }

    IReadOnlyList<IOperatorPort> OutputPorts { get; }

    IReadOnlyList<IOperatorParameter> Parameters { get; }
}

public interface IOperatorPort
{
    string Name { get; }

    string DisplayName { get; }

    string DataType { get; }

    bool IsRequired { get; }

    string? Description { get; }
}

public interface IOperatorParameter
{
    string Name { get; }

    string DisplayName { get; }

    string DataType { get; }

    object? DefaultValue { get; }

    object? MinValue { get; }

    object? MaxValue { get; }

    bool IsRequired { get; }

    string? Description { get; }

    IReadOnlyList<IOperatorParameterOption> Options { get; }
}

public interface IOperatorParameterOption
{
    string Label { get; }

    string Value { get; }
}
