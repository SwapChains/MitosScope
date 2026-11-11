using System.Text.Json;
using System.Text.Json.Serialization;

namespace MitosScope.Core.Api;

public static class MitosJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new FlexibleStringConverter(), new FlexibleBoolConverter() },
    };
}

/// <summary>Reads strings, numbers and booleans into a string (the API docs don't always state the type).</summary>
internal sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            _ => SkipAndReturnRaw(ref reader),
        };

    private static string SkipAndReturnRaw(ref Utf8JsonReader reader)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return doc.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

/// <summary>Accepts true/false, 0/1 and "true"/"false"/"0"/"1".</summary>
internal sealed class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetDouble() != 0,
            JsonTokenType.String => reader.GetString() is { } s && (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase)),
            JsonTokenType.Null => false,
            _ => throw new JsonException($"Cannot read {reader.TokenType} as bool."),
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
}
