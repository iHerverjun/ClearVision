// CoreTypeAdapters.cs
// 核心类型适配器
// 提供 OperatorLibrary 与核心类型之间的适配转换工具
// 作者：蘅芜君
using System.Collections.Generic;
using System.Linq;
using Acme.OperatorLibrary.Abstractions.Contracts;
using Acme.OperatorLibrary.Abstractions.Models;

#if ACME_OPERATORLIB_PACKAGE
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;
#endif

namespace Acme.OperatorLibrary.Abstractions.Adapters;

/// <summary>
/// Mapping extensions between host concrete types and package-level abstraction models.
/// </summary>
public static class CoreTypeAdapters
{
#if ACME_OPERATORLIB_PACKAGE
    public static OperatorExecutionResultModel ToModel(this OperatorExecutionOutput source)
    {
        return new OperatorExecutionResultModel
        {
            IsSuccess = source.IsSuccess,
            OutputData = source.OutputData?.ToDictionary(
                item => item.Key,
                item => (object?)item.Value,
                System.StringComparer.Ordinal),
            ExecutionTimeMs = source.ExecutionTimeMs,
            ErrorMessage = source.ErrorMessage
        };
    }

    public static OperatorDescriptorModel ToModel(this OperatorMetadata source)
    {
        return new OperatorDescriptorModel
        {
            Type = (int)source.Type,
            TypeName = source.Type.ToString(),
            DisplayName = source.DisplayName,
            Description = source.Description,
            Category = source.Category,
            IconName = source.IconName,
            Keywords = source.Keywords?.ToArray() ?? [],
            InputPorts = source.InputPorts.Select(ToModel).Cast<IOperatorPort>().ToArray(),
            OutputPorts = source.OutputPorts.Select(ToModel).Cast<IOperatorPort>().ToArray(),
            Parameters = source.Parameters.Select(ToModel).Cast<IOperatorParameter>().ToArray()
        };
    }

    private static OperatorPortModel ToModel(this PortDefinition source)
    {
        return new OperatorPortModel
        {
            Name = source.Name,
            DisplayName = source.DisplayName,
            DataType = source.DataType.ToString(),
            IsRequired = source.IsRequired,
            Description = source.Description
        };
    }

    private static OperatorParameterModel ToModel(this ParameterDefinition source)
    {
        var options = source.Options?.Select(ToModel).Cast<IOperatorParameterOption>().ToArray()
            ?? [];

        return new OperatorParameterModel
        {
            Name = source.Name,
            DisplayName = source.DisplayName,
            DataType = source.DataType,
            DefaultValue = source.DefaultValue,
            MinValue = source.MinValue,
            MaxValue = source.MaxValue,
            IsRequired = source.IsRequired,
            Description = source.Description,
            Options = options
        };
    }

    private static OperatorParameterOptionModel ToModel(this ParameterOption source)
    {
        return new OperatorParameterOptionModel
        {
            Label = source.Label,
            Value = source.Value
        };
    }
#endif
}
