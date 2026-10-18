using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using NewsReporter.Abstracts;
using NewsReporter.Dto;
using NewsReporter.Settings;

namespace NewsReporter;

public class StoryAggregator(IFetcher fetcher, ICache cache, IOptionsMonitor<CachingSettings> cachingOptions, ILogger<StoryAggregator> logger) : IAggregator
{
    private readonly IFetcher _fetcher = fetcher;
    private readonly ICache _cache = cache;
    private readonly IOptionsMonitor<CachingSettings> _cachingOptions = cachingOptions;
    private readonly ILogger<StoryAggregator> _logger = logger;

    public async Task<IList<StorySummary>> GetBestStoriesAsync(int bestStoryCount, CancellationToken cancellationToken)
    {
        // Validate the input parameter
        if (bestStoryCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bestStoryCount),
                "The number of best stories to retrieve must be greater than zero.");
        }

        // Get the list of best story ids (assuming that they are sorted in "best" order).
        var bestStoryIds = await GetBestStoryIdsAsync(cancellationToken);
        _logger.LogInformation("Retrieved {Count} best story IDs.", bestStoryIds.Count);

        // Take the top N ids
        var filteredStoryIds = bestStoryIds.Take(bestStoryCount);

        // Get the story summaries for the filtered ids (note we convert to a standard Task in order to use Task.WhenAll so we can get the data in parallel).
        var storyFetchTasks = filteredStoryIds.Select(storyId => GetStorySummaryAsync(storyId, cancellationToken).AsTask());
        // Wait for the tasks to complete and sort by descending score as the tasks can finish in any order so they won't necssarily be the same order as we requested them.
        var storySummaries = (await Task.WhenAll(storyFetchTasks)).OrderByDescending(story => story.Score).ToImmutableArray();

        // Done
        _logger.LogInformation("Retrieved {Count} best stories.", storySummaries.Length);
        return storySummaries;
    }

    // Use cache to reduce load on IFetcher/API.  Note default HybridCache implementation manages concurrent requests some only a single invocation is made to the value factory.
    private ValueTask<IList<ulong>> GetBestStoryIdsAsync(CancellationToken cancellationToken)
        => _cache.GetOrCreateAsync(
            "bestStoryIds",
            async ct => await _fetcher.FetchBestStoryIdsAsync(ct),
            new HybridCacheEntryOptions
            {
                // Note we use a small cache expiration here so we can pick up changes in the best story list reltively quickly.
                Expiration = TimeSpan.FromSeconds(_cachingOptions.CurrentValue.BestStoriesCacheExpirationSeconds)
            },
            cancellationToken);

    private ValueTask<StorySummary> GetStorySummaryAsync(ulong storyId, CancellationToken cancellationToken)
       => _cache.GetOrCreateAsync(
            $"storySummary_{storyId}",
            async ct =>
            {
                // Note we transform the StoryResponse objects to StorySummary objects here so we cache this mapping effort (rather than repeating it for each cache-hit)
                var storyResponse = await _fetcher.FetchStoryResponseAsync(storyId, ct);
                return MapToStorySummary(storyResponse);
            },
            new HybridCacheEntryOptions
            {
                // Note we use a longer cache expiration here since story summaries are unlikely to change greatly they are published.
                Expiration = TimeSpan.FromSeconds(_cachingOptions.CurrentValue.StorySummaryCacheExpirationSeconds)
            },
            cancellationToken);

    private static StorySummary MapToStorySummary(StoryResponse storyResponse)
        => new()
        {
            Title = storyResponse.Title,
            Uri = storyResponse.Url,
            PostedBy = storyResponse.By,
            Time = storyResponse.Time,
            Score = storyResponse.Score,
            CommentCount = storyResponse.Descendants
        };
}
