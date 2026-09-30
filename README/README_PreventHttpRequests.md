# Preventing HTTP Requests to External Services in Unit Tests

## References

- Meziantou, "Prevent HTTP requests to external services in unit tests" https://www.meziantou.net/prevent-http-requests-to-external-services-in-unit-tests.htm

All examples are located in `UnitTestingCookbook.Tests` -> [`PreventHttpRequestsTests`](../UnitTestingCookbook.Tests/PreventHttpRequestsTests.cs)

**NOTE:** The source article applies this as a static class with a `[ModuleInitializer]` method, so it arms
itself automatically the moment the test assembly loads - protecting every test in the whole assembly forever,
with no per-test setup. This cookbook's `HttpRequestsDetector` (`UnitTestingCookbook.TestHelpers`) is the same
mechanism, but exposed as an instance with an explicit `Subscribe()`/`Dispose()` instead, so one chapter can
demonstrate it without silently instrumenting every other chapter sharing this Tests assembly - several of which
([WireMock.NET](./README_WireMockNet.md), [Polly Policies](./README_WireMockNetPollyPolicies.md)) deliberately
make real HTTP calls of their own. If you adopt this in your own project, the article's `[ModuleInitializer]`
version is the one to use - see the link above.

---

## How do I stop a test from silently hitting a real external service?

A test that forgets to mock `HttpClient` (or `IHttpClientFactory`) doesn't fail loudly - it just makes a real
network call, which is slow, flaky in CI, and sometimes actually reaches a live third-party service. There's no
compiler warning for this; you only find out when the test is slow or the network is unavailable.

**Answer:** Subscribe to the same `DiagnosticListener` that `System.Net.Http`'s `DiagnosticsHandler` raises
internally on every real request, and throw if the target host isn't on an explicit allow-list. Because this
listens at the transport layer, it only sees requests that actually reach a real `SocketsHttpHandler`/
`HttpClientHandler` - a Moq-mocked `HttpMessageHandler` never raises the event, so every other chapter that mocks
`HttpClient` (see [HttpClientFactory](./README_HttpClientFactory.md)) is unaffected.

```csharp
public sealed class HttpRequestsDetector : IDisposable
{
    public HashSet<string> AllowedHosts { get; } = new(StringComparer.OrdinalIgnoreCase) { "localhost" };

    private readonly DiagnosticSourceSubscriber subscriber;

    public HttpRequestsDetector()
    {
        subscriber = new DiagnosticSourceSubscriber(AllowedHosts);
    }

    public void Subscribe() => subscriber.Subscribe();

    public void Dispose() => subscriber.Dispose();

    private sealed class DiagnosticSourceSubscriber : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly HashSet<string> allowedHosts;
        private IDisposable? allListenersSubscription;
        private IDisposable? httpHandlerSubscription;

        public DiagnosticSourceSubscriber(HashSet<string> allowedHosts)
        {
            this.allowedHosts = allowedHosts;
        }

        public void Subscribe()
        {
            allListenersSubscription ??= DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "HttpHandlerDiagnosticListener")
                httpHandlerSubscription = value.Subscribe(new HttpHandlerDiagnosticListener(allowedHosts));
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void Dispose()
        {
            httpHandlerSubscription?.Dispose();
            httpHandlerSubscription = null;
            allListenersSubscription?.Dispose();
            allListenersSubscription = null;
        }

        private sealed class HttpHandlerDiagnosticListener : IObserver<KeyValuePair<string, object?>>
        {
            private static readonly Func<object, HttpRequestMessage?> GetRequestPropertyValue = CreateGetRequestPropertyValue();

            private readonly HashSet<string> allowedHosts;

            public HttpHandlerDiagnosticListener(HashSet<string> allowedHosts)
            {
                this.allowedHosts = allowedHosts;
            }

            public void OnNext(KeyValuePair<string, object?> value)
            {
                if (value.Key is not "System.Net.Http.Request")
                    return;

                HttpRequestMessage? request = GetRequestPropertyValue(value.Value!);
                if (request?.RequestUri?.Host is not string host)
                    return;

                if (!allowedHosts.Contains(host))
                    throw new InvalidOperationException($"Requesting external resource '{request.RequestUri}' from unit tests is forbidden");
            }

            public void OnCompleted() { }

            public void OnError(Exception error) { }

            // RequestData is an internal nested type on DiagnosticsHandler - not publicly exposed - so its
            // Request property has to be reached via reflection.
            private static Func<object, HttpRequestMessage?> CreateGetRequestPropertyValue()
            {
                Type requestDataType = Type.GetType("System.Net.Http.DiagnosticsHandler+RequestData, System.Net.Http", throwOnError: true)!;
                PropertyInfo requestProperty = requestDataType.GetProperty("Request")!;
                return o => (HttpRequestMessage?) requestProperty.GetValue(o);
            }
        }
    }
}
```

**NOTE:** The exception is thrown synchronously from inside the diagnostic listener's callback, *before* any DNS
resolution or socket connection happens - it genuinely prevents the request, it doesn't just log it after the
fact.

## Example

```csharp
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
```

---

Back to [README](../README.md)
