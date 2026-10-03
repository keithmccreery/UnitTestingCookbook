using System.Text;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Testcontainers.Redis;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// IDistributedCache and HybridCache - see README_Caching.md
//
[Category("unit")]
[Category("caching")]
[TestFixture]
public class CachingTests
{
    //
    // Q: How do I test code that uses IDistributedCache - with the cache that's "just there"?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: CachingTests_A_DistributedMemoryCache_SecondCallIsCached
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
    // end-snippet

    //
    // Q: Why not just mock IDistributedCache?
    // NOTE: GetStringAsync/SetStringAsync are EXTENSION methods over the interface's byte[] GetAsync/SetAsync - a mock
    //       can't set up an extension method, so the test has to speak bytes.
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: CachingTests_B_MockingIDistributedCache_MeansMockingBytes
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
    // end-snippet

    //
    // Q: How do I test cache expiration without waiting 5 minutes?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: CachingTests_C_Expiration_WithFakeTime
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
    // end-snippet

    //
    // Q: Is MemoryDistributedCache actually distributed?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: CachingTests_D_MemoryDistributedCache_IsNotShared
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
    // end-snippet

    //
    // Q: How do I test against a real, shared Redis cache?
    // NOTE: Requires Docker - disabled by default, like DockerTests.A_Container
    //
    [Test]
    [Category("_passes")]
    [Ignore("Requires Docker - disabled by default to keep full test-suite runs fast during regular development")]
    // begin-snippet: CachingTests_E_Redis_IsShared
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
    // end-snippet

    //
    // Q: How do I test HybridCache - and its protection against many callers loading the same value at once?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: CachingTests_F_HybridCache_ConcurrentCallers_ShareOneLoad
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
    // end-snippet
}
