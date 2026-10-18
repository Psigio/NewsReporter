namespace NewsReporter.Settings;

public record TargetApiSettings
{
    public const string SectionName = "TargetApi";
    public required string BaseUrl { get; init; }
    public double TimeoutSeconds { get; init; }
    public int MaxConcurrentRequests { get; init; }
    public int MaxQueuedRequests { get; init; }
}
