# HttpClientFactory

## NuGet Packages Referenced

- Moq https://github.com/moq/moq
- Moq.Contrib.HttpClient https://github.com/maxkagamine/Moq.Contrib.HttpClient

Honorable Mentions...
- mockhttp https://github.com/richardszalay/mockhttp

## Web Sites Referenced

- HttpBin.org https://httpbin.org

All examples are located in `UnitTestingCookbook.Test` -> [`HttpClientFactoryTest`](../UnitTestingCookbook.Test/HttpClientFactoryTest.cs)  

---

## How do I Mock HttpClient?

### Answer 1

You can't. It doesn't have an Interface.

### Answer 2 - Good

Mock `HttpMessageHandler` and instantiate `HttpClient` with the `HttpMessageHandler`.  

**NOTE:** This example only illustrates the 'wiring'.  

```csharp
public async Task A_Mock_HttpClient()
{
    // Arrange
    HttpResponseMessage httpResponseMessage = new HttpResponseMessage()
    {
        StatusCode = HttpStatusCode.OK,
    };

    var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    httpMessageHandlerMock
        .Protected() // Protected Method
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(httpResponseMessage);
    HttpMessageHandler httpMessageHandler = httpMessageHandlerMock.Object;

    HttpClient httpClient = new HttpClient(httpMessageHandler); // No Interface
    httpClient.BaseAddress = new Uri("https://httpbin.org/"); // base has trailing slash

    // Act
    await Task.Delay(1);

    // Assert
    httpMessageHandlerMock
        .Protected()
        .Verify("SendAsync", Times.Exactly(0), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get), ItExpr.IsAny<CancellationToken>());
}
```

### Answer 2 - Better

Assuming you are using `IHttpClientFactory`, the same process is used -
Mock `HttpMessageHandler` and instantiate `HttpClient` with the `HttpMessageHandler`
and final Mock `IHttpClientFactory`, with a `.CreateClient()` Setup.  

```csharp
public async Task B_Mock_HttpClientFactory()
{
    // Arrange
    HttpResponseMessage httpResponseMessage = new HttpResponseMessage()
    {
        StatusCode = HttpStatusCode.OK,
    };

    var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    httpMessageHandlerMock
        .Protected() // Protected Method
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(httpResponseMessage);
    HttpMessageHandler httpMessageHandler = httpMessageHandlerMock.Object;

    HttpClient httpClient = new HttpClient(httpMessageHandler); // No Interface
    httpClient.BaseAddress = new Uri("https://httpbin.org/"); // base has trailing slash

    var httpClientFactoryMock = new Mock<IHttpClientFactory>();
    httpClientFactoryMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);
    IHttpClientFactory httpClientFactory = httpClientFactoryMock.Object;

    var loggerMock = new Mock<ILogger<HttpBinOrgService>>();
    ILogger<HttpBinOrgService> logger = loggerMock.Object;

    IHttpBinOrgService service = new HttpBinOrgService(httpClientFactory, logger);

    // Act
    HttpStatusCode result = await service.GetStatusAsync(HttpStatusCode.OK);

    // Assert
    result.Should().Be(HttpStatusCode.OK);

    httpMessageHandlerMock
        .Protected()
        .Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get), ItExpr.IsAny<CancellationToken>());
}
```

### Answer 3 - (More) Better (but not Best)

Using Moq.Contrib.HttpClient.  

**NOTICE:** Moq.Contrib.HttpClient adds `.CreateClient()`, `.SetupRequest()` and `.Return()` to `HttpMessageHandler` Mock.  

```csharp
public async Task C_Mock_HttpClientFactory_Best()
{
    // Arrange
    HttpResponseMessage httpResponseMessage = new HttpResponseMessage()
    {
        StatusCode = HttpStatusCode.OK,
    };

    var httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    httpMessageHandlerMock
        .SetupRequest(HttpMethod.Get, "https://httpbin.org/status/200")
        .Returns((HttpRequestMessage _, CancellationToken _) => Task.FromResult<HttpResponseMessage>(httpResponseMessage)); // async/await

    IHttpClientFactory httpClientFactory = httpMessageHandlerMock.CreateClientFactory();
    Mock.Get(httpClientFactory)
        .Setup(_ => _.CreateClient("HttpBinOrg"))
        .Returns(() =>
        {
            HttpClient client = httpMessageHandlerMock.CreateClient();
            client.BaseAddress = new Uri("https://httpbin.org/");
            return client;
        });

    var loggerMock = new Mock<ILogger<HttpBinOrgService>>();
    ILogger<HttpBinOrgService> logger = loggerMock.Object;

    IHttpBinOrgService service = new HttpBinOrgService(httpClientFactory, logger);

    // Act
    HttpStatusCode result = await service.GetStatusAsync(HttpStatusCode.OK);

    // Assert
    result.Should().Be(HttpStatusCode.OK);

    httpMessageHandlerMock
        .Protected()
        .Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get), ItExpr.IsAny<CancellationToken>());
}
```

### Answer 4 - Best

See [WireMock.NET](./README_WireMockNet.md)

---

Back to [README](../README.md)
