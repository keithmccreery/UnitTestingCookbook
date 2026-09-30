# AwesomeAssertions Add-Ons

## NuGet Packages Referenced

- AwesomeAssertions.Json https://github.com/AwesomeAssertions/AwesomeAssertions.Json
- AwesomeAssertions.Web https://github.com/adrianiftode/FluentAssertions.Web

All examples are located in `UnitTestingCookbook.Tests` -> [`AwesomeAssertionsAddOnsTests`](../UnitTestingCookbook.Tests/AwesomeAssertionsAddOnsTests.cs)

## No longer covered (FluentAssertions-only add-ons)

These FluentAssertions add-ons don't have an AwesomeAssertions equivalent (as of this writing), so they've been
dropped from this cookbook rather than pinned to the legacy, commercially-licensed FluentAssertions package:

- **FluentAssertions.Http**, **FluentAssertions.Mvc**, **FluentAssertions.AspNetCore.Mvc**, **FluentAssertions.Reactive**
  - none of these ever had a test example in this cookbook, so nothing to migrate.
- **FluentAssertions.Microsoft.Extensions.DependencyInjection**
  - this one *was* covered (asserting on `IServiceCollection`). See below for how to do the same thing with
    AwesomeAssertions' core collection assertions instead.

---

## AwesomeAssertions.Json

Asserts on `JToken`, `JObject`, and `JValue`.

```csharp
public void A_Json()
{
    // Arrange
    const string json =
@"
{
""name"": ""John"",
""age"": 22,
""gender"": ""male"",
}
";
    JToken jToken = JToken.Parse(json);

    // Act

    // Assert
    using (new AssertionScope())
    {
        jToken.Should().HaveCount(3);
        jToken.Should().HaveElement("name");
        jToken.Should().NotHaveElement("bozo");
    }
}
```

---

## AwesomeAssertions.Web

Asserts on `HttpResponseMessage`.

```csharp
public void B_Web()
{
    // Arrange
    const string json =
@"
{
""name"": ""John"",
""age"": 22,
""gender"": ""male"",
}
";
    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
    response.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
    response.Content = new StringContent(json);

    // Act

    // Assert
    using (new AssertionScope())
    {
        response.Should().Be200Ok()
            .And.BeAs(new
            {
                name = "John",
                age = 22,
                gender = "male",
            });
        response.Should().HaveHeader("X-Correlation-ID").And.NotBeEmpty();
    }
}
```

---

## DI ServiceCollection assertions (no add-on)

Asserts on `IServiceCollection` / `ServiceDescriptor`, directly - there is no AwesomeAssertions equivalent of
`FluentAssertions.Microsoft.Extensions.DependencyInjection`'s fluent
`.Should().HaveService<T>().WithImplementation<T>().AsSingleton()` API, so this uses AwesomeAssertions' core
collection assertions (`.ContainSingle()`, `.Which`, LINQ `.Where()`) against the plain `IServiceCollection`
instead.  

**NOTE:** Unlike [Dependency Injection](./README_DependencyInjection.md) (the source of truth for the basic
`ServiceCollection` setup pattern), this example builds a full Generic Host (`Host.CreateDefaultBuilder()`)
rather than a bare `ServiceCollection`, because the point here is asserting on what the *Host* registers by
default - a bare `ServiceCollection` wouldn't have any of that to assert on.  

**NOTE:** the exact service count below is tied to what `Microsoft.Extensions.Hosting`'s `Host.CreateDefaultBuilder()`
registers for the package version in use, and `EventLogLoggerProvider` is only registered on Windows - both will
need adjusting if/when the `Microsoft.Extensions.*` packages are bumped again.  

```csharp
public void X_DependencyInjection()
{
    // Arrange
    IServiceCollection? serviceCollection = null;
    IHost host = Host.CreateDefaultBuilder()
        .UseConsoleLifetime()
        .UseContentRoot(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!) // never null - a running test assembly always has a real file location
        .ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton<IOptions<Animal>>(Options.Create<Animal>(new Animal()));
            serviceCollection = services; // HACK: To get IServiceCollection
        })
        .Build();

    // Act

    // Assert
    using (new AssertionScope())
    {
        serviceCollection.Should().HaveCount(52);

        // With Implementation
        serviceCollection!.Should()
            .ContainSingle(d => d.ServiceType == typeof(IHostApplicationLifetime))
            .Which.Should().Match<ServiceDescriptor>(d =>
                d.ImplementationType == typeof(ApplicationLifetime)
                && d.Lifetime == ServiceLifetime.Singleton);

        // Instance only - No Implementation
        serviceCollection.Should()
            .ContainSingle(d => d.ServiceType == typeof(HostBuilderContext))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Factory - No Implementation
        serviceCollection.Should()
            .ContainSingle(d => d.ServiceType == typeof(IHost))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Multiple Implementations (Console/Debug/EventSource only - EventLog is Windows-only)
        IEnumerable<ServiceDescriptor> loggerProviderDescriptors =
            serviceCollection.Where(d => d.ServiceType == typeof(ILoggerProvider));

        loggerProviderDescriptors.Should().HaveCount(3)
            .And.OnlyContain(d => d.Lifetime == ServiceLifetime.Singleton);
        loggerProviderDescriptors.Select(d => d.ImplementationType).Should().BeEquivalentTo(new[]
        {
            typeof(ConsoleLoggerProvider),
            typeof(DebugLoggerProvider),
            typeof(EventSourceLoggerProvider),
        });

        // Generic - No Implementation
        serviceCollection.Should()
            .ContainSingle(d => d.ServiceType == typeof(IOptions<Animal>))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
```

---

Back to [README](../README.md)
