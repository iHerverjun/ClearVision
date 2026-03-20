using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acme.Product.Infrastructure.AI;

/// <summary>
/// Accepts scalar JSON values for string dictionaries and coerces them into invariant strings.
/// This makes AI JSON parsing resilient when the model emits numbers/bools instead of quoted strings.
/// </summary>
public sealed class FlexibleStringDictionaryJsonConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected JSON object for string dictionary.");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name in string dictionary.");

            var key = reader.GetString();
            if (string.IsNullOrWhiteSpace(key))
                throw new JsonException("Dictionary key must not be empty.");

            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON while reading dictionary value.");

            result[key] = ReadValueAsString(ref reader);
        }

        throw new JsonException("Unexpected end of JSON while reading string dictionary.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, itemValue) in value)
        {
            writer.WriteString(key, itemValue);
        }

        writer.WriteEndObject();
    }

    private static string ReadValueAsString(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => reader.TryGetInt64(out var longValue)
                ? longValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => string.Empty,
            JsonTokenType.StartArray or JsonTokenType.StartObject => JsonDocument.ParseValue(ref reader).RootElement.GetRawText(),
            _ => throw new JsonException($"Unsupported token {reader.TokenType} for string dictionary value.")
        };
    }
}
