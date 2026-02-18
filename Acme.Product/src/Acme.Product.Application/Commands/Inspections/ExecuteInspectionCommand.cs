// ExecuteInspectionCommand.cs
// ExecuteInspection命令
// 作者：蘅芜君

using MediatR;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.Services;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using AutoMapper;

namespace Acme.Product.Application.Commands.Inspections;

public record ExecuteInspectionCommand(Guid ProjectId, Dictionary<string, object>? Parameters = null) : IRequest<InspectionResultDto>;

public class ExecuteInspectionCommandHandler : IRequestHandler<ExecuteInspectionCommand, InspectionResultDto>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IFlowExecutionService _flowExecutionService;
    private readonly IInspectionResultRepository _resultRepository;
    private readonly IMapper _mapper;

    public ExecuteInspectionCommandHandler(
        IProjectRepository projectRepository,
        IFlowExecutionService flowExecutionService,
        IInspectionResultRepository resultRepository,
        IMapper mapper)
    {
        _projectRepository = projectRepository;
        _flowExecutionService = flowExecutionService;
        _resultRepository = resultRepository;
        _mapper = mapper;
    }

    public async Task<InspectionResultDto> Handle(ExecuteInspectionCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.ProjectId);
        if (project == null)
            throw new KeyNotFoundException($"Project {request.ProjectId} not found");

        var flowResult = await _flowExecutionService.ExecuteFlowAsync(project.Flow, request.Parameters);

        var inspectionResult = new InspectionResult(project.Id, null);
        
        if (flowResult.IsSuccess)
        {
            inspectionResult.SetResult(
                InspectionStatus.OK, 
                flowResult.ExecutionTimeMs, 
                1.0); // Confidence placeholder
        }
        else
        {
            inspectionResult.MarkAsError(flowResult.ErrorMessage ?? "Unknown Error");
        }

        // 将流程输出映射为缺陷（如果存在）
        // 缺陷数据从流程执行结果的OutputData中提取
        // 实际实现需要根据具体的缺陷数据结构进行映射

        // 保存输出图像（如果存在）
        if (flowResult.OutputData?.TryGetValue("Image", out var outputImage) == true
            && outputImage is byte[] imageBytes)
        {
            inspectionResult.SetOutputImage(imageBytes);
        }

        // 保存缺陷数量（从 DefectCount 获取）
        if (flowResult.OutputData?.TryGetValue("DefectCount", out var defectCount) == true
            && defectCount is int count && count > 0)
        {
            // 更新状态为 NG
            inspectionResult.SetResult(
                Core.Enums.InspectionStatus.NG,
                flowResult.ExecutionTimeMs,
                1.0);
        }

        await _resultRepository.AddAsync(inspectionResult);
        
        return _mapper.Map<InspectionResultDto>(inspectionResult);
    }
}