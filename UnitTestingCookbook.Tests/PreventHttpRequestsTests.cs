using System.Net;

using UnitTestingCookbook.TestHelpers;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[TestFixture]
public class PreventHttpRequestsTests
{
    private HttpRequestsDetector? _httpRequestsDetector;

    [SetUp]
    public void SetUp()
    {
        _httpRequestsDetector = new HttpRequestsDetector();
        _httpRequestsDetector.Subscribe();
    }

    [TearDown]
    public void TearDown()
    {
        _httpRequestsDetector?.Dispose();
    }

    [Test]
    [Category("_passes")]
    public async Task A_DisallowedHost_ThrowsBeforeRequestIsSent()
    {
        // Arrange
        using HttpClient httpClient = new HttpClient();

        // Act
        Func<Task> action = () => httpClient.GetAsync("https://example.invalid/");

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Requesting external resource*forbidden*");
    }

    [Test]
    [Category("_passes")]
    public async Task B_AllowedHost_RequestProceedsNormally()
    {
        // Arrange - WireMock.Net binds to "localhost" by default, already in AllowedHosts
        using WireMockServer wireMockServer = WireMockServer.Start();
        wireMockServer
            .Given(Request.Create().WithPath("/status/200").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

        using HttpClient httpClient = new HttpClient { BaseAddress = new Uri(wireMockServer.Urls[0]) };

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("/status/200");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
