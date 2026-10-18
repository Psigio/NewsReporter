using System.Text.Json.Serialization;
using NewsReporter.Utilities;

namespace NewsReporter.Dto;

public record StoryResponse
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("url")]
    public string Url { get; init; } = ""; // During testing, story 50121562 has no URL and causes a deserialization error without a default value (and therefore the required keyword must also be removed)

    [JsonPropertyName("by")]
    public required string By { get; init; }

    [JsonPropertyName("time")]
    [JsonConverter(typeof(UnixTimeConverter))]
    public required DateTimeOffset Time { get; init; }

    [JsonPropertyName("score")]
    public required int Score { get; init; }

    [JsonPropertyName("descendants")]
    public required int Descendants { get; init; }
}
