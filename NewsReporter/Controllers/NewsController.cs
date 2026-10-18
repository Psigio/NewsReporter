using Microsoft.AspNetCore.Mvc;
using NewsReporter.Abstracts;
using NewsReporter.Dto;

namespace NewsReporter.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[ProducesResponseType(typeof(string), StatusCodes.Status503ServiceUnavailable, "text/plain")]
[ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError, "text/plain")]
public class NewsController(IAggregator aggregator, ILogger<NewsController> logger) : ControllerBase
{
    private readonly IAggregator _aggregator = aggregator;
    private readonly ILogger<NewsController> _logger = logger;

    [HttpGet("beststories/{bestStoryCount:int}")]
    [ProducesResponseType(typeof(ICollection<StorySummary>), StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> GetBestStories([FromRoute] int bestStoryCount, CancellationToken cancellationToken)
    {
        try
        {
            var bestStories = await _aggregator.GetBestStoriesAsync(bestStoryCount, cancellationToken);
            return Ok(bestStories);
        }
        catch (OperationCanceledException)
        {
            // In case the caller cancels, return a 503, even though they are probably not listening any longer.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Request was cancelled.");
        }
        catch (Exception ex)
        {
            // Don't return the exception to the caller - log full detail and return a generic error message.
            _logger.LogError(ex, "An error occurred while processing the request to {Path}", Request?.Path);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the request");
        }
    }
}
