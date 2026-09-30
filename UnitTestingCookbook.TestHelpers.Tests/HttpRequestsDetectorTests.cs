namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class HttpRequestsDetectorTests
{
    [Test]
    [Category("_passes")]
    public void A_AllowedHosts_DefaultsToLocalhostOnly()
    {
        // Arrange
        using HttpRequestsDetector httpRequestsDetector = new HttpRequestsDetector();

        // Act

        // Assert
        httpRequestsDetector.AllowedHosts.Should().ContainSingle().Which.Should().Be("localhost");
    }

    [Test]
    [Category("_passes")]
    public async Task B_Subscribed_DisallowedHost_ThrowsBeforeRequestIsSent()
    {
        // Arrange
        using HttpRequestsDetector httpRequestsDetector = new HttpRequestsDetector();
        httpRequestsDetector.Subscribe();
        using HttpClient httpClient = new HttpClient();

        // Act
        Func<Task> action = () => httpClient.GetAsync("https://example.invalid/");

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Requesting external resource*forbidden*");
    }

    [Test]
    [Category("_passes")]
    public async Task C_Disposed_NoLongerIntercepts()
    {
        // Arrange
        HttpRequestsDetector httpRequestsDetector = new HttpRequestsDetector();
        httpRequestsDetector.Subscribe();
        httpRequestsDetector.Dispose();

        using HttpClient httpClient = new HttpClient();

        // Act
        Func<Task> action = () => httpClient.GetAsync("https://example.invalid/");

        // Assert - fails for a real network reason (unresolvable host), not the detector's exception
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    [Category("_passes")]
    public async Task D_NotSubscribed_DoesNotIntercept()
    {
        // Arrange
        using HttpRequestsDetector httpRequestsDetector = new HttpRequestsDetector();
        using HttpClient httpClient = new HttpClient();

        // Act
        Func<Task> action = () => httpClient.GetAsync("https://example.invalid/");

        // Assert - fails for a real network reason (unresolvable host), not the detector's exception
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
