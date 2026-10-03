using Microsoft.Extensions.Caching.Hybrid;

namespace UnitTestingCookbook.Support.Services;

// The same read-through cache, written against HybridCache (.NET 9+): GetOrCreateAsync does the "check, else load and
// store" dance itself, keeps an in-memory copy in front of any distributed cache, serializes values for you, and makes
// concurrent callers for the same key share a single load instead of all hitting the source at once.
public class HybridProductPriceService
{
    private readonly HybridCache cache;
    private readonly IProductPriceSource source;

    public HybridProductPriceService(HybridCache cache, IProductPriceSource source)
    {
        this.cache = cache;
        this.source = source;
    }

    public async Task<decimal> GetPriceAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(
            $"price:{sku}",
            async token => await source.GetPriceAsync(sku, token),
            new HybridCacheEntryOptions { Expiration = CachedProductPriceService.CacheDuration },
            cancellationToken: cancellationToken);
    }
}
