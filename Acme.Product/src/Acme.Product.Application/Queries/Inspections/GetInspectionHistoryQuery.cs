// GetInspectionHistoryQuery.cs
// GetInspectionHistory查询
// 作者：蘅芜君

using MediatR;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Interfaces;
using AutoMapper;

namespace Acme.Product.Application.Queries.Inspections;

public record GetInspectionHistoryQuery(Guid ProjectId, DateTime? StartDate, DateTime? EndDate, int Limit = 100) : IRequest<List<InspectionResultDto>>;

public class GetInspectionHistoryQueryHandler : IRequestHandler<GetInspectionHistoryQuery, List<InspectionResultDto>>
{
    private readonly IInspectionResultRepository _repository;
    private readonly IMapper _mapper;

    public GetInspectionHistoryQueryHandler(IInspectionResultRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<InspectionResultDto>> Handle(GetInspectionHistoryQuery request, CancellationToken cancellationToken)
    {
        // Use GetByProjectIdAsync or GetByTimeRangeAsync based on params
        // For simplicity, using GetByProjectIdAsync with limit as pageSize
        
        IEnumerable<Core.Entities.InspectionResult> results;
        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            results = await _repository.GetByTimeRangeAsync(request.ProjectId, request.StartDate.Value, request.EndDate.Value);
            // Limit manually if needed
            if (request.Limit > 0) results = results.Take(request.Limit);
        }
        else
        {
            results = await _repository.GetByProjectIdAsync(request.ProjectId, 0, request.Limit);
        }

        return _mapper.Map<List<InspectionResultDto>>(results);
    }
}