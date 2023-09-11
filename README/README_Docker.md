# Docker

## NuGet Packages Referenced

- testcontainers https://github.com/testcontainers/testcontainers-dotnet
    - https://dotnet.testcontainers.org/

**NOTE:** Requires Docker

All examples are located in `UnitTestingCookbook.Test` -> [`DockerTest`](../UnitTestingCookbook.Test/DockerTest.cs)   

---

## How do I execute a Docker container for testing?

### SetUp

```csharp
public async Task OneTimeSetup()
{
    const ushort port = 80;

    _container = new ContainerBuilder()
        .WithImage( "kennethreitz/httpbin:latest" )
        .WithName( "httpbinorg" )
        .WithCleanUp( false ) // must be false - default or true will fail (BUG)
        .WithAutoRemove( true )
        .WithPortBinding( port, port )
        .WithWaitStrategy( Wait.ForUnixContainer().UntilHttpRequestIsSucceeded( request => request.ForPath( "/" ) ) )
        .Build();

    await _container.StartAsync().ConfigureAwait( false );
}
```

### TearDown

```csharp
public async Task OneTimeTearDown()
{
    await _container.StopAsync().ConfigureAwait( false );
    await _container.DisposeAsync().ConfigureAwait( false );
}
```

### Solution

```csharp
public async Task A_Container()
{
    // Arrange
    using HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Get, "http://localhost/status/201" );
    using HttpClient client = new HttpClient();

    // Act
    using HttpResponseMessage response = await client.SendAsync( request );

    // Assert
    response.Should().Be201Created();
}
```

**NOTES:** TestContainer also supports creating containers.  

---

Back to [README](../README.md)
