using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using Acme.Product.Core.Events;
using Acme.Product.Desktop.Inspection;
using FluentAssertions;

namespace Acme.Product.Desktop.Tests;

public class InspectionRealtimeEventMapperTests
{
    [Fact]
    public void Map_ResultProduced_Should_Include_AnalysisData_In_Payload()
    {
        var inspectionResultEvent = CreateInspectionResultEventWithAnalysisData();

        var messages = InspectionRealtimeEventMapper.Map(inspectionResultEvent);

        var resultMessage = messages.Single(message => message.EventType == "resultProduced");
        var payloadJson = JsonDocument.Parse(JsonSerializer.Serialize(resultMessage.Payload));
        var analysisData = GetProperty(payloadJson.RootElement, "analysisData");

        GetProperty(payloadJson.RootElement, "status").GetString().Should().Be("OK");
        GetProperty(analysisData, "version").GetInt32().Should().Be(1);
        GetProperty(analysisData, "cards").GetArrayLength().Should().Be(1);

        var firstCard = GetProperty(analysisData, "cards").EnumerateArray().Single();
        GetProperty(firstCard, "category").GetString().Should().Be("recognition");
        GetProperty(firstCard, "sourceOperatorType").GetString().Should().Be("OcrRecognition");
    }

    private static InspectionResultEvent CreateInspectionResultEventWithAnalysisData()
    {
        var evt = (InspectionResultEvent)FormatterServices.GetUninitializedObject(typeof(InspectionResultEvent));
        SetProperty(evt, nameof(InspectionResultEvent.ProjectId), Guid.NewGuid());
        SetProperty(evt, nameof(InspectionResultEvent.SessionId), Guid.NewGuid());
        SetProperty(evt, nameof(InspectionResultEvent.ResultId), Guid.NewGuid());
        SetProperty(evt, nameof(InspectionResultEvent.Status), "OK");
        SetProperty(evt, nameof(InspectionResultEvent.DefectCount), 0);
        SetProperty(evt, nameof(InspectionResultEvent.ProcessingTimeMs), 18L);
        SetProperty(evt, nameof(InspectionResultEvent.OutputData), new Dictionary<string, object> { ["Text"] = "ABC123" });

        var analysisDataProperty = typeof(InspectionResultEvent).GetProperty(
            "AnalysisData",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        analysisDataProperty.Should().NotBeNull("InspectionResultEvent 应该携带 analysisData，供桌面实时消息透传");

        var analysisDataJson = """
        {
          "version": 1,
          "cards": [
            {
              "id": "ocr-1",
              "category": "recognition",
              "sourceOperatorId": "00000000-0000-0000-0000-000000000001",
              "sourceOperatorType": "OcrRecognition",
              "title": "OCR 文本识别",
              "status": "OK",
              "priority": 90,
              "fields": [
                {
                  "key": "text",
                  "label": "识别文本",
                  "value": "ABC123"
                }
              ],
              "meta": {
                "confidence": 98.2
              }
            }
          ],
          "summary": {
            "cardCount": 1,
            "categories": ["recognition"]
          }
        }
        """;

        var analysisData = JsonSerializer.Deserialize(
            analysisDataJson,
            analysisDataProperty!.PropertyType,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        analysisData.Should().NotBeNull();
        analysisDataProperty.SetValue(evt, analysisData);

        return evt;
    }

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"未找到属性 {propertyName}");
        property.SetValue(target, value);
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) || property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException($"JSON 中缺少属性 {propertyName}");
    }
}
