# Caching (IDistributedCache and HybridCache)

## NuGet Packages Referenced

- Microsoft.Extensions.Caching.Memory (`MemoryDistributedCache`, `AddDistributedMemoryCache()`)
- Microsoft.Extensions.Caching.Hybrid (`HybridCache`, `AddHybridCache()`)
- Microsoft.Extensions.Caching.StackExchangeRedis (`RedisCache`) and Testcontainers.Redis https://dotnet.testcontainers.org (the Redis example only)

All examples are located in `UnitTestingCookbook.Tests` -> [`CachingTests`](../UnitTestingCookbook.Tests/CachingTests.cs)  

---

## The code under test

[`CachedProductPriceService`](../UnitTestingCookbook.Support/Services/CachedProductPriceService.cs) is a typical
read-through cache: return the cached price if there is one, otherwise load it from a slow
[`IProductPriceSource`](../UnitTestingCookbook.Support/Services/IProductPriceSource.cs) and cache it for 5 minutes.

<!-- snippet: CachedProductPriceService.cs -->
<a id='snippet-CachedProductPriceService.cs'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Support/Services/CachedProductPriceService.cs#L1-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachedProductPriceService.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

It depends only on `IDistributedCache`, so it doesn't know - or care - whether the cache behind it is Redis, SQL
Server, or just memory.

---

## How do I test code that uses IDistributedCache - with the cache that's "just there"?

