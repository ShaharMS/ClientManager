using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClientManager.Shared.Models.Search;

/// <summary>
/// Custom JSON converter for <see cref="FilterClause.Value"/> that preserves CLR types
/// during round-tripping. Without this, <see cref="System.Text.Json"/> deserializes
/// <c>object</c>-typed properties as <see cref="JsonElement"/>, losing type fidelity
/// that the query evaluators rely on.
/// </summary>
public class FilterClauseValueConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when reader.TryGetDateTime(out var dt) => dt,
            JsonTokenType.String => reader.GetString()!,
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unsupported token type '{reader.TokenType}' for filter value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
