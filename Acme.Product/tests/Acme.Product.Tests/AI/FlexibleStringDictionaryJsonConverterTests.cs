using System.Text.Json;
using Acme.Product.Core.DTOs;
using Acme.Product.Infrastructure.AI;
using FluentAssertions;

namespace Acme.Product.Tests.AI;

public class FlexibleStringDictionaryJsonConverterTests
{
    [Fact(DisplayName = "FlexibleStringDictionaryJsonConverter should coerce scalar parameter values to strings")]
    public void Deserialize_AiGeneratedFlowJson_WithNumericAndBooleanParameters_ShouldSucceed()
    {
        var json = """
        {
          "explanation": "test",
          "operators": [
            {
              "tempId": "op_1",
              "operatorType": "Thresholding",
              "displayName": "二值化",
              "parameters": {
                "Threshold": 127,
                "UseOtsu": true,
                "Type": "8"
              }
            }
          ],
          "connections": []
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new FlexibleStringDictionaryJsonConverter());

        var result = JsonSerializer.Deserialize<AiGeneratedFlowJson>(json, options);

        result.Should().NotBeNull();
        result!.Operators.Should().HaveCount(1);
        result.Operators[0].Parameters["Threshold"].Should().Be("127");
        result.Operators[0].Parameters["UseOtsu"].Should().Be("true");
        result.Operators[0].Parameters["Type"].Should().Be("8");
    }
}
