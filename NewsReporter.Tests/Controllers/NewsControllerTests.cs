using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NewsReporter.Abstracts;
using NewsReporter.Controllers;
using NewsReporter.Dto;

namespace NewsReporter.Tests.Controllers;

public class NewsControllerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly IAggregator _mockAggregator;
    private readonly ILogger<NewsController> _mockLogger;

    private readonly IList<StorySummary> _fakeBestStories =
    [
        new() { Title = "Story 1", Uri = "https://example.com/story1", CommentCount = 10, Score = 100, PostedBy="user1", Time = Now.Subtract(TimeSpan.FromDays(1)) },
        new() { Title = "Story 2", Uri = "https://example.com/story2", CommentCount = 5, Score = 50, PostedBy="user2", Time = Now.Subtract(TimeSpan.FromHours(4)) }
    ];

    public NewsControllerTests()
    {
        _mockAggregator = A.Fake<IAggregator>();
        _mockLogger = A.Fake<ILogger<NewsController>>();

        // Setup mock return value
        A.CallTo(() => _mockAggregator.GetBestStoriesAsync(A<int>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.FromResult(_fakeBestStories));
    }

    [Fact]
    public async Task GetBestStories_Aggregator_Called_As_ExpectedAsync()
    {
        var candidate = CreateCandidate();

        // Assert no early calls
        A.CallTo(() => _mockAggregator.GetBestStoriesAsync(A<int>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();

        // Act
        using var cts = new CancellationTokenSource();
        await candidate.GetBestStories(2, cts.Token);

        // Assert correct call count & content
        A.CallTo(() => _mockAggregator.GetBestStoriesAsync(A<int>.Ignored, A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _mockAggregator.GetBestStoriesAsync(2, cts.Token))
            .MustHaveHappened();
    }

    [Fact]
    public async Task GetBestStories_Returns_Expected_Value()
    {
        var candidate = CreateCandidate();

        // Act
        var result = await candidate.GetBestStories(2, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var objectResult = (OkObjectResult)result;
        Assert.Equal(_fakeBestStories, objectResult.Value);
    }

    [Fact]
    public async Task GetBestStories_Cancellation_Returns_Expected_Value()
    {
        // Update mock to return a cancelled task to simulate cancellation
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        A.CallTo(() => _mockAggregator.GetBestStoriesAsync(A<int>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.FromCanceled<IList<StorySummary>>(cts.Token));

        var candidate = CreateCandidate();

        // Act
        var result = await candidate.GetBestStories(2, cts.Token);

        // Assert
        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetBestStories_Exception_Returns_Expected_Value()
    {
        // Update mock to throw
        var testException = new Exception("Failed - with some internal details in the message");
        A.CallTo(() => _mockAggregator.GetBestStoriesAsync(A<int>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.FromException<IList<StorySummary>>(testException));

        var candidate = CreateCandidate();

        // Act
        var result = await candidate.GetBestStories(2, CancellationToken.None);

        // Assert
        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        // Assert that the message does not contain the detail of the exception
        Assert.True(objectResult.Value?.ToString()!.IndexOf(testException.Message) == -1);
    }

    private NewsController CreateCandidate() => new NewsController(_mockAggregator, _mockLogger);
}
