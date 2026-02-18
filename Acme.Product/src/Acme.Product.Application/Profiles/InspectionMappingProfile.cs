// InspectionMappingProfile.cs
// AutoMapper 映射配置
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using AutoMapper;

namespace Acme.Product.Application.Profiles;

/// <summary>
/// AutoMapper 映射配置
/// </summary>
public class InspectionMappingProfile : Profile
{
    public InspectionMappingProfile()
    {
        // InspectionResult -> InspectionResultDto
        CreateMap<InspectionResult, InspectionResultDto>()
            .ForMember(dest => dest.OutputImage, opt => opt.MapFrom(src => 
                src.OutputImage != null ? Convert.ToBase64String(src.OutputImage) : null));

        // Defect -> DefectDto
        CreateMap<Defect, DefectDto>();
    }
}
