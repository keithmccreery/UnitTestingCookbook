namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class HttpClientExtensionsTests
{
    [Test]
    [Category("_passes")]
    public void A_GetPrimaryHttpMessageHandler_FindsHandlerAtBottomOfDelegatingChain()
    {
        // Arrange
        SocketsHttpHandler socketsHttpHandler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        using PassThroughDelegatingHandler outer = new PassThroughDelegatingHandler { InnerHandler = socketsHttpHandler };
        using HttpClient httpClient = new HttpClient(outer);

        // Act
        SocketsHttpHandler result = httpClient.GetPrimaryHttpMessageHandler<SocketsHttpHandler>();

        // Assert
        result.Should().BeSameAs(socketsHttpHandler);
    }

    [Test]
    [Category("_passes")]
    public void B_GetPrimaryHttpMessageHandler_HandlerNotInChain_Throws()
    {
        // Arrange
        using HttpClient httpClient = new HttpClient(new SocketsHttpHandler());

        // Act
        Action action = () => httpClient.GetPrimaryHttpMessageHandler<HttpClientHandler>();

        // Assert
        action.Should().Throw<MissingMemberException>();
    }

    private sealed class PassThroughDelegatingHandler : DelegatingHandler;
}
