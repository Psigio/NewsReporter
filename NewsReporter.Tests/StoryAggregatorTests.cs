using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsReporter.Abstracts;
using NewsReporter.Dto;
using NewsReporter.Settings;

namespace NewsReporter.Tests;

public class StoryAggregatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly IFetcher _mockFetcher;
    private readonly ICache _mockCache;
    private readonly IOptionsMonitor<CachingSettings> _mockOptions;
    private readonly ILogger<StoryAggregator> _mockLogger;

    private readonly CachingSettings _fakeCachingSettings = new() { BestStoriesCacheExpirationSeconds = 10, StorySummaryCacheExpirationSeconds = 20 };

    private readonly ulong[] _fakeBestStoryIds = [50, 150, 75];

    private readonly StoryResponse[] _fakeStoryResponses = [
        new StoryResponse{ By ="A", Descendants = 10, Score = 52, Time = Now, Title = "A-A", Url = "http://www.example/com/a"},
        new StoryResponse{ By ="B", Descendants = 7, Score = 37, Time = Now, Title = "B-A", Url = "http://www.example/com/b"}
    ];

    private readonly StorySummary[] _fakeStorySummaries = [
        new StorySummary{PostedBy ="A",CommentCount = 10, Score = 52, Time = Now, Title = "A-A", Uri = "http://www.example/com/a"},
        new StorySummary{ PostedBy ="B", CommentCount = 7, Score = 37, Time = Now, Title = "B-A", Uri = "http://www.example/com/b"}
    ];

    private readonly Captured<Func<CancellationToken, ValueTask<IList<ulong>>>> _capturedMockFetchBestStoryIdsCalls;
    private readonly Captured<Func<CancellationToken, ValueTask<StorySummary>>> _capturedMockFetchStorySummary;

    public StoryAggregatorTests()
    {
        _mockFetcher = A.Fake<IFetcher>();
        _mockCache = A.Fake<ICache>();
        _mockOptions = A.Fake<IOptionsMonitor<CachingSettings>>();
        _mockLogger = A.Fake<ILogger<StoryAggregator>>();
        _capturedMockFetchBestStoryIdsCalls = A.Captured<Func<CancellationToken, ValueTask<IList<ulong>>>>();
        _capturedMockFetchStorySummary = A.Captured<Func<CancellationToken, ValueTask<StorySummary>>>();

        A.CallTo(() => _mockOptions.CurrentValue).Returns(_fakeCachingSettings);

        // Capture the factory functions passed to the cache without invoking them directly - note we need to include the Returns(...) statement otherwise the setup is a no-op
        A.CallTo(() => _mockCache.GetOrCreateAsync(
            A<string>.Ignored, _capturedMockFetchBestStoryIdsCalls.Ignored,
            A<HybridCacheEntryOptions>.Ignored, A<CancellationToken>.Ignored))
            .Returns(_fakeBestStoryIds);
        A.CallTo(() => _mockCache.GetOrCreateAsync(A<string>.Ignored,
            _capturedMockFetchStorySummary.Ignored, A<HybridCacheEntryOptions>.Ignored,
            A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(_fakeStorySummaries);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetBestStoriesAsync_Throws_On_Invalid_Count(int invalidValue)
    {
        var candidate = CreateCandidate();

        // Act/Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => candidate.GetBestStoriesAsync(invalidValue, CancellationToken.None));
    }

    [Fact]
    public async Task GetBestStoriesAsync_Calls_Cache_Correctly_For_BestStoryIds()
    {
        var candidate = CreateCandidate();

        using var cts = new CancellationTokenSource();

        // Act
        await candidate.GetBestStoriesAsync(2, cts.Token);

        // Assert call count and content
        A.CallTo(() => _mockCache.GetOrCreateAsync(A<string>.Ignored, A<Func<CancellationToken, ValueTask<IList<ulong>>>>.Ignored,
            A<HybridCacheEntryOptions>.Ignored, A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();

        // Note we don't verify the factory func here (done in the next test), nor the cache key itself
        A.CallTo(() => _mockCache.GetOrCreateAsync(A<string>.Ignored, A<Func<CancellationToken, ValueTask<IList<ulong>>>>.Ignored,
            A<HybridCacheEntryOptions>.That.Matches(x =>
            x.Expiration == TimeSpan.FromSeconds(_fakeCachingSettings.BestStoriesCacheExpirationSeconds)),
             cts.Token))
            .MustHaveHappened();
    }

    [Fact]
    public async Task GetBestStoriesAsync_Calls_Fetcher_Correctly_For_BestStoryIds()
    {
        var candidate = CreateCandidate();

        using var cts = new CancellationTokenSource();

        // Act
        await candidate.GetBestStoriesAsync(2, cts.Token);

        // Extract the func passed in the call to the cache to obtain the best story ids
        Assert.Single(_capturedMockFetchBestStoryIdsCalls.Values);

        // Assert no early call made to fetcher
        A.CallTo(() => _mockFetcher.FetchBestStoryIdsAsync(A<CancellationToken>.Ignored)).MustNotHaveHappened();

        // Now invoke the callback to verify that the logic in StoryAggregator is invoking the correct target on IFetcher
        await _capturedMockFetchBestStoryIdsCalls.Values.Single().Invoke(cts.Token);

        // Assert call count and content now
        A.CallTo(() => _mockFetcher.FetchBestStoryIdsAsync(A<CancellationToken>.Ignored))
           .MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockFetcher.FetchBestStoryIdsAsync(cts.Token))
           .MustHaveHappened();
    }

    [Fact]
    public async Task GetBestStoriesAsync_Calls_Cache_Correctly_For_Individual_Stories()
    {
        // Update the mock cache to return the dummy best story ids
        A.CallTo(() => _mockCache.GetOrCreateAsync(A<string>.Ignored, A<Func<CancellationToken, ValueTask<IList<ulong>>>>.Ignored,
            A<HybridCacheEntryOptions>.Ignored, A<CancellationToken>.Ignored)).Returns(_fakeBestStoryIds);

        var candidate = CreateCandidate();

        using var cts = new CancellationTokenSource();

        // Act
        var desiredCount = 2;
        await candidate.GetBestStoriesAsync(desiredCount, cts.Token);

        // Assert call count and content - note we expect 2 calls to the cache, not 3 as we are only requesting 2 stories
        A.CallTo(() => _mockCache.GetOrCreateAsync(A<string>.Ignored, A<Func<CancellationToken, ValueTask<StorySummary>>>.Ignored,
            A<HybridCacheEntryOptions>.Ignored, A<CancellationToken>.Ignored))
            .MustHaveHappenedANumberOfTimesMatching(i => i == desiredCount);

        // Note we don't verify the factory funcs here (done in the next test)
        foreach (var expectedStoryId in _fakeBestStoryIds.Take(desiredCount))
        {
            // Note we don't care about the exact cache key (as this is opaque to us), only that it contains our expected story id to verify it is unique per story
            A.CallTo(() => _mockCache.GetOrCreateAsync(A<string>.That.Contains(expectedStoryId.ToString()),
                A<Func<CancellationToken, ValueTask<StorySummary>>>.Ignored,
                A<HybridCacheEntryOptions>.That.Matches(x =>
                x.Expiration == TimeSpan.FromSeconds(_fakeCachingSettings.StorySummaryCacheExpirationSeconds)),
                 cts.Token))
                .MustHaveHappened();
        }
    }

    [Fact]
    public async Task GetBestStoriesAsync_Calls_Fetcher_Correctly_For_Individual_Stories()
    {
        var candidate = CreateCandidate();

        using var cts = new CancellationTokenSource();

        // Act
        var desiredCount = 2;
        await candidate.GetBestStoriesAsync(desiredCount, cts.Token);

        // Extract the func passed in the call to the cache to obtain the story responses - we expect two
        Assert.Equal(desiredCount, _capturedMockFetchStorySummary.Values.Count);

        // Assert no early calls made to fetcher
        A.CallTo(() => _mockFetcher.FetchStoryResponseAsync(A<ulong>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();

        // Now invoke the callbacks to verify that the logic in StoryAggregator is invoking the correct target on IFetcher
        foreach (var callback in _capturedMockFetchStorySummary.Values)
        {
            await callback.Invoke(cts.Token);
        }

        // Assert call count (should match `desiredCount` ie: 2) and content now
        A.CallTo(() => _mockFetcher.FetchStoryResponseAsync(A<ulong>.Ignored, A<CancellationToken>.Ignored))
           .MustHaveHappenedANumberOfTimesMatching(i => i == desiredCount);
        foreach (var storyId in _fakeBestStoryIds.Take(desiredCount))
        {

            A.CallTo(() => _mockFetcher.FetchStoryResponseAsync(storyId, cts.Token))
               .MustHaveHappened();
        }
    }

    [Fact]
    public async Task GetBestStoriesAsync_Caches_Mapped_StoryResponse_As_StorySummary()
    {
        // Update the mock fetcher to return the first StoryResponse
        A.CallTo(() => _mockFetcher.FetchStoryResponseAsync(_fakeBestStoryIds[0], A<CancellationToken>.Ignored))
            .Returns(_fakeStoryResponses[0]);

        var candidate = CreateCandidate();

        // Act
        await candidate.GetBestStoriesAsync(1, CancellationToken.None);

        // Extract the func passed in the call to the cache to obtain the story response
        Assert.Single(_capturedMockFetchStorySummary.Values);

        var fetcherFactoryFunction = _capturedMockFetchStorySummary.Values.Single();

        // Invoke the fetcher and confirm that the values are mapped to StorySummary types (so we can verify that the mapping effort is cached, rather than being called for each incoming HTTP request)
        var valueToBeCached = await fetcherFactoryFunction(CancellationToken.None);

        // Assert it matches the expected mapped values which we have already manually specified in the test setup
        Assert.NotNull(valueToBeCached);
        // Because we use records we can use simple Equality to check
        Assert.Equal(_fakeStorySummaries[0], valueToBeCached);
    }

    private StoryAggregator CreateCandidate() => new(_mockFetcher, _mockCache, _mockOptions, _mockLogger);
}
