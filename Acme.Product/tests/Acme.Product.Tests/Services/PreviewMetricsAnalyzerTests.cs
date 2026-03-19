using Acme.Product.Core.Services;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;

namespace Acme.Product.Tests.Services;

public class PreviewMetricsAnalyzerTests
{
    [Fact]
    public void Analyze_BrightNoisyOutput_ProducesDiagnosticsAndSuggestions()
    {
        using var image = new Mat(20, 20, MatType.CV_8UC1, Scalar.All(240));
        var analyzer = new PreviewMetricsAnalyzer(NullLogger<PreviewMetricsAnalyzer>.Instance);
        var outputData = new Dictionary<string, object>
        {
            ["Defects"] = Enumerable.Range(0, 6)
                .Select(index => new Dictionary<string, object>
                {
                    ["X"] = index,
                    ["Y"] = index,
                    ["Width"] = 2,
                    ["Height"] = 3
                })
                .ToList()
        };

        var metrics = analyzer.Analyze(image, outputData, new AutoTuneGoal
        {
            TargetBlobCount = 2,
            MinArea = 50
        });

        metrics.BlobStats.Should().HaveCount(6);
        metrics.Diagnostics.Should().Contain("SpecularHighlightsDominant");
        metrics.Diagnostics.Should().Contain("MaskTooNoisy");
        metrics.Diagnostics.Should().Contain("LowContrast");
        metrics.Diagnostics.Should().Contain("BlurryImage");
        metrics.Goals.CurrentBlobCount.Should().Be(6);
        metrics.Goals.NoisePenalty.Should().Be(6);
        metrics.OverallScore.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);

        var suggestedParameters = metrics.Suggestions.Select(suggestion => suggestion.ParameterName).ToList();
        suggestedParameters.Should().Contain("Threshold");
        suggestedParameters.Should().Contain("MinArea");
        suggestedParameters.Should().Contain("MorphologyOperation");
    }
}
