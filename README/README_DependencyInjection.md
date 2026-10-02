# Dependency Injection

All examples are located in `UnitTestingCookbook.Tests` -> [`DependencyInjectionTests`](../UnitTestingCookbook.Tests/DependencyInjectionTests.cs)   

**NOTE:** This chapter is the source of truth for the basic `ServiceCollection` setup pattern used across the
cookbook. A few other chapters wire up a `ServiceCollection` differently because they're solving a different
problem, not because of drift - `Logging` and `WireMockNet`/`WireMockNetPollyPolicies` need an `IHttpBinOrgService`
registered against a fake/local endpoint, and `AwesomeAssertionsAddOns`' `X_DependencyInjection` uses the Generic
Host (`Host.CreateDefaultBuilder()`) specifically because it's asserting on what the *Host* registers by default,
not on a bare `ServiceCollection`. Each of those chapters is self-contained (its full setup is inline, not just a
link here), so you shouldn't need to bounce between READMEs to see any one demo work end to end.  

---

## How do I setup IConfiguration for testing?

<!-- snippet: DependencyInjectionTests_A_IConfiguration -->
<a id='snippet-DependencyInjectionTests_A_IConfiguration'></a>
```cs
public void A_IConfiguration()
{
    // Arrange
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Development", })
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("{ \"key\": \"value\" }")))
        .Build();

    // Act / Assert
    configuration["Logging:LogLevel:Default"].Should().Be("Debug");
    configuration["key"].Should().Be("value");
}
```
<sup><a href='/UnitTestingCookbook.Tests/DependencyInjectionTests.cs#L23-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-DependencyInjectionTests_A_IConfiguration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I create a new 'scope'?

A scoped registration has two properties, and the test checks both:
- **Same scope -> same instance.** `.BeSameAs()` fails if the service were registered `Transient` (a new instance per resolve).
- **Different scope -> different instance.** `.NotBeSameAs()` fails if the service were registered `Singleton` (one
instance for everything).

Asserting only the second one (different scopes) isn't enough: a `Transient` registration would pass that too.  

`.BeSameAs()`/`.NotBeSameAs()` call `ReferenceEquals()` under the hood.  

**NOTES:**
- `serviceProvider.CreateScope()` is a shortcut extension method that does the same as resolving `IServiceScopeFactory` and calling
`.CreateScope()` on it.
- `BuildServiceProvider(true)` turns on scope validation: resolving a scoped service from the *root* provider throws,
instead of quietly giving you an instance that lives as long as the provider (effectively a singleton).
- Both scopes stay open until the end of the test, so every resolved instance is still live when it's asserted on.
Disposing a scope also disposes the `IDisposable` services it created, so don't keep a service around after its scope is disposed.

<!-- snippet: DependencyInjectionTests_B_Scope -->
<a id='snippet-DependencyInjectionTests_B_Scope'></a>
```cs
public void B_Scope()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddScoped<IScopedService, SampleScopedService>();

    using ServiceProvider serviceProvider = services.BuildServiceProvider(true); // true = validateScopes

    IServiceScopeFactory serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

    // Act
    // Both scopes stay open until the end of the test, so every instance is still live (not yet disposed by
    // its scope) when it's asserted on - matters if the service were IDisposable.
    using IServiceScope scope1 = serviceScopeFactory.CreateScope();
    using IServiceScope scope2 = serviceScopeFactory.CreateScope();

    IScopedService scope1First = scope1.ServiceProvider.GetRequiredService<IScopedService>();
    IScopedService scope1Second = scope1.ServiceProvider.GetRequiredService<IScopedService>();
    IScopedService scope2First = scope2.ServiceProvider.GetRequiredService<IScopedService>();

    // Assert
    using (new AssertionScope())
    {
        scope1First.Should().BeSameAs(scope1Second); // same scope -> same instance (would fail if registered Transient)
        scope1First.Should().NotBeSameAs(scope2First); // different scope -> new instance (would fail if registered Singleton)
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/DependencyInjectionTests.cs#L46-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-DependencyInjectionTests_B_Scope' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I setup IOptions<T> for testing?

<!-- snippet: DependencyInjectionTests_C_IOptions -->
<a id='snippet-DependencyInjectionTests_C_IOptions'></a>
```cs
public void C_IOptions()
{
    // Arrange
    IOptions<MemoryDistributedCacheOptions> options = Options.Create<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions());

    // Act / Assert
    options.Should().NotBeNull();
    options.Value.Should().BeOfType<MemoryDistributedCacheOptions>()
        .Which.SizeLimit.Should().Be(200 * 1024 * 1024);
}
```
<sup><a href='/UnitTestingCookbook.Tests/DependencyInjectionTests.cs#L81-L92' title='Snippet source file'>snippet source</a> | <a href='#snippet-DependencyInjectionTests_C_IOptions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

Back to [README](../README.md)
