using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Hybrid;
using NewsReporter.Abstracts;

namespace NewsReporter.Utilities;

[ExcludeFromCodeCoverage(Justification = "Wrapper over non-mockable Microsoft HybridCache class to allow for easier unit testing")]
public class HybridCacheWrapper(HybridCache underlyingCache) : ICache
{
    private readonly HybridCache _underlyingCache = underlyingCache;

    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions options, CancellationToken cancellationToken)
            => _underlyingCache.GetOrCreateAsync(key, factory, options, cancellationToken: cancellationToken);
}
