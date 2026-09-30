using System.Net;

using Microsoft.Extensions.Logging;

using Moq;
using Moq.Contrib.HttpClient;
using Moq.Protected;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("httpclientfactory")]
[TestFixture]
public class HttpClientFactoryTests
{
    //
    // Q: How do I Mock HttpClient? - Good
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Mock HttpClient? - Better
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I Mock HttpClient? - More Better
    // https://github.com/maxkagamine/Moq.Contrib.HttpClient
    //
    [Test]
    [Category("_passes")]
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
}
