// DtoGenerationSpec.cs
// DtoGenerationSpec实现
// 作者：蘅芜君

using TypeGen.Core.SpecGeneration;
using Acme.Product.Application.DTOs;

namespace Acme.Product.Application;

public class DtoGenerationSpec : GenerationSpec
{
    public override void OnBeforeGeneration(OnBeforeGenerationArgs args)
    {
        var outputDir = "../../../Acme.Product.Desktop/wwwroot/src/types/generated";
        
        AddInterface<ProjectDto>(outputDir);
        AddInterface<OperatorDto>(outputDir);
        AddInterface<OperatorFlowDto>(outputDir);
        AddInterface<ImageDto>(outputDir);
        AddInterface<InspectionResultDto>(outputDir);
        
        // Add Enums
        AddEnum<Core.Enums.OperatorType>(outputDir);
        AddEnum<Core.Enums.InspectionStatus>(outputDir);
        AddEnum<Core.Enums.DefectType>(outputDir);
    }
}