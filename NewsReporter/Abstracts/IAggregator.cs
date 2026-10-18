using NewsReporter.Dto;

namespace NewsReporter.Abstracts;

public interface IAggregator
{
    Task<IList<StorySummary>> GetBestStoriesAsync(int bestStoryCount, CancellationToken cancellationToken);
}
