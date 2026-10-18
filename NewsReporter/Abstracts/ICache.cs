using Microsoft.Extensions.Caching.Hybrid;

namespace NewsReporter.Abstracts;

public interface ICache
{
    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions options, CancellationToken cancellationToken);
}