You don't need to build an `IDistributedCache` yourself, or wrap `IMemoryCache` in one: **`MemoryDistributedCache`**
already is one - a real `IDistributedCache` implementation that keeps everything in process memory (it owns its own
internal `MemoryCache`; it doesn't share a registered `IMemoryCache`). `services.AddDistributedMemoryCache()` registers
it.

That's also a legitimate **production** choice when you want the `IDistributedCache` abstraction now but don't need -
or don't have yet - a shared cache: a single-instance app, local development, or "Redis later". Code written against
`IDistributedCache` won't change when Redis arrives; only the registration does.

<!-- snippet: CachingTests_A_DistributedMemoryCache_SecondCallIsCached -->
<a id='snippet-CachingTests_A_DistributedMemoryCache_SecondCallIsCached'></a>
```cs
public async Task A_DistributedMemoryCache_SecondCallIsCached()
{
    // Arrange - the same registration an app would use when it wants the IDistributedCache abstraction without Redis
    IProductPriceSource source = Substitute.For<IProductPriceSource>();
    source.GetPriceAsync("WIDGET", Arg.Any<CancellationToken>()).Returns(9.99m);

    ServiceCollection services = new ServiceCollection();
    services.AddDistributedMemoryCache();
    services.AddSingleton(source);
    services.AddTransient<CachedProductPriceService>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    CachedProductPriceService service = serviceProvider.GetRequiredService<CachedProductPriceService>();

    // Act
    decimal first = await service.GetPriceAsync("WIDGET");
    decimal second = await service.GetPriceAsync("WIDGET");

    // Assert
    using (new AssertionScope())
    {
        first.Should().Be(9.99m);
        second.Should().Be(9.99m);
        serviceProvider.GetRequiredService<IDistributedCache>().Should().BeOfType<MemoryDistributedCache>();
    }

    await source.Received(1).GetPriceAsync("WIDGET", Arg.Any<CancellationToken>()); // the 2nd call came from the cache
}
```
<sup><a href='/UnitTestingCookbook.Tests/CachingTests.cs#L33-L62' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachingTests_A_DistributedMemoryCache_SecondCallIsCached' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Why not just mock IDistributedCache?

Because the methods everyone actually calls - `GetStringAsync`, `SetStringAsync` - **aren't on the interface**. They're
extension methods over `IDistributedCache`'s `byte[]`-based `GetAsync`/`SetAsync`, and neither Moq nor NSubstitute can
set up an extension method. So a mocked cache has to be set up (and verified) in terms of UTF-8 bytes:

<!-- snippet: CachingTests_B_MockingIDistributedCache_MeansMockingBytes -->
<a id='snippet-CachingTests_B_MockingIDistributedCache_MeansMockingBytes'></a>
```cs
public async Task B_MockingIDistributedCache_MeansMockingBytes()
{
    // Arrange
    IProductPriceSource source = Substitute.For<IProductPriceSource>();
    IDistributedCache cache = Substitute.For<IDistributedCache>();

    // cache.GetStringAsync(...).Returns("9.99") doesn't work - it's an extension method. Set up what it calls instead:
    cache.GetAsync("price:WIDGET", Arg.Any<CancellationToken>()).Returns(Encoding.UTF8.GetBytes("9.99"));

    CachedProductPriceService service = new CachedProductPriceService(cache, source);

    // Act
    decimal price = await service.GetPriceAsync("WIDGET");

    // Assert
    price.Should().Be(9.99m);
    await source.DidNotReceiveWithAnyArgs().GetPriceAsync(default!, default);
}
```
<sup><a href='/UnitTestingCookbook.Tests/CachingTests.cs#L71-L90' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachingTests_B_MockingIDistributedCache_MeansMockingBytes' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

It works, but the test now depends on how `GetStringAsync` is *implemented* (that it calls `GetAsync` and decodes
UTF-8), and verifying a `SetAsync` call means matching byte arrays and `DistributedCacheEntryOptions`. A real
`MemoryDistributedCache` avoids all of that, and actually behaves like a cache - including expiration.

---

## How do I test cache expiration without waiting 5 minutes?

`MemoryDistributedCache` gets the current time from `MemoryCacheOptions.Clock`, which in .NET 10 is still an
`ISystemClock` (the time abstraction that predates `TimeProvider`) - there's no `TimeProvider` option. A one-line
adapter lets the same `FakeTimeProvider` from [TimeProvider](./README_TimeProvider.md) (the source of truth for
`FakeTimeProvider`) drive the cache's clock:

<!-- snippet: CachingTests_C_Expiration_WithFakeTime -->
<a id='snippet-CachingTests_C_Expiration_WithFakeTime'></a>
```cs
public async Task C_Expiration_WithFakeTime()
{
    // Arrange - MemoryDistributedCache takes its time from an ISystemClock, so adapt a FakeTimeProvider to one
    FakeTimeProvider time = new FakeTimeProvider();
    IDistributedCache cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions { Clock = new TimeProviderClock(time) }));

    IProductPriceSource source = Substitute.For<IProductPriceSource>();
    source.GetPriceAsync("WIDGET", Arg.Any<CancellationToken>()).Returns(9.99m, 12.50m); // the price changes later

    CachedProductPriceService service = new CachedProductPriceService(cache, source);

    // Act / Assert
    (await service.GetPriceAsync("WIDGET")).Should().Be(9.99m);

    time.Advance(CachedProductPriceService.CacheDuration - TimeSpan.FromSeconds(1));
    (await service.GetPriceAsync("WIDGET")).Should().Be(9.99m); // still cached

    time.Advance(TimeSpan.FromSeconds(2));
    (await service.GetPriceAsync("WIDGET")).Should().Be(12.50m); // expired - reloaded from the source

    await source.Received(2).GetPriceAsync("WIDGET", Arg.Any<CancellationToken>());
}

// MemoryCacheOptions.Clock is an ISystemClock (the pre-.NET 8 time abstraction), not a TimeProvider - so this
// 1-line adapter lets the same FakeTimeProvider used in README_TimeProvider.md drive the cache's clock.
private sealed class TimeProviderClock(TimeProvider timeProvider) : ISystemClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
```
<sup><a href='/UnitTestingCookbook.Tests/CachingTests.cs#L97-L127' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachingTests_C_Expiration_WithFakeTime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Is MemoryDistributedCache actually distributed?

No - and it's worth a test that says so, so nobody assumes otherwise. Two "app instances", each with its own
`MemoryDistributedCache`, share nothing: both have to load the price themselves.

<!-- snippet: CachingTests_D_MemoryDistributedCache_IsNotShared -->
<a id='snippet-CachingTests_D_MemoryDistributedCache_IsNotShared'></a>
```cs
public async Task D_MemoryDistributedCache_IsNotShared()
{
    // Arrange - two "app instances", each with its own in-memory IDistributedCache
    IProductPriceSource source = Substitute.For<IProductPriceSource>();
    source.GetPriceAsync("WIDGET", Arg.Any<CancellationToken>()).Returns(9.99m);

    CachedProductPriceService instance1 = new CachedProductPriceService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), source);
    CachedProductPriceService instance2 = new CachedProductPriceService(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), source);

    // Act
    await instance1.GetPriceAsync("WIDGET");
    await instance2.GetPriceAsync("WIDGET");

    // Assert - each instance had to load the price itself: nothing is shared between processes
    await source.Received(2).GetPriceAsync("WIDGET", Arg.Any<CancellationToken>());
}
```
<sup><a href='/UnitTestingCookbook.Tests/CachingTests.cs#L134-L151' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachingTests_D_MemoryDistributedCache_IsNotShared' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

That's the limit of testing with `MemoryDistributedCache`: tests pass, but they can't catch anything specific to a
real distributed cache - instances sharing (or failing to share) data, connection failures and timeouts, or
serialization and size limits.

---

## How do I test against a real, shared Redis cache?

Start a throwaway Redis in Docker with [Testcontainers](./README_Docker.md) (the source of truth for Testcontainers)
and point two separate `RedisCache` clients at it. Now the second "instance" gets the first one's cached price - the
opposite of `D_` above, with the same service code.

<!-- snippet: CachingTests_E_Redis_IsShared -->
<a id='snippet-CachingTests_E_Redis_IsShared'></a>
```cs
public async Task E_Redis_IsShared()
{
    // Arrange - a throwaway Redis container, started for this test only
    await using RedisContainer redis = new RedisBuilder("redis:7-alpine").Build();
    await redis.StartAsync();

    IProductPriceSource source = Substitute.For<IProductPriceSource>();
    source.GetPriceAsync("WIDGET", Arg.Any<CancellationToken>()).Returns(9.99m);

    // two "app instances", each with its own RedisCache client - pointing at the same Redis
    using RedisCache cache1 = new RedisCache(Options.Create(new RedisCacheOptions { Configuration = redis.GetConnectionString() }));
    using RedisCache cache2 = new RedisCache(Options.Create(new RedisCacheOptions { Configuration = redis.GetConnectionString() }));
    CachedProductPriceService instance1 = new CachedProductPriceService(cache1, source);
    CachedProductPriceService instance2 = new CachedProductPriceService(cache2, source);

    // Act
    decimal first = await instance1.GetPriceAsync("WIDGET");
    decimal second = await instance2.GetPriceAsync("WIDGET");

    // Assert - instance 2 got instance 1's cached price: the source was only called once (compare D_)
    first.Should().Be(9.99m);
    second.Should().Be(9.99m);
    await source.Received(1).GetPriceAsync("WIDGET", Arg.Any<CancellationToken>());
}
```
<sup><a href='/UnitTestingCookbook.Tests/CachingTests.cs#L160-L185' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachingTests_E_Redis_IsShared' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**NOTE:** Like [`DockerTests.A_Container`](./README_Docker.md), this test is `[Ignore]`d by default: starting a real
container adds real time to every full test-suite run. It was verified to pass locally (Docker running, `[Ignore]`
temporarily removed) - about 6 seconds, including starting the container.  

---

## How do I test HybridCache - and its protection against many callers loading the same value at once?

`HybridCache` (`Microsoft.Extensions.Caching.Hybrid`, .NET 9+) is Microsoft's newer caching API, and new code
increasingly uses it instead of raw `IDistributedCache`. `GetOrCreateAsync` does the "check, else load and store" work
itself, keeps an in-memory copy in front of any distributed cache (Redis, if one is registered), and serializes values
for you - no `GetStringAsync`/`decimal.Parse`:

<!-- snippet: HybridProductPriceService.cs -->
<a id='snippet-HybridProductPriceService.cs'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Support/Services/HybridProductPriceService.cs#L1-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-HybridProductPriceService.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

It also protects the source from a **stampede**: when many callers ask for the same missing key at once, only one of
them loads it and the rest wait for that result. Plain `IDistributedCache` read-through code like
`CachedProductPriceService` has no such protection - every concurrent caller that misses goes to the source. (Checked:
the same 10 overlapping callers below, run against `CachedProductPriceService`, call the source **10 times**;
through `HybridCache`, **once**.) To test
it, the source is held open (with a `TaskCompletionSource`) until all 10 callers have asked, so they genuinely overlap:

<!-- snippet: CachingTests_F_HybridCache_ConcurrentCallers_ShareOneLoad -->
<a id='snippet-CachingTests_F_HybridCache_ConcurrentCallers_ShareOneLoad'></a>
```cs
public async Task F_HybridCache_ConcurrentCallers_ShareOneLoad()
{
    // Arrange - the source is held open until every caller has asked, so all 10 genuinely overlap
    TaskCompletionSource<decimal> releaseSource = new TaskCompletionSource<decimal>(TaskCreationOptions.RunContinuationsAsynchronously);
    IProductPriceSource source = Substitute.For<IProductPriceSource>();
    source.GetPriceAsync("WIDGET", Arg.Any<CancellationToken>()).Returns(releaseSource.Task);

    ServiceCollection services = new ServiceCollection();
    services.AddHybridCache(); // no distributed cache registered, so HybridCache is in-memory only
    services.AddSingleton(source);
    services.AddTransient<HybridProductPriceService>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    HybridProductPriceService service = serviceProvider.GetRequiredService<HybridProductPriceService>();

    // Act
    Task<decimal>[] callers = Enumerable.Range(0, 10).Select(_ => service.GetPriceAsync("WIDGET")).ToArray();
    releaseSource.SetResult(9.99m);
    decimal[] prices = await Task.WhenAll(callers).WaitAsync(TimeSpan.FromSeconds(5));

    // Assert - 10 callers, 1 load
    prices.Should().AllBeEquivalentTo(9.99m);
    await source.Received(1).GetPriceAsync("WIDGET", Arg.Any<CancellationToken>());
}
```
<sup><a href='/UnitTestingCookbook.Tests/CachingTests.cs#L192-L217' title='Snippet source file'>snippet source</a> | <a href='#snippet-CachingTests_F_HybridCache_ConcurrentCallers_ShareOneLoad' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With no `IDistributedCache` registered, `AddHybridCache()` gives an in-memory-only cache - which, like
`MemoryDistributedCache`, is exactly what most unit tests want.

---

## Summary

| Testing approach | Good for | Misses |
|---|---|---|
| `MemoryDistributedCache` (real, in-memory) | Unit tests of code using `IDistributedCache`; expiration (with a fake clock) | Anything specific to a shared, out-of-process cache |
| Mocked `IDistributedCache` | Rarely worth it - forcing a specific cache *failure*, perhaps | Readability (byte arrays), real cache behavior |
| Redis via Testcontainers | Integration tests: sharing between instances, real connections | Speed - needs Docker, seconds per run |
| `HybridCache` via `AddHybridCache()` | Code written against `HybridCache`, including stampede protection | Same as `MemoryDistributedCache` unless a distributed cache is also registered |

---

Back to [README](../README.md)
