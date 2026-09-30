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

```csharp
public void A_IConfiguration()
{
    // Arrange
    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Development", })
        .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("{ \"key\": \"value\" }")))
        .Build();

    // Assert

    // Act
    configuration["Logging:LogLevel:Default"].Should().Be("Debug");
    configuration["key"].Should().Be("value");
}
```

---

## How do I create a new 'scope'?

`.NotBeSameAs()` calls `ReferenceEquals()` under the hood.  

**NOTE:** `serviceProvider.CreateScope()` can also be used to create a new scope.  

```csharp
public void B_Scope()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddScoped<IScopedService, SampleScopedService>();

    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Assert
    IServiceScopeFactory? serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

    IScopedService scoped1;
    IScopedService scoped2;

    using (var scope = serviceScopeFactory.CreateScope())
    {
        scoped1 = scope.ServiceProvider.GetRequiredService<IScopedService>();
    }

    using (var scope = serviceScopeFactory.CreateScope())
    {
        scoped2 = scope.ServiceProvider.GetRequiredService<IScopedService>();
    }

    // Act
    scoped1.Should().NotBeSameAs(scoped2); // same as ReferenceEquals( scoped1, scoped2 ).Should().BeFalse()
}
```

---

## How do I setup IOptions<T> for testing?

```csharp
public void C_IOptions()
{
    // Arrange
    IOptions<MemoryDistributedCacheOptions> options = Options.Create<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions());

    // Assert

    // Act
    options.Should().NotBeNull();
    options.Value.Should().BeOfType<MemoryDistributedCacheOptions>()
        .Which.SizeLimit.Should().Be(200 * 1024 * 1024);
}
```

---

Back to [README](../README.md)
