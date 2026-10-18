using System.Diagnostics.CodeAnalysis;
using NewsReporter.Abstracts;
using NewsReporter.Dto;

namespace NewsReporter;

[ExcludeFromCodeCoverage(Justification = "Wrapper over DI-managed HttpClient")]
public class HackerNewsFetcher(HttpClient httpClient, ILogger<HackerNewsFetcher> logger) : IFetcher
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<HackerNewsFetcher> _logger = logger;

    public async Task<IList<ulong>> FetchBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ulong[]>("beststories.json", cancellationToken);
            return response ?? throw new InvalidOperationException("Failed to fetch best story IDs from Hacker News API.");
        }
        catch (Exception ex)
        {
            // Rethrow to avoid caching empty response
            _logger.LogError(ex, "Failed to retreive BestStoryIds");
            throw;
        }
    }

    public async Task<StoryResponse> FetchStoryResponseAsync(ulong storyId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<StoryResponse>($"item/{storyId}.json", cancellationToken);
            return response ?? throw new InvalidOperationException($"Failed to fetch story summary for story ID {storyId} from Hacker News API.");
        }
        catch (Exception ex)
        {
            // Rethrow to avoid caching empty response
            _logger.LogError(ex, "Failed to retreivee StorySummary");
            throw;
        }
    }
}
