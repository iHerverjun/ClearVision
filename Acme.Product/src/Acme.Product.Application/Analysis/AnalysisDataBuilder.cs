using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Services;

namespace Acme.Product.Application.Analysis;

public interface IAnalysisDataBuilder
{
    AnalysisDataDto Build(OperatorFlow flow, FlowExecutionResult flowResult, InspectionStatus inspectionStatus);

    AnalysisDataDto Build(
        OperatorFlow flow,
        IEnumerable<OperatorExecutionResult> operatorResults,
        InspectionStatus inspectionStatus);
}

public class AnalysisDataBuilder : IAnalysisDataBuilder
{
    private readonly AnalysisCardRegistry _registry;

    public AnalysisDataBuilder()
        : this(new AnalysisCardRegistry(new IAnalysisCardMapper[]
        {
            new OcrRecognitionAnalysisCardMapper(),
            new CodeRecognitionAnalysisCardMapper(),
            new WidthMeasurementAnalysisCardMapper()
        }))
    {
    }

    public AnalysisDataBuilder(AnalysisCardRegistry registry)
    {
        _registry = registry;
    }

    public AnalysisDataDto Build(OperatorFlow flow, FlowExecutionResult flowResult, InspectionStatus inspectionStatus)
    {
        return Build(flow, flowResult.OperatorResults, inspectionStatus);
    }

    public AnalysisDataDto Build(
        OperatorFlow flow,
        IEnumerable<OperatorExecutionResult> operatorResults,
        InspectionStatus inspectionStatus)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(operatorResults);

        var operatorsById = flow.Operators.ToDictionary(op => op.Id);
        var cards = new List<AnalysisCardDto>();

        foreach (var result in operatorResults)
        {
            if (!operatorsById.TryGetValue(result.OperatorId, out var @operator))
            {
                continue;
            }

            cards.AddRange(_registry.Map(@operator, result));
        }

        var orderedCards = cards
            .OrderByDescending(card => card.Priority)
            .ThenBy(card => card.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AnalysisDataDto
        {
            Version = 1,
            Cards = orderedCards,
            Summary = new AnalysisSummaryDto
            {
                CardCount = orderedCards.Count,
                Categories = orderedCards
                    .Select(card => card.Category)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            }
        };
    }
}
