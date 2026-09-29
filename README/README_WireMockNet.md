# WireMock.NET

## NuGet Packages Referenced

- WireMock.Net https://github.com/wiremock/WireMock.Net
- WireMock.Net.AwesomeAssertions https://github.com/wiremock/WireMock.Net (same repo, `WireMock.Net.AwesomeAssertions` project)

Honorable Mentions...
- TestServer

All examples are located in `UnitTestingCookbook.Test` -> [`WireMockNetTest`](../UnitTestingCookbook.Test/WireMockNetTest.cs)  

**NOTE:** The `IServiceCollection`/DI wiring below follows the same basic pattern as
[Dependency Injection](./README_DependencyInjection.md) (the source of truth for that pattern) - it's declared as
`IServiceCollection` rather than the concrete `ServiceCollection` type here since that's what gets registered
against a fake endpoint for the test to consume.  

---

# Background

"WireMock.Net is a tool which mimics the behaviour of an HTTP API,
it captures the HTTP requests and sends it to WireMock.Net HTTP server,
which is started and as a result, we can setup expectations,
call the service and then verify its behaviour." [^1]

- What is WireMock.Net https://github.com/WireMock-Net/WireMock.Net/wiki/What-Is-WireMock.Net

## Stubbing (Response)

"The core feature of WireMock is the ability to return predefined HTTP responses for requests matching criteria."

- https://github.com/WireMock-Net/WireMock.Net/wiki/Stubbing

## Request Matching

"WireMock.Net supports matching of requests to stubs and verification queries..."

- https://github.com/WireMock-Net/WireMock.Net/wiki/Request-Matching

## Response Templating

"Response headers and bodies can optionally be rendered (templated)..."

- https://github.com/WireMock-Net/WireMock.Net/wiki/Response-Templating

[^1]: https://github.com/WireMock-Net/WireMock.Net/wiki/What-Is-WireMock.Net

---

## How do I test a service without accessing the service?

Alternate Questions...  
- How do I test my service code WITHOUT Mocking anything?
- What is the best way to test `HttpClient` or `IHttpClientFactory`?

**Answer:** Fake the services. That way all code paths are tested without changes / Mocks.  

Using WireMock.NET, the requests and responses are defined/mapped.

### One Time Setup

```csharp
private static WireMockServer _wireMockServer;
private IServiceProvider? _serviceProvider;
private string? _baseUrl;

const string ENDPOINT_STATUS_OK = "/status/200"; // requires leading slash

[OneTimeSetUp]
public void OneTimeSetUp()
{
    //
    // WireMock
    //
    _wireMockServer = WireMockServer.Start();
    _baseUrl = _wireMockServer.Urls[0];

    // status endpoint
    _wireMockServer
        .Given(
            Request.Create()
                .WithPath(ENDPOINT_STATUS_OK)
                .UsingGet()
        )
        .RespondWith(
            Response.Create()
                .WithHeader("Authorization", "valid")
                .WithStatusCode(HttpStatusCode.OK)
        );
}
```

### Setup (before each run)

```csharp
public void SetUp()
{
    //
    // Clear Logs
    //
    _wireMockServer.ResetLogEntries();

    //
    // services
    //
    IServiceCollection services = new ServiceCollection();
    services.AddScoped<IHttpBinOrgService, HttpBinOrgService>();
    services.AddHttpClient("HttpBinOrg", client =>
    {
        client.BaseAddress = new Uri(_baseUrl!); // setup capture of URLs
    });

    _serviceProvider = services.BuildServiceProvider(true);
}
```

### TearDown

```csharp
[TearDown]
public void TearDown()
{
    (_serviceProvider as IDisposable)?.Dispose();
}
```

### One Time TearDown

```csharp
[OneTimeTearDown]
public void OneTimeTearDown()
{
    _wireMockServer.Stop();
    _wireMockServer.Dispose();
}
```

---

### Example

Notice NO MOCKs!  

Once the call is made, the `..HaveReceivedACall()` Assertion can be performed.
Additionally, the WireMock `.LogEntries` can be interrogated to view the details of the requests and responses.  

```csharp
public async Task A_WireMockNet()
{
    // Arrange
    IServiceScopeFactory? serviceScopeFactory = _serviceProvider!.GetRequiredService<IServiceScopeFactory>();
    using IServiceScope? scope = serviceScopeFactory.CreateScope();

    IHttpBinOrgService service = scope!.ServiceProvider.GetRequiredService<IHttpBinOrgService>();

    // Act
    await service.GetStatusAsync(HttpStatusCode.OK);

    // Assert
    _wireMockServer.Should()
        .HaveReceivedACall()
        .AtUrl($"{_baseUrl}{ENDPOINT_STATUS_OK}");

    _wireMockServer.LogEntries.Should().ContainSingle()
        .Which
        .ResponseMessage
        .Headers!.FirstOrDefault(h => h.Key.Equals("Authorization"))
        .Value.FirstOrDefault().Should().Be("valid");
}
```

---

Back to [README](../README.md)
