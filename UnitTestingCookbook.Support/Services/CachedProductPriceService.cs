using System.Globalization;

using Microsoft.Extensions.Caching.Distributed;

namespace UnitTestingCookbook.Support.Services;

// Read-through cache over IDistributedCache: return the cached price if there is one, otherwise load it from the
// source and cache it for CacheDuration. The same code works whether IDistributedCache is backed by Redis, SQL Server,
// or (AddDistributedMemoryCache) just this process's memory.
public class CachedProductPriceService
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IDistributedCache cache;
    private readonly IProductPriceSource source;

    public CachedProductPriceService(IDistributedCache cache, IProductPriceSource source)
    {
        this.cache = cache;
        this.source = source;
    }

    public async Task<decimal> GetPriceAsync(string sku, CancellationToken cancellationToken = default)
    {
        string key = $"price:{sku}";

        string? cached = await cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            return decimal.Parse(cached, CultureInfo.InvariantCulture);
        }

        decimal price = await source.GetPriceAsync(sku, cancellationToken);

        await cache.SetStringAsync(
            key,
            price.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
            cancellationToken);

        return price;
    }
}
