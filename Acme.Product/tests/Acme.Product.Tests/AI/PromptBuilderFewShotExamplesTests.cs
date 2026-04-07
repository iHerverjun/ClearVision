using System.Text.Json;
using Acme.Product.Core.DTOs;
using Acme.Product.Infrastructure.AI;
using Acme.Product.Infrastructure.Services;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class PromptBuilderFewShotExamplesTests
{
    [Fact(DisplayName = "PromptBuilder few-shot examples should stay valid against current validator")]
    public void BuildSystemPrompt_FewShotExamples_ShouldRemainValid()
    {
        var factory = new OperatorFactory();
        var builder = new PromptBuilder(factory);
        var validator = new AiFlowValidator(factory);
        var prompt = builder.BuildSystemPrompt("示例校验");
        var exampleJsons = ExtractFewShotJsonObjects(prompt);

        exampleJsons.Should().HaveCount(5);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new FlexibleStringDictionaryJsonConverter());

        foreach (var exampleJson in exampleJsons)
        {
            var flow = JsonSerializer.Deserialize<AiGeneratedFlowJson>(exampleJson, options);
            flow.Should().NotBeNull("few-shot JSON should deserialize");

            var validation = validator.Validate(flow!);
            validation.Errors.Should().BeEmpty($"few-shot example should remain valid.\nJSON:\n{exampleJson}");
        }
    }

    [Fact(DisplayName = "PromptBuilder should expose normalized top-level section headers")]
    public void BuildSystemPrompt_ShouldContainNormalizedSections()
    {
        var factory = new OperatorFactory();
        var builder = new PromptBuilder(factory);

        var prompt = builder.BuildSystemPrompt("scratch detection");

        prompt.Should().Contain("## Section 1 - Role And Hard Rules");
        prompt.Should().Contain("## Section 7 - Operator Catalog");
        prompt.Should().Contain("## Section 10 - Output Format");
        prompt.Should().Contain("## Section 11 - Few Shot Examples");
    }

    private static List<string> ExtractFewShotJsonObjects(string prompt)
    {
        const string marker = "正确输出：";
        var results = new List<string>();
        var searchIndex = 0;

        while (true)
        {
            var markerIndex = prompt.IndexOf(marker, searchIndex, StringComparison.Ordinal);
            if (markerIndex < 0)
                break;

            var objectStart = prompt.IndexOf('{', markerIndex);
            objectStart.Should().BeGreaterOrEqualTo(0, "each few-shot example should contain a JSON object");

            results.Add(ExtractJsonObject(prompt, objectStart));
            searchIndex = objectStart + 1;
        }

        return results;
    }

    private static string ExtractJsonObject(string text, int startIndex)
    {
        var depth = 0;
        var inString = false;
        var escaping = false;

        for (var i = startIndex; i < text.Length; i++)
        {
            var ch = text[i];

            if (inString)
            {
                if (escaping)
                {
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '{')
            {
                depth++;
                continue;
            }

            if (ch != '}')
                continue;

            depth--;
            if (depth == 0)
                return text[startIndex..(i + 1)];
        }

        throw new InvalidOperationException("Failed to extract JSON object from few-shot examples.");
    }
}
