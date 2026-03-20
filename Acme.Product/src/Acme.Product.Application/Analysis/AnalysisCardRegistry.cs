using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Application.Analysis;

public interface IAnalysisCardMapper
{
    bool CanMap(OperatorType operatorType);

    IEnumerable<AnalysisCardDto> Map(Operator @operator, OperatorExecutionResult result);
}

public class AnalysisCardRegistry
{
    private readonly IReadOnlyList<IAnalysisCardMapper> _mappers;

    public AnalysisCardRegistry(IEnumerable<IAnalysisCardMapper> mappers)
    {
        _mappers = mappers.ToList();
    }

    public IReadOnlyList<AnalysisCardDto> Map(Operator @operator, OperatorExecutionResult result)
    {
        var cards = new List<AnalysisCardDto>();

        foreach (var mapper in _mappers)
        {
            if (!mapper.CanMap(@operator.Type))
            {
                continue;
            }

            cards.AddRange(mapper.Map(@operator, result));
        }

        return cards;
    }
}
