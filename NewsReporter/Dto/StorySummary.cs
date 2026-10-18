using System.Text.Json.Serialization;

namespace NewsReporter.Dto;

public record StorySummary
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("postedBy")]
    public required string PostedBy { get; init; }

    [JsonPropertyName("time")]
    public required DateTimeOffset Time { get; init; }

    [JsonPropertyName("score")]
    public required int Score { get; init; }

    [JsonPropertyName("commentCount")]
    public required int CommentCount { get; init; }
}
