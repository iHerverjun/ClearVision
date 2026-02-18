// GetInspectionStatisticsQuery.cs
// GetInspectionStatistics查询
// 作者：蘅芜君

using MediatR;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Interfaces;

namespace Acme.Product.Application.Queries.Inspections;

public record GetInspectionStatisticsQuery(Guid ProjectId) : IRequest<InspectionStatisticsDto>;

// Assuming InspectionStatisticsDto exists in DTOs?
// If not, I need to create it or use dynamic/dictionary.
// Let's assume it doesn't exist and define it here or rely on existing (if I can check).
// Checked DTOs earlier: `AnalysisDto.cs`, `InspectionResultDto.cs`...
// Maybe `AnalysisDto` contains statistics?
// Or I create `InspectionStatisticsDto.cs`.

// Let's define it inside the same file for now if simple, or use AnalysisDto logic.
// I'll create a dedicated DTO file if needed, but for MVP:

public class InspectionStatisticsDto
{
    public int TotalCount { get; set; }
    public int OKCount { get; set; }
    public int NGCount { get; set; }
    public double YieldRate { get; set; }
    public double AverageTimeMs { get; set; }
}

public class GetInspectionStatisticsQueryHandler : IRequestHandler<GetInspectionStatisticsQuery, InspectionStatisticsDto>
{
    private readonly IInspectionResultRepository _repository;

    public GetInspectionStatisticsQueryHandler(IInspectionResultRepository repository)
    {
        _repository = repository;
    }

    public async Task<InspectionStatisticsDto> Handle(GetInspectionStatisticsQuery request, CancellationToken cancellationToken)
    {
        var stats = await _repository.GetStatisticsAsync(request.ProjectId);
        
        return new InspectionStatisticsDto
        {
            TotalCount = stats.TotalCount,
            OKCount = stats.OKCount,
            NGCount = stats.NGCount,
            YieldRate = stats.OKRate,
            AverageTimeMs = stats.AverageProcessingTimeMs
        };
    }
}