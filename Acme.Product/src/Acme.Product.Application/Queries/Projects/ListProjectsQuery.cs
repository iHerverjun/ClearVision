// ListProjectsQuery.cs
// ListProjects查询
// 作者：蘅芜君

using MediatR;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Interfaces;
using AutoMapper;

namespace Acme.Product.Application.Queries.Projects;

public record ListProjectsQuery(string? SearchTerm = null) : IRequest<List<ProjectDto>>;

public class ListProjectsQueryHandler : IRequestHandler<ListProjectsQuery, List<ProjectDto>>
{
    private readonly IProjectRepository _repository;
    private readonly IMapper _mapper;

    public ListProjectsQueryHandler(IProjectRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<ProjectDto>> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        // Assuming Repository has SearchAsync or we use ListAsync and filter.
        // IProjectRepository interface usually has SearchAsync.
        // Let's assume SearchAsync exists based on previous file reads (ProjectService uses it).
        
        var projects = await _repository.SearchAsync(request.SearchTerm);
        return _mapper.Map<List<ProjectDto>>(projects);
    }
}