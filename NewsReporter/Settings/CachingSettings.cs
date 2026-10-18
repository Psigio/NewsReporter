namespace NewsReporter.Settings;

public record CachingSettings
{
    public const string SectionName = "Caching";
    public required int BestStoriesCacheExpirationSeconds { get; init; }
    public required int StorySummaryCacheExpirationSeconds { get; init; }
}
