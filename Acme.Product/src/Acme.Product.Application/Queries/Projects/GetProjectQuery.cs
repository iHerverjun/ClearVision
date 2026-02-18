// GetProjectQuery.cs
// GetProject查询
// 作者：蘅芜君

using MediatR;
using Acme.Product.Application.DTOs;
using Acme.Product.Core.Interfaces;
using AutoMapper;

namespace Acme.Product.Application.Queries.Projects;

public record GetProjectQuery(Guid Id) : IRequest<ProjectDto?>;

public class GetProjectQueryHandler : IRequestHandler<GetProjectQuery, ProjectDto?>
{
    private readonly IProjectRepository _repository;
    private readonly IMapper _mapper;

    public GetProjectQueryHandler(IProjectRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ProjectDto?> Handle(GetProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await _repository.GetByIdAsync(request.Id);
        return project == null ? null : _mapper.Map<ProjectDto>(project);
    }
}