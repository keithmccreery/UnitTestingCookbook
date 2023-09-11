# FluentAssertions Add-Ons

## NuGet Packages Referenced

- FluentAssertions.Json https://github.com/fluentassertions/fluentassertions.json
- FluentAssertions.Web https://github.com/adrianiftode/FluentAssertions.Web
- FluentAssertions.Http https://github.com/balanikas/FluentAssertions.Http
- FluentAssertions.Mvc https://github.com/fluentassertions/fluentassertions.mvc
- FluentAssertions.ApsNetCore.Mvc https://github.com/fluentassertions/fluentassertions.aspnetcore.mvc
- FluentAssertions.Reactive https://github.com/fluentassertions/fluentassertions.reactive
- FluentAssertions.Microsoft.Extensions.DependencyInjection https://github.com/zachdean/FluentAssertions.Microsoft.Extensions.DependencyInjection


All examples are located in `UnitTestingCookbook.Test` -> [`FluentAssertionsAddOnsTest`](../UnitTestingCookbook.Test/FluentAssertionsAddOnsTest.cs)

---

## FluentAssertions.Json

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
    JToken jToken = JToken.Parse( json );

    // Act

    // Assert
    using ( new AssertionScope() )
    {
        jToken.Should().HaveCount( 3 );
        jToken.Should().HaveElement( "name" );
        jToken.Should().NotHaveElement( "bozo" );
    }
}
```

---

## FluentAssertions.Web

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
    HttpResponseMessage response = new HttpResponseMessage( HttpStatusCode.OK );
    response.Headers.Add( "X-Correlation-ID", Guid.NewGuid().ToString() );
    response.Content = new StringContent( json );

    // Act

    // Assert
    using ( new AssertionScope() )
    {
        response.Should().Be200Ok()
            .And.BeAs( new
            {
                name = "John",
                age = 22,
                gender = "male",
            } );
        response.Should().HaveHeader( "X-Correlation-ID" ).And.NotBeEmpty();
    }
}
```

---

## FluentAssertions.Http

Asserts on `HttpResponseMessage`.  
**NOTE:** NOT Heavy Used.  

---

## FluentAssertions.Mvc

**NOTE:** .NET Framework  

---

## FluentAssertions.ApsNetCore.Mvc

Asserts on `IActionResult`, `ActionResult`, `RouteData`, and `IConvertToActionResult`.  

**NOTE:** .NET Core  

---

## FluentAssertions.Reactive

Asserts on `IObservable`.  

---

## FluentAssertions.Microsoft.Extensions.DependencyInjection

Asserts on `IServiceCollection`.  
**NOTE:** Doesn't handle generics, builders or factories.  

```csharp
public void X_DependencyInjection()
{
    // Arrange
    IServiceCollection? serviceCollection = null;
    IHost host = Host.CreateDefaultBuilder()
        .UseConsoleLifetime()
        .UseContentRoot(
            Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) )
        .ConfigureServices( ( hostContext, services ) =>
        {
            services.AddSingleton<IOptions<Animal>>( Options.Create<Animal>( new Animal() ) );
            serviceCollection = services; // HACK: To get IServiceCollection
        } )
        .Build();

    // Act

    // Assert
    using ( new AssertionScope() )
    {
        serviceCollection.Should().HaveCount( 44 );

        // With Implementation
        serviceCollection.Should()
            .HaveService<IHostApplicationLifetime>()
            .WithImplementation<ApplicationLifetime>()
            .AsSingleton();

        // Instance / Implementation only - No Interface
        serviceCollection.Should()
            .HaveService<HostBuilderContext>()
            .AsSingleton();

        // Factory - No Implementation
        serviceCollection.Should()
            .HaveService<IHost>()
            .AsSingleton();

        // Multiple Implementations
        serviceCollection.Should()
            .HaveService<ILoggerProvider>()
            .WithCount( 4 )
            .WithImplementation<ConsoleLoggerProvider>()
            .WithImplementation<DebugLoggerProvider>()
            .WithImplementation<EventLogLoggerProvider>()
            .WithImplementation<EventSourceLoggerProvider>()
            .AsSingleton();

        // Generic - No Implementation
        serviceCollection.Should()
            .HaveService<IOptions<Animal>>()
            .AsSingleton();
    }
}
```

---

Back to [README](../README.md)
