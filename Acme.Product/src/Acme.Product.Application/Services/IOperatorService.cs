// IOperatorService.cs
// 更新算子请求
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Operators;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Application.Services;

/// <summary>
/// 算子服务接口
/// Sprint 4: S4-004 实现
/// </summary>
public interface IOperatorService
{
    /// <summary>
    /// 获取算子库元数据列表
    /// </summary>
    Task<IEnumerable<OperatorMetadataDto>> GetLibraryAsync();

    /// <summary>
    /// 根据ID获取算子
    /// </summary>
    Task<OperatorDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// 根据类型获取算子
    /// </summary>
    Task<OperatorDto?> GetByTypeAsync(OperatorType type);

    /// <summary>
    /// 创建算子
    /// </summary>
    Task<OperatorDto> CreateAsync(CreateOperatorRequest request);

    /// <summary>
    /// 更新算子
    /// </summary>
    Task<OperatorDto> UpdateAsync(Guid id, UpdateOperatorRequest request);

    /// <summary>
    /// 删除算子
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// 验证算子参数
    /// </summary>
    Task<ValidationResultDto> ValidateParametersAsync(Guid operatorId, Dictionary<string, object> parameters);

    /// <summary>
    /// 获取算子类型列表
    /// </summary>
    Task<IEnumerable<OperatorTypeInfoDto>> GetOperatorTypesAsync();

    /// <summary>
    /// 获取算子元数据（包含端口和参数定义）
    /// </summary>
    Task<OperatorMetadataDto?> GetMetadataAsync(OperatorType type);
}

/// <summary>
/// 算子元数据传输对象
/// </summary>
public class OperatorMetadataDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PortDefinitionDto> Inputs { get; set; } = new();
    public List<PortDefinitionDto> Outputs { get; set; } = new();
    public List<ParameterDefinitionDto> Parameters { get; set; } = new();
}

/// <summary>
/// 端口定义DTO
/// </summary>
public class PortDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PortDataType DataType { get; set; }
    public bool IsRequired { get; set; } = true;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 参数定义DTO
/// </summary>
public class ParameterDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public object? DefaultValue { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public bool IsRequired { get; set; } = true;
    public List<ParameterOptionDto>? Options { get; set; }
}

/// <summary>
/// 参数选项DTO
/// </summary>
public class ParameterOptionDto
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 算子类型信息DTO
/// </summary>
public class OperatorTypeInfoDto
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// 验证结果DTO
/// </summary>
public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 创建算子请求
/// </summary>
public class CreateOperatorRequest
{
    public OperatorType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ParameterRequest>? Parameters { get; set; }
}

/// <summary>
/// 参数请求
/// </summary>
public class ParameterRequest
{
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
}

/// <summary>
/// 更新算子请求
/// </summary>
public class UpdateOperatorRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<ParameterRequest>? Parameters { get; set; }
}
