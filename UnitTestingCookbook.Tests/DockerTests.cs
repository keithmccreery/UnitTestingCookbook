using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("docker")]
[TestFixture]
public class DockerTests
{
    IContainer _container;

    // https://httpbin.org
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        const ushort port = 80;

        _container = new ContainerBuilder("kennethreitz/httpbin:latest")
            .WithName("httpbinorg")
            .WithCleanUp(false) // must be false - default or true will fail (BUG)
            .WithAutoRemove(true)
            .WithPortBinding(port, port)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPath("/")))
            .Build();

        await _container.StartAsync().ConfigureAwait(false);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _container.StopAsync().ConfigureAwait(false);
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    //
    // Q: How do I execute a Docker container for testing?
    //
    [Test]
    [Category("_passes")]
    [Ignore("Requires Docker - disabled by default to keep full test-suite runs fast during regular development")]
    public async Task A_Container()
    {
        // Arrange
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/status/201");
        using HttpClient client = new HttpClient();

        // Act
        using HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        response.Should().Be201Created();
    }
}
