# Singleton HttpClient

## NuGet Packages Referenced

- Microsoft.Extensions.Http https://github.com/dotnet/runtime (`Microsoft.Extensions.Http` on NuGet)

All examples are located in `UnitTestingCookbook.Tests` -> [`SingletonHttpClientTests`](../UnitTestingCookbook.Tests/SingletonHttpClientTests.cs)

**NOTE:** This is a different failure mode than [HttpClientFactory](./README_HttpClientFactory.md) - that chapter
is about mocking `HttpClient`/`IHttpClientFactory` for a normal, short-lived consumer. This chapter is about a
consumer that's registered as a **Singleton** and holds the same `HttpClient` for the app's entire lifetime, and
the specific problem that creates.

---

## What's wrong with a Singleton holding one HttpClient forever?

A single long-lived `HttpClient` keeps its underlying connections (and the DNS resolution behind them) pinned for
as long as the process runs. If the remote endpoint's IP changes - a DNS failover, a container restart behind a
load balancer, a cloud provider swapping infrastructure - the Singleton keeps talking to the old IP until the
process itself restarts. `IHttpClientFactory`'s normal fix (rotating out `HttpMessageHandler` instances every 2
minutes by default, so a fresh one re-resolves DNS) doesn't apply here, because the whole point of a Singleton is
that it never asks the factory for a new client after startup.

## How do I fix that without giving up the Singleton?

Configure the primary handler explicitly with `SocketsHttpHandler.PooledConnectionLifetime` set to a bounded
value. This forces the *connections themselves* to be periodically torn down and re-established - including a
fresh DNS lookup - even though the C# `HttpClient` object the Singleton holds never changes.

```csharp
services.AddHttpClient(nameof(SingletonHttpService))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = expectedPooledConnectionLifetime,
    });
services.AddSingleton<ISingletonHttpService>(serviceProvider =>
    new SingletonHttpService(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SingletonHttpService))));
```

Note the factory delegate passed to `AddSingleton` calls `IHttpClientFactory.CreateClient()` exactly once - at
first resolution - and the container caches that one `HttpClient` for its lifetime. This is the standard "safe
Singleton" pattern: still built through `IHttpClientFactory` (so the primary handler is wired up the normal way),
but the handler's own `PooledConnectionLifetime` - not the factory's handler-rotation timer - is what keeps
connections fresh.

## How do I test that PooledConnectionLifetime is actually set, once it's built?

This is the interesting part: **there is no public API to read a configured primary handler back out of a built
`HttpClient`**. `HttpMessageInvoker` (`HttpClient`'s base type) stores it in a private `_handler` field, and
`IHttpClientFactory` wraps whatever you pass to `.ConfigurePrimaryHttpMessageHandler()` in a chain of internal
`DelegatingHandler`s (`LifetimeTrackingHttpMessageHandler`, `LoggingScopeHttpMessageHandler`,
`LoggingHttpMessageHandler`) before your `SocketsHttpHandler` shows up at the bottom.

`GetPrimaryHttpMessageHandler<T>()` (an `HttpClient` extension in `UnitTestingCookbook.TestHelpers`) does the
walk for you: it reaches the private `_handler` field via `GetFieldValue<T>()` (see
[General Tips](./README_GeneralTips.md) for the reflection helpers themselves), then follows each
`DelegatingHandler.InnerHandler` (a public property - no further reflection needed) until it finds a handler
assignable to `T`.

Confirming the DI wiring itself - that the consumer really is registered `Singleton`, not just declared that way
- uses the same `IServiceCollection`/`ServiceDescriptor` inspection technique as
[AwesomeAssertions Add-Ons](./README_AwesomeAssertionsAddOns.md#di-servicecollection-assertions-no-add-on).

```csharp
public void A_ConfigurePrimaryHttpMessageHandler_RegistersHandlerWithPooledConnectionLifetime()
{
    // Arrange
    TimeSpan expectedPooledConnectionLifetime = TimeSpan.FromMinutes(5);

    IServiceCollection services = new ServiceCollection();
    services.AddHttpClient(nameof(SingletonHttpService))
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = expectedPooledConnectionLifetime,
        });
    services.AddSingleton<ISingletonHttpService>(serviceProvider =>
        new SingletonHttpService(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SingletonHttpService))));

    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    ISingletonHttpService singletonHttpService = serviceProvider.GetRequiredService<ISingletonHttpService>();

    // Assert
    using (new AssertionScope())
    {
        // IServiceCollection registration - the consumer is registered as a true Singleton
        services.Should()
            .ContainSingle(d => d.ServiceType == typeof(ISingletonHttpService))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Resolving twice returns the same instance - proves Singleton, not just the descriptor's Lifetime enum
        serviceProvider.GetRequiredService<ISingletonHttpService>().Should().BeSameAs(singletonHttpService);

        // There is no public API to read a configured primary handler back out of a built HttpClient -
        // GetPrimaryHttpMessageHandler<T>() (TestHelpers) walks the private handler chain to find it.
        SocketsHttpHandler socketsHttpHandler = singletonHttpService.HttpClient.GetPrimaryHttpMessageHandler<SocketsHttpHandler>();
        socketsHttpHandler.PooledConnectionLifetime.Should().Be(expectedPooledConnectionLifetime);
    }
}
```

---

Back to [README](../README.md)
