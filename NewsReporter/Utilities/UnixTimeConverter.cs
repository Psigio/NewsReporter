using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewsReporter.Utilities;

public class UnixTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Check for incoming Number token
        if (reader.TokenType == JsonTokenType.Number)
        {
            var unixTimeSeconds = reader.GetInt64();
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        }
        // In case we have a number in a string
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (long.TryParse(stringValue, out var unixTimeSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
            }
            else
            {
                throw new JsonException($"String token had invalid value for a Unix time: {stringValue}.");
            }
        }

        throw new JsonException($"Expected number or string token type, but got {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => throw new NotImplementedException("Serialization of DateTimeOffset to Unix time is not implemented");
}
