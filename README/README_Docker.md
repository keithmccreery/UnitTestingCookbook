# Docker

## NuGet Packages Referenced

- testcontainers https://github.com/testcontainers/testcontainers-dotnet
    - https://dotnet.testcontainers.org/

**NOTE:** Requires Docker

**NOTE:** `DockerTests.A_Container` is permanently `[Ignore]`d - not because the technique doesn't work, but as a
deliberate development-speed tradeoff. Pulling and starting a real container adds real wall-clock time (and a
Docker dependency) to every single full test-suite run, which most day-to-day development doesn't want paying
for on every save. This chapter exists to document the Testcontainers pattern - verified to pass locally (Docker running,
`[Ignore]` attribute temporarily removed) - rather than to run routinely alongside the rest of the suite.

All examples are located in `UnitTestingCookbook.Tests` -> [`DockerTests`](../UnitTestingCookbook.Tests/DockerTests.cs)   

---

## Why httpbin?

httpbin is used as the target service throughout this cookbook (this chapter runs a real instance in Docker;
`HttpClientFactory` and `WireMockNetPollyPolicies` mock/stub against its URL shape without needing Docker) because
its endpoints are a predictable **mirror** of the request: `/status/<code>` returns that status code, `/get`
echoes back headers/query params as JSON, `/delay/<n>` waits `n` seconds before responding, etc. That
determinism - "call this path, get exactly this back" - is what makes it a good stand-in for "some HTTP API" in
a testing example, without needing a real backend.

**NOTE:** httpbin.org itself is now maintained by Postman ([postmanlabs/httpbin](https://github.com/postmanlabs/httpbin))
rather than by original author Kenneth Reitz. The `kennethreitz/httpbin` Docker Hub image this chapter runs is an
older, separately-maintained image (last updated years ago) - it still works fine for this demo, but if it ever
stops pulling/running, [`mccutchen/go-httpbin`](https://github.com/mccutchen/go-httpbin) is an actively maintained,
drop-in-compatible reimplementation with its own published Docker image.

---

## How do I execute a Docker container for testing?

### SetUp

<!-- snippet: DockerTests_OneTimeSetup -->
<a id='snippet-DockerTests_OneTimeSetup'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/DockerTests.cs#L15-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-DockerTests_OneTimeSetup' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### TearDown

<!-- snippet: DockerTests_OneTimeTearDown -->
<a id='snippet-DockerTests_OneTimeTearDown'></a>
```cs
public async Task OneTimeTearDown()
{
    await _container.StopAsync().ConfigureAwait(false);
    await _container.DisposeAsync().ConfigureAwait(false);
}
```
<sup><a href='/UnitTestingCookbook.Tests/DockerTests.cs#L33-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-DockerTests_OneTimeTearDown' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Solution

<!-- snippet: DockerTests_A_Container -->
<a id='snippet-DockerTests_A_Container'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/DockerTests.cs#L47-L60' title='Snippet source file'>snippet source</a> | <a href='#snippet-DockerTests_A_Container' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**NOTES:** TestContainer also supports creating containers.  

---

Back to [README](../README.md)
