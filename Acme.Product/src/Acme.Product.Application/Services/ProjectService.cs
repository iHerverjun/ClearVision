using System.Text.Json;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Exceptions;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Application.Services;

/// <summary>
/// 工程应用服务
/// </summary>
public class ProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectFlowStorage _flowStorage;
    private readonly IOperatorFactory _operatorFactory;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ProjectService(IProjectRepository projectRepository, IProjectFlowStorage flowStorage, IOperatorFactory operatorFactory)
    {
        _projectRepository = projectRepository;
        _flowStorage = flowStorage;
        _operatorFactory = operatorFactory;
    }

    /// <summary>
    /// 创建工程
    /// </summary>
    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request)
    {
        var project = new Project(request.Name, request.Description);
        await _projectRepository.AddAsync(project);

        // 如果创建时带有流程（通常是空的，但为了完整性）
        if (request.Flow != null)
        {
            var json = JsonSerializer.Serialize(request.Flow);
            await _flowStorage.SaveFlowJsonAsync(project.Id, json);
        }

        return MapToDto(project);
    }

    /// <summary>
    /// 获取工程
    /// </summary>
    public async Task<ProjectDto?> GetByIdAsync(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            return null;

        var dto = MapToDto(project);

        // 从文件加载流程数据覆盖 DB 数据 (如果有)
        var flowJson = await _flowStorage.LoadFlowJsonAsync(id);
        if (!string.IsNullOrEmpty(flowJson))
        {
            try
            {
                var flowDto = JsonSerializer.Deserialize<OperatorFlowDto>(flowJson, _jsonOptions);
                if (flowDto != null)
                {
                    dto.Flow = flowDto;
                }
            }
            catch
            {
                // 忽略反序列化错误，回退到 DB 数据
            }
        }

        // 【统一修复】无论数据来自 DB 还是 JSON，都尝试回填缺失的 Options
        if (dto.Flow != null)
        {
            EnrichFlowDtoWithMetadata(dto.Flow);
        }

        return dto;
    }

    private void EnrichFlowDtoWithMetadata(OperatorFlowDto flowDto)
    {
        foreach (var opDto in flowDto.Operators)
        {
            var metadata = _operatorFactory.GetMetadata(opDto.Type);
            if (metadata == null)
                continue;

            foreach (var paramDto in opDto.Parameters)
            {
                // 如果 Options 为空且 DataType 是 enum，尝试从元数据恢复
                if ((paramDto.Options == null || paramDto.Options.Count == 0) &&
                    (paramDto.DataType.Equals("enum", StringComparison.OrdinalIgnoreCase) ||
                     paramDto.DataType.Equals("select", StringComparison.OrdinalIgnoreCase)))
                {
                    var paramDef = metadata.Parameters.FirstOrDefault(p => p.Name == paramDto.Name);
                    if (paramDef != null && paramDef.Options != null)
                    {
                        paramDto.Options = paramDef.Options;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 获取所有工程
    /// </summary>
    public async Task<IEnumerable<ProjectDto>> GetAllAsync()
    {
        var projects = await _projectRepository.GetAllAsync();
        // GetAll 通常不返回详细的 Flow 内容以优化性能，或者我们可以选择加载
        // 这里暂时保持原样，仅返回轻量级列表
        return projects.Select(MapToDto);
    }

    /// <summary>
    /// 更新工程
    /// </summary>
    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new ProjectNotFoundException(id);

        project.UpdateInfo(request.Name, request.Description);

        // 如果有流程数据，更新到文件
        if (request.Flow != null)
        {
            var json = JsonSerializer.Serialize(request.Flow, _jsonOptions);
            await _flowStorage.SaveFlowJsonAsync(id, json);
        }

        await _projectRepository.UpdateAsync(project);
        return MapToDto(project);
    }

    /// <summary>
    /// 更新工程流程
    /// </summary>
    public async Task UpdateFlowAsync(Guid id, UpdateFlowRequest request)
    {
        // 1. 验证工程存在
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new ProjectNotFoundException(id);

        // 2. 构造流程DTO
        var flowDto = new OperatorFlowDto
        {
            Name = "MainFlow", // 保持默认名称或从某处获取
            Operators = request.Operators,
            Connections = request.Connections
        };

        // 3. 序列化并保存到文件
        var json = JsonSerializer.Serialize(flowDto, _jsonOptions);
        await _flowStorage.SaveFlowJsonAsync(id, json);

        // 4. 更新工程修改时间 (可选，但推荐)
        // project.LastModified = DateTime.UtcNow; // 如果 Project 有这个字段
        // await _projectRepository.UpdateAsync(project);
    }

    /// <summary>
    /// 将OperatorFlowDto转换为Core实体
    /// </summary>
    private OperatorFlow MapDtoToFlow(OperatorFlowDto dto, Guid? flowId = null)
    {
        var flow = new OperatorFlow(dto.Name);

        // 【关键修复】如果指定了 flowId (通常是 Project.Id)，强制设置它
        // EF Core Table Splitting 要求 Project.Id == Flow.Id
        if (flowId.HasValue)
        {
            // Flow继承自Entity，Id定义在Entity中
            typeof(Acme.Product.Core.Entities.Base.Entity)
                .GetProperty("Id")?
                .SetValue(flow, flowId.Value);
        }

        // 添加算子
        foreach (var opDto in dto.Operators)
        {
            var op = new Operator(
                opDto.Name,
                opDto.Type,
                opDto.X,
                opDto.Y
            );

            // 设置ID（如果提供了）
            if (opDto.Id != Guid.Empty)
            {
                // 使用反射设置ID，因为构造函数会生成新的ID
                typeof(Operator).GetProperty("Id")?.SetValue(op, opDto.Id);
            }

            // 恢复输入端口（保留ID以维持连线）
            foreach (var portDto in opDto.InputPorts)
            {
                op.LoadInputPort(portDto.Id, portDto.Name, portDto.DataType, portDto.IsRequired);
            }

            // 恢复输出端口（保留ID以维持连线）
            foreach (var portDto in opDto.OutputPorts)
            {
                op.LoadOutputPort(portDto.Id, portDto.Name, portDto.DataType);
            }

            // 添加参数
            foreach (var paramDto in opDto.Parameters)
            {
                var param = new Parameter(
                    paramDto.Id == Guid.Empty ? Guid.NewGuid() : paramDto.Id,
                    paramDto.Name,
                    paramDto.DisplayName,
                    paramDto.Description ?? string.Empty,
                    paramDto.DataType,
                    paramDto.DefaultValue,
                    paramDto.MinValue,
                    paramDto.MaxValue,
                    paramDto.IsRequired,
                    paramDto.Options
                );

                if (paramDto.Value != null)
                {
                    param.SetValue(paramDto.Value);
                }

                op.AddParameter(param);
            }

            flow.AddOperator(op);
        }

        // 添加连接
        foreach (var connDto in dto.Connections)
        {
            // 【修复】修正参数顺序：sourceOperatorId, sourcePortId, targetOperatorId, targetPortId
            var connection = new OperatorConnection(
                connDto.SourceOperatorId,
                connDto.SourcePortId,        // ✅ 修正：第2个参数应该是 SourcePortId
                connDto.TargetOperatorId,    // ✅ 修正：第3个参数应该是 TargetOperatorId
                connDto.TargetPortId
            );

            // 设置连接ID
            if (connDto.Id != Guid.Empty)
            {
                typeof(OperatorConnection).GetProperty("Id")?.SetValue(connection, connDto.Id);
            }

            flow.AddConnection(connection);
        }

        return flow;
    }

    /// <summary>
    /// 删除工程
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new ProjectNotFoundException(id);

        project.MarkAsDeleted();
        await _projectRepository.UpdateAsync(project);
    }

    /// <summary>
    /// 搜索工程
    /// </summary>
    public async Task<IEnumerable<ProjectDto>> SearchAsync(string keyword)
    {
        var projects = await _projectRepository.SearchAsync(keyword);
        return projects.Select(MapToDto);
    }

    /// <summary>
    /// 获取最近打开的工程
    /// </summary>
    public async Task<IEnumerable<ProjectDto>> GetRecentlyOpenedAsync(int count = 10)
    {
        var projects = await _projectRepository.GetRecentlyOpenedAsync(count);
        return projects.Select(MapToDto);
    }

    private ProjectDto MapToDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Version = project.Version,
            CreatedAt = project.CreatedAt,
            ModifiedAt = project.ModifiedAt,
            LastOpenedAt = project.LastOpenedAt,
            GlobalSettings = project.GlobalSettings,
            // 修复：添加 Flow 字段映射
            Flow = project.Flow != null ? MapFlowToDto(project.Flow) : null
        };
    }

    /// <summary>
    /// 将 OperatorFlow 实体映射为 DTO
    /// </summary>
    private OperatorFlowDto MapFlowToDto(OperatorFlow flow)
    {
        return new OperatorFlowDto
        {
            Id = flow.Id,
            Name = flow.Name,
            Operators = flow.Operators.Select(MapOperatorToDto).ToList(),
            Connections = flow.Connections.Select(MapConnectionToDto).ToList()
        };
    }

    /// <summary>
    /// 将 Operator 实体映射为 DTO
    /// </summary>
    private OperatorDto MapOperatorToDto(Operator op)
    {
        return new OperatorDto
        {
            Id = op.Id,
            Name = op.Name,
            Type = op.Type,
            X = op.Position.X,
            Y = op.Position.Y,
            InputPorts = op.InputPorts.Select(MapPortToDto).ToList(),
            OutputPorts = op.OutputPorts.Select(MapPortToDto).ToList(),
            Parameters = op.Parameters.Select(MapParameterToDto).ToList(),
            IsEnabled = op.IsEnabled,
            ExecutionStatus = op.ExecutionStatus,
            ExecutionTimeMs = op.ExecutionTimeMs,
            ErrorMessage = op.ErrorMessage
        };
    }

    /// <summary>
    /// 将 Port 值对象映射为 DTO
    /// </summary>
    private PortDto MapPortToDto(Port port)
    {
        return new PortDto
        {
            Id = port.Id,
            Name = port.Name,
            Direction = port.Direction,
            DataType = port.DataType,
            IsRequired = port.IsRequired
        };
    }

    /// <summary>
    /// 将 Parameter 值对象映射为 DTO
    /// </summary>
    private ParameterDto MapParameterToDto(Parameter param)
    {
        return new ParameterDto
        {
            Id = param.Id,
            Name = param.Name,
            DisplayName = param.DisplayName,
            Description = param.Description,
            DataType = param.DataType,
            Value = param.GetValue(),
            DefaultValue = param.DefaultValue,
            MinValue = param.MinValue,
            MaxValue = param.MaxValue,
            IsRequired = param.IsRequired,
            Options = param.Options
        };
    }

    /// <summary>
    /// 将 OperatorConnection 值对象映射为 DTO
    /// </summary>
    private OperatorConnectionDto MapConnectionToDto(OperatorConnection conn)
    {
        return new OperatorConnectionDto
        {
            Id = conn.Id,
            SourceOperatorId = conn.SourceOperatorId,
            SourcePortId = conn.SourcePortId,
            TargetOperatorId = conn.TargetOperatorId,
            TargetPortId = conn.TargetPortId
        };
    }
}
