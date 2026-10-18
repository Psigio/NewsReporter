using NewsReporter.Dto;

namespace NewsReporter.Abstracts;

public interface IFetcher
{
    Task<IList<ulong>> FetchBestStoryIdsAsync(CancellationToken cancellationToken);
    Task<StoryResponse> FetchStoryResponseAsync(ulong storyId, CancellationToken cancellationToken);
}
