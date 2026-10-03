# WireMock.Net + Polly (Resilience)

## NuGet Packages Referenced

- WireMock.Net https://github.com/wiremock/WireMock.Net
- WireMock.Net.AwesomeAssertions https://github.com/wiremock/WireMock.Net/tree/master/src/WireMock.Net.AwesomeAssertions
- Microsoft.Extensions.Http.Resilience https://github.com/dotnet/extensions (Polly v8 resilience pipelines for `HttpClient`)
- Polly https://github.com/App-vNext/Polly, with Microsoft.Extensions.Http.Polly (the v7-style policies, kept for comparison)
- Serilog.Extensions.Logging https://github.com/serilog/serilog-extensions-logging and Humanizer https://github.com/Humanizr/Humanizer (v7 logging only)

## Web Sites Referenced

- HttpBin.org https://httpbin.org

Examples are located in `UnitTestingCookbook.Tests` ->
- [`WireMockNetResilienceTests`](../UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs) - **Polly v8** resilience pipelines (the current approach)
- [`WireMockNetPollyPoliciesTests`](../UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs) - **Polly v7** policies, kept for comparison

**NOTE:** See [WireMock.Net](./README_WireMockNet.md) for WireMock basics. This chapter focuses on using WireMock.Net
to **simulate failing and slow servers** - the only realistic way to test resilience code - and Polly is the code
being tested.  

---

## Why test resilience code with WireMock.Net?

Retry, timeout, and circuit-breaker code only runs when something goes **wrong**, so in normal testing it never runs
at all. A mocked `HttpMessageHandler` *can* be made slow, made to fail once then succeed, and made to count calls - but
by then you're hand-building a fake server. WireMock.Net is a real HTTP server running inside the test, so the whole
`HttpClient` pipeline - resilience handler, real sockets, real timeouts - runs exactly as in production:

- **Failures on demand** - any status code (`500`), any delay (`.WithDelay(...)`).
- **Behavior over time** - *Scenarios and States* (fail on the 1st call, succeed after), or changing a stub mid-test.
- **The server's view** - `LogEntries` records every request that actually arrived, with a timestamp, so a test can
assert *how many* attempts were made and *how far apart*, rather than trusting the client's logs.

**Fun Fact:** I have worked on code where
(1) the Polly Policies were not defined in the correct order;
(2) they produced `NullReferenceException`s in the callbacks;
and/or (3) they failed to specify the proper exceptions to handle.
Each of those is caught by the tests in this chapter - including (1): moving the timeout to the *outside* of the
retry makes `C_` below fail (verified by trying it).  

---

## Polly v7 vs. v8 - what changed

| | Polly v7 policies | Polly v8 resilience pipelines |
|---|---|---|
| Package | `Microsoft.Extensions.Http.Polly` (+ deprecated `Polly.Extensions.Http`) | `Microsoft.Extensions.Http.Resilience` |
| Wiring | `.AddPolicyHandler(Policy.WrapAsync(retry, breaker, timeout))` | `.AddResilienceHandler("name", pipeline => pipeline.AddRetry(...).AddCircuitBreaker(...).AddTimeout(...))` |
| Order | `Policy.WrapAsync(...)`: first argument is outermost | builder: first strategy added is outermost |
| "Which failures?" | `HttpPolicyExtensions.HandleTransientHttpError().Or<TimeoutRejectedException>()` | built into `HttpRetryStrategyOptions` / `HttpCircuitBreakerStrategyOptions` by default |
| Circuit breaker | consecutive failures (`16` in a row) | failure **ratio** over a sampling window (`FailureRatio`, `MinimumThroughput`) |
| Logging | hand-written callbacks + a `Context` carrying the logger (`PollyContextExtensions`) | built in: every resilience event is logged through the app's `ILogger` |
| Defaults | build every policy yourself | `AddStandardResilienceHandler()` - a production-ready pipeline in one line |

The v8 version runs the **same scenarios** as the v7 one, against the same WireMock endpoints. The fixture runs in
about **9 seconds** instead of about a minute: the delays are milliseconds instead of seconds, and there are no fixed
sleeps.

---

## Polly v8 - the current approach

### A fresh WireMock server for every test

<!-- snippet: WireMockNetResilienceTests_SetUp -->
<a id='snippet-WireMockNetResilienceTests_SetUp'></a>
```cs
// A brand-new WireMock server for every test: its request log and its scenario state start empty, so nothing a
// previous test left behind - like a slow response still finishing - can leak into this one. Starting a server
// takes milliseconds.
[SetUp]
public void SetUp()
{
    _wireMockServer = WireMockServer.Start();

    _wireMockServer
        .Given(Request.Create().WithPath(ENDPOINT_STATUS_OK).UsingGet())
        .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

    _wireMockServer
        .Given(Request.Create().WithPath(ENDPOINT_STATUS_INTERNALSERVER).UsingGet())
        .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

    _wireMockServer
        .Given(Request.Create().WithPath(ENDPOINT_STATUS_REQUESTTIMEOUT).UsingGet())
        .RespondWith(Response.Create().WithDelay(SlowResponseDelay).WithStatusCode(HttpStatusCode.RequestTimeout));

    // State machine: the 1st call fails, every call after that succeeds
    _wireMockServer
        .Given(Request.Create().WithPath(ENDPOINT_STATUS_STATEMACHINE).UsingGet())
        .InScenario("Fail then Succeed")
        .WillSetStateTo("Failed")
        .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

    _wireMockServer
        .Given(Request.Create().WithPath(ENDPOINT_STATUS_STATEMACHINE).UsingGet())
        .InScenario("Fail then Succeed")
        .WhenStateIs("Failed")
        .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.Created));
}

[TearDown]
public void TearDown()
{
    _serviceProvider?.Dispose();
    _wireMockServer.Stop();
    _wireMockServer.Dispose();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L46-L88' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_SetUp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The v7 fixture shares **one** server across all its tests and clears its log in `[SetUp]`, with a 3-second
`Task.Delay` in `[TearDown]`. That delay is needed because **WireMock logs a request only once its response has
finished**, and some tests deliberately give up on slow responses: `B_` cancels after 1 second and `D_` times out after
2, while the server holds those responses for 3 seconds. Those responses then finish - and get logged - *after* the
next test has cleared the log. Removing the delay proves it: the next tests see **one extra request each** (`C_` finds
4 instead of 3, `E_` finds 17 instead of 16).

A shared server also carries **scenario state** between tests: once the "Fail then Succeed" scenario has advanced, any
later test hitting that endpoint starts in the "already failed" state. That's [Data Hangover](./README_DataHangover.md)
at the HTTP level. A new server per test fixes both, and starting one takes milliseconds.

### The pipeline under test, and the service wiring

<!-- snippet: WireMockNetResilienceTests_CookbookPipeline -->
<a id='snippet-WireMockNetResilienceTests_CookbookPipeline'></a>
```cs
// The same three strategies as the v7 PolicyWrap, in the same order: the first strategy added is the outermost.
//   retry (outermost) -> circuit breaker -> timeout per attempt (innermost)
// The Http* options' default ShouldHandle already covers transient HTTP failures - 5xx, 408, 429,
// HttpRequestException, and TimeoutRejectedException - which v7 needed Polly.Extensions.Http's
// HandleTransientHttpError().Or<TimeoutRejectedException>() for.
private static void CookbookPipeline(ResiliencePipelineBuilder<HttpResponseMessage> pipeline) => pipeline
    .AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        Delay = RetryDelay,
        BackoffType = DelayBackoffType.Constant,
        UseJitter = false, // predictable delays for the test; keep jitter on in production
    })
    .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 1.0,     // open when every sampled call failed...
        MinimumThroughput = 3,  // ...once at least 3 calls have been sampled
        BreakDuration = BreakDuration,
    })
    .AddTimeout(AttemptTimeout);

private IHttpBinOrgService CreateService(Action<IHttpClientBuilder> addResilience)
{
    ServiceCollection services = new ServiceCollection();
    services.AddFakeLogging(); // v8 logs resilience events itself - see H_ - so a FakeLogCollector can assert on them
    services.AddTransient<IHttpBinOrgService, HttpBinOrgService>();

    IHttpClientBuilder httpClient = services.AddHttpClient("HttpBinOrg", client =>
    {
        client.BaseAddress = new Uri(_wireMockServer.Url!);
        client.Timeout = Timeout.InfiniteTimeSpan; // let the resilience pipeline own timeouts
    });
    addResilience(httpClient);

    _serviceProvider = services.BuildServiceProvider(true);
    return _serviceProvider.GetRequiredService<IHttpBinOrgService>();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L90-L128' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_CookbookPipeline' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`HttpBinOrgService` is unchanged: v8 doesn't need the `request.AddPollyContext(logger)` call it makes for v7 (it's
simply ignored by the v8 handler), because v8 logs resilience events itself.

### Waiting for the server's log - poll with a deadline, don't sleep

<!-- snippet: WireMockNetResilienceTests_WaitForRequestsAsync -->
<a id='snippet-WireMockNetResilienceTests_WaitForRequestsAsync'></a>
```cs
// WireMock logs a request only once its response has finished, so a request whose response is delayed (or that
// the client gave up on) appears in LogEntries late. Poll for the expected count with a deadline, instead of
// sleeping a fixed amount of time and hoping it was long enough.
private async Task<IReadOnlyList<ILogEntry>> WaitForRequestsAsync(int count)
{
    Stopwatch deadline = Stopwatch.StartNew();
    while (_wireMockServer.LogEntries.Count() < count && deadline.Elapsed < TimeSpan.FromSeconds(10))
    {
        await Task.Delay(50);
    }

    return _wireMockServer.LogEntries.ToList();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L130-L144' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_WaitForRequestsAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A fixed sleep is either too long (a slow suite) or too short (a flaky one). Polling returns as soon as the expected
requests have arrived, and the deadline turns "never arrived" into a clear assertion failure instead of a hang.

### Happy path

<!-- snippet: WireMockNetResilienceTests_A_Resilience_OK -->
<a id='snippet-WireMockNetResilienceTests_A_Resilience_OK'></a>
```cs
public async Task A_Resilience_OK()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));

    // Act
    HttpStatusCode result = await service.GetStatusAsync(HttpStatusCode.OK);

    // Assert
    result.Should().Be(HttpStatusCode.OK);
    _wireMockServer.Should().HaveReceived(1).Calls();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L151-L164' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_A_Resilience_OK' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Retries - how many, and how far apart, from the server's point of view

<!-- snippet: WireMockNetResilienceTests_B_Resilience_Retry_InternalServerError -->
<a id='snippet-WireMockNetResilienceTests_B_Resilience_Retry_InternalServerError'></a>
```cs
public async Task B_Resilience_Retry_InternalServerError()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));

    // Act
    Func<Task> act = () => service.GetStatusAsync(HttpStatusCode.InternalServerError);

    // Assert - 1 attempt + 2 retries, all 500, so the last 500 reaches EnsureSuccessStatusCode()
    await act.Should().ThrowAsync<HttpRequestException>();

    IReadOnlyList<ILogEntry> requests = await WaitForRequestsAsync(3);
    requests.Should().HaveCount(3);

    // WireMock timestamps every request it receives, so the retry delay can be checked server-side
    List<DateTime> receivedAt = requests.Select(entry => entry.RequestMessage!.DateTime).Order().ToList();
    (receivedAt[1] - receivedAt[0]).Should().BeGreaterThanOrEqualTo(RetryDelay - TimeSpan.FromMilliseconds(50));
    (receivedAt[2] - receivedAt[1]).Should().BeGreaterThanOrEqualTo(RetryDelay - TimeSpan.FromMilliseconds(50));
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L171-L191' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_B_Resilience_Retry_InternalServerError' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Timeouts cut off a slow server - and are retried

The server delays its response by 2 seconds; each attempt is cut off at 500ms. The whole call fails in about 1.9
seconds (3 attempts x 500ms + 2 retry delays x 200ms) instead of 3 x 2 seconds. Right after the call, WireMock's log is
still **empty** - its 3 delayed responses are still finishing - which is exactly why `WaitForRequestsAsync` exists.

<!-- snippet: WireMockNetResilienceTests_C_Resilience_Timeout_ThenRetry -->
<a id='snippet-WireMockNetResilienceTests_C_Resilience_Timeout_ThenRetry'></a>
```cs
public async Task C_Resilience_Timeout_ThenRetry()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));
    Stopwatch stopwatch = Stopwatch.StartNew();

    // Act
    Func<Task> act = () => service.GetStatusAsync(HttpStatusCode.RequestTimeout);

    // Assert - every attempt is cut off at 500ms, long before the server's 2-second response
    await act.Should().ThrowAsync<TimeoutRejectedException>();
    stopwatch.Elapsed.Should().BeLessThan(SlowResponseDelay); // 3 x 500ms + 2 x 200ms ~= 1.9s - not 3 x 2s

    // The server still finishes all 3 delayed responses after the client gave up, so they're logged late
    _wireMockServer.LogEntries.Should().BeEmpty();
    (await WaitForRequestsAsync(3)).Should().HaveCount(3);
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L198-L216' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_C_Resilience_Timeout_ThenRetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Intermittent failure, then success - WireMock Scenarios and States

<!-- snippet: WireMockNetResilienceTests_D_Resilience_IntermittentFailureThenSuccess -->
<a id='snippet-WireMockNetResilienceTests_D_Resilience_IntermittentFailureThenSuccess'></a>
```cs
public async Task D_Resilience_IntermittentFailureThenSuccess()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));

    // Act
    HttpStatusCode result = await service.GetStatusAsync(HttpStatusCode.Created);

    // Assert - 500 then 201, from the WireMock scenario. A fresh server per test means the scenario always starts
    // in its initial state, no matter which tests ran before this one.
    result.Should().Be(HttpStatusCode.Created);
    _wireMockServer.Should().HaveReceived(2).Calls();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L223-L237' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_D_Resilience_IntermittentFailureThenSuccess' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

:exclamation: Scenarios and States reference https://github.com/wiremock/WireMock.Net/wiki/Scenarios-and-States  

### Circuit breaker - opens to protect the server, then recovers

While the circuit is open, calls fail immediately with `BrokenCircuitException` and **never reach the server** - which
WireMock can prove, because its request count doesn't move. Then the test changes the stub mid-test
(`ResetMappings()` + a new `Given`) so the "server" recovers, and checks the breaker lets a trial call through and
closes again. Unlike the v7 version (10 overlapping calls started 100ms apart, with an exact expected count of 16), this
is sequential and deterministic.

<!-- snippet: WireMockNetResilienceTests_E_Resilience_CircuitBreaker_OpensThenRecovers -->
<a id='snippet-WireMockNetResilienceTests_E_Resilience_CircuitBreaker_OpensThenRecovers'></a>
```cs
public async Task E_Resilience_CircuitBreaker_OpensThenRecovers()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));

    // Act / Assert - 3 failed attempts (1 + 2 retries) trip the breaker (MinimumThroughput = 3, FailureRatio = 100%)
    Func<Task> firstCall = () => service.GetStatusAsync(HttpStatusCode.InternalServerError);
    await firstCall.Should().ThrowAsync<HttpRequestException>();
    _wireMockServer.Should().HaveReceived(3).Calls();

    // While open, calls fail fast - and never reach the server
    Func<Task> whileOpen = () => service.GetStatusAsync(HttpStatusCode.InternalServerError);
    await whileOpen.Should().ThrowAsync<BrokenCircuitException>();
    _wireMockServer.Should().HaveReceived(3).Calls();

    // The server recovers: WireMock can change a response mid-test
    _wireMockServer.ResetMappings();
    _wireMockServer
        .Given(Request.Create().WithPath(ENDPOINT_STATUS_INTERNALSERVER).UsingGet())
        .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

    // After the break duration the breaker lets a trial call through (half-open); it succeeds, so it closes
    await Task.Delay(BreakDuration + TimeSpan.FromMilliseconds(200));
    HttpStatusCode afterBreak = await service.GetStatusAsync(HttpStatusCode.InternalServerError);

    afterBreak.Should().Be(HttpStatusCode.OK);
    _wireMockServer.Should().HaveReceived(4).Calls();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L244-L273' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_E_Resilience_CircuitBreaker_OpensThenRecovers' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Caller cancellation stops the retries

<!-- snippet: WireMockNetResilienceTests_F_Resilience_CallerCancellation_StopsRetries -->
<a id='snippet-WireMockNetResilienceTests_F_Resilience_CallerCancellation_StopsRetries'></a>
```cs
public async Task F_Resilience_CallerCancellation_StopsRetries()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));
    using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)); // before the 500ms attempt timeout

    // Act
    Func<Task> act = () => service.GetStatusAsync(HttpStatusCode.RequestTimeout, cancellation.Token);

    // Assert - the caller's cancellation is not a transient failure, so it is not retried
    await act.Should().ThrowAsync<OperationCanceledException>();
    (await WaitForRequestsAsync(1)).Should().HaveCount(1);
    _serviceProvider!.GetFakeLogCollector().GetSnapshot()
        .Should().NotContain(record => record.Message.Contains("'OnRetry'", StringComparison.Ordinal));
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L280-L296' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_F_Resilience_CallerCancellation_StopsRetries' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Built-in telemetry replaces the v7 logging plumbing

v7 needed `PollyContextExtensions` to carry a logger through a `Context`, plus a callback per policy - and some v7
callbacks can't log at all (see below). v8 logs every resilience event through the application's own `ILogger`
(category `Polly`), e.g. `Resilience event occurred. EventName: 'OnRetry', Source: 'HttpBinOrg-cookbook//Retry', ...`,
so a [`FakeLogger`](./README_Logging.md) can assert on it directly:

<!-- snippet: WireMockNetResilienceTests_G_Resilience_Telemetry -->
<a id='snippet-WireMockNetResilienceTests_G_Resilience_Telemetry'></a>
```cs
public async Task G_Resilience_Telemetry()
{
    // Arrange
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddResilienceHandler("cookbook", CookbookPipeline));

    // Act
    Func<Task> act = () => service.GetStatusAsync(HttpStatusCode.InternalServerError);
    await act.Should().ThrowAsync<HttpRequestException>();

    // Assert - v8 logs every resilience event through the app's own ILogger (category "Polly"); nothing to wire up
    IReadOnlyList<FakeLogRecord> retryEvents = _serviceProvider!.GetFakeLogCollector().GetSnapshot()
        .Where(record => record.Category == "Polly" && record.Message.Contains("EventName: 'OnRetry'", StringComparison.Ordinal))
        .ToList();

    retryEvents.Should().HaveCount(2);
    retryEvents.Should().AllSatisfy(record => record.Message.Should().Contain("Source: 'HttpBinOrg-cookbook//Retry'"));
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L303-L321' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_G_Resilience_Telemetry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### The standard pipeline

Most apps don't need a hand-built pipeline: `AddStandardResilienceHandler()` adds a rate limiter, a total timeout,
retries, a circuit breaker, and a per-attempt timeout, with production-ready defaults. It's still worth a test that
proves it's actually wired up:

<!-- snippet: WireMockNetResilienceTests_H_StandardResilienceHandler -->
<a id='snippet-WireMockNetResilienceTests_H_StandardResilienceHandler'></a>
```cs
public async Task H_StandardResilienceHandler()
{
    // Arrange - AddStandardResilienceHandler() = rate limiter, total timeout, retry, circuit breaker, attempt timeout,
    // with production-ready defaults. Only the retry delay is shortened here, to keep the test fast.
    IHttpBinOrgService service = CreateService(httpClient => httpClient.AddStandardResilienceHandler(options =>
    {
        options.Retry.Delay = RetryDelay;
        options.Retry.BackoffType = DelayBackoffType.Constant;
        options.Retry.UseJitter = false;
    }));

    // Act
    Func<Task> act = () => service.GetStatusAsync(HttpStatusCode.InternalServerError);

    // Assert - the standard handler retries 3 times by default: 1 attempt + 3 retries
    await act.Should().ThrowAsync<HttpRequestException>();
    (await WaitForRequestsAsync(4)).Should().HaveCount(4);
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetResilienceTests.cs#L328-L347' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetResilienceTests_H_StandardResilienceHandler' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Polly v7 policies (kept for comparison)

The original v7 version. It still runs and passes, and it's a fair picture of how Polly + WireMock tests were commonly
written. Comments in its code explain two v7 quirks and one test-isolation workaround (checked against Polly 7.2.4's
source and by experiment):

- **`onBreak`'s `circuitState`** - not the new state. By v7's (admittedly annoying) design it's the state the breaker
transitioned **from** (`Closed` or `HalfOpen`): Polly's `CircuitStateController` captures `transitionedState = _circuitState`
before setting `Open`, then calls `onBreak(..., transitionedState, ...)`. That's why the log message states `Open` itself.
- **`onHalfOpen`** - a genuine v7 limitation: it's a parameterless `Action`, so there's no context to get a logger from.
v8's `OnHalfOpened` receives the context, and is logged automatically anyway.
- **The `[TearDown]` delay** - lets in-flight responses finish on the shared server before the next test; see
[A fresh WireMock server for every test](#a-fresh-wiremock-server-for-every-test).

### One Time Setup

Using the HttpBin.org API model, we define 3 endpoints.
The RequestTimeout, includes a delay.  

```csharp
private static WireMockServer _wireMockServer;
private IServiceProvider? _serviceProvider;
private string? _baseUrl;

private const string STATEMACHINE_STATUS_FAILED = "Failed";
private const string STATEMACHINE_NAME_FAILED_THEN_SUCCEED = "Fail then Succeed";

const string ENDPOINT_STATUS_STATEMACHINE = "/status/201"; // requires leading slash
const string ENDPOINT_STATUS_OK = "/status/200"; // requires leading slash
const string ENDPOINT_STATUS_INTERNALSERVER = "/status/500"; // requires leading slash
const string ENDPOINT_STATUS_REQUESTTIMEOUT = "/status/408"; // requires leading slash

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
                .WithStatusCode(HttpStatusCode.OK)
        );

    _wireMockServer
        .Given(
            Request.Create()
                .WithPath(ENDPOINT_STATUS_INTERNALSERVER)
                .UsingGet()
        )
        .RespondWith(
            Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
        );

    _wireMockServer
        .Given(
            Request.Create()
                .WithPath(ENDPOINT_STATUS_REQUESTTIMEOUT)
                .UsingGet()
        )
        .RespondWith(
            Response.Create()
                .WithDelay(3000) // simulate (short) timeout
                .WithStatusCode(HttpStatusCode.RequestTimeout)
        );

    // State Machine - 1st call fails, 2nd call (and onward) succeeds
    _wireMockServer
        .Given(
            Request.Create()
                .WithPath(ENDPOINT_STATUS_STATEMACHINE)
                .UsingGet()
        )
        .InScenario(STATEMACHINE_NAME_FAILED_THEN_SUCCEED)
        .WillSetStateTo(STATEMACHINE_STATUS_FAILED)
        .RespondWith(
            Response.Create()
            .WithStatusCode(HttpStatusCode.InternalServerError)
        );

    _wireMockServer
        .Given(
            Request.Create()
                .WithPath(ENDPOINT_STATUS_STATEMACHINE)
                .UsingGet()
        )
        .InScenario(STATEMACHINE_NAME_FAILED_THEN_SUCCEED)
        .WhenStateIs(STATEMACHINE_STATUS_FAILED)
        .RespondWith(
            Response.Create()
            .WithStatusCode(HttpStatusCode.Created)
        );
}
```

:exclamation: Scenarios and States reference https://github.com/wiremock/WireMock.Net/wiki/Scenarios-and-States  

### Setup

Here we define the Polly Policies.  

**NOTICE:**
- Use of the Polly Extension Method `.GetLogger()`.
- The HttpClient Timeout is set to Infinity, allowing Polly to handle timeouts `client.Timeout = Timeout.InfiniteTimeSpan;`.
- The order of the policies 'outermost: waitAndRetryPolicy / innermost: timeoutPolicy'.
- Use of Serilog `.Override()`.

:exclamation: A nice chart of the recommended order of policies https://github.com/App-vNext/Polly/wiki/PolicyWrap  

:exclamation: PolicyWrap reference https://github.com/App-vNext/Polly/wiki/PolicyWrap  

:exclamation: Polly Policy context example https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory#configuring-httpclientfactory-policies-to-use-an-iloggert-from-the-call-site  

<!-- snippet: WireMockNetPollyPoliciesTests_SetUp -->
<a id='snippet-WireMockNetPollyPoliciesTests_SetUp'></a>
```cs
public void SetUp()
{
    //
    // Clear Logs
    //
    _wireMockServer.ResetLogEntries();

    //
    // Polly
    // https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory
    //
    AsyncTimeoutPolicy timeoutPolicy = (AsyncTimeoutPolicy) Policy  // explicit cast required due to WithPolicyKey()
        .TimeoutAsync(
            TimeSpan.FromSeconds(2),
            TimeoutStrategy.Optimistic, // we are co-operative cancellation via CancellationToken
                                        // onTimeout
            (context, timespan, task, exception) =>
            {
                context.GetLogger()?.LogWarning("{PolicyKey} at {OperationKey}: execution timed out after {TimeSpan}.", context.PolicyKey, context.OperationKey, timespan.Humanize());
                return Task.CompletedTask;
            }
        )
        .WithPolicyKey("Cookbook-Timeout-Policy");
    AsyncCircuitBreakerPolicy<HttpResponseMessage> circuitBreakerPolicy = (AsyncCircuitBreakerPolicy<HttpResponseMessage>) HttpPolicyExtensions  // explicit cast required due to WithPolicyKey()
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>() // handle Polly timeouts
        .CircuitBreakerAsync(
            16,
            TimeSpan.FromSeconds(5),
            // onBreak
            (delegateResult, circuitState, timespan, context) =>
            {
                // NOTE: circuitState is NOT the new state - by Polly v7's (admittedly annoying) design it's the state the
                // breaker transitioned FROM (Closed or HalfOpen), so this message states 'Open' itself.
                context.GetLogger()?.LogWarning("{PolicyKey} at {OperationKey}: circuit breaker is in {CircuitState} for {TimeSpan}.", context.PolicyKey, context.OperationKey, "Broken ('Open')", timespan.Humanize());
            },
            // onReset
            (context) =>
            {
                context.GetLogger()?.LogInformation("{PolicyKey} at {OperationKey}: circuit breaker has Reset ('Closed').", context.PolicyKey, context.OperationKey);
            },
            // onHalfOpen
            () =>
            {
                // v7 limitation: onHalfOpen is a parameterless Action - no Context, so no logger to log with.
                // (Polly v8's OnHalfOpened receives the context, and logs automatically - see WireMockNetResilienceTests.)
            }
        )
        .WithPolicyKey("Cookbook-CircuitBreaker-Policy");
    AsyncRetryPolicy<HttpResponseMessage> waitAndRetryPolicy = (AsyncRetryPolicy<HttpResponseMessage>) HttpPolicyExtensions  // explicit cast required due to WithPolicyKey()
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>() // handle Polly timeouts
        .Or<BrokenCircuitException>() // wait and retry for circuit breaker
        .WaitAndRetryAsync(
            2, // retries
            (duration) => TimeSpan.FromSeconds(3), // delay
                                                   // onRetry
            (delegateResult, timespan, context) =>
            {
                context.GetLogger()?.LogInformation("{PolicyKey} at {OperationKey}: execution is waiting for {TimeSpan} before retry.", context.PolicyKey, context.OperationKey, timespan.Humanize());
            }
        ).WithPolicyKey("Cookbook-WaitAndRetry-Policy");

    // https://github.com/App-vNext/Polly/wiki/PolicyWrap
    // outermost: waitAndRetryPolicy / innermost: timeoutPolicy
    AsyncPolicyWrap<HttpResponseMessage> policies = (AsyncPolicyWrap<HttpResponseMessage>) Policy
        .WrapAsync<HttpResponseMessage>(waitAndRetryPolicy, circuitBreakerPolicy, timeoutPolicy.AsAsyncPolicy<HttpResponseMessage>())
        .WithPolicyKey("Cookbook-PolicyWrap");

    //
    // services
    //
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.Mvc", LogEventLevel.Error)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Properties:j}] {Message:lj}{NewLine}{Exception}")
        .Enrich.FromLogContext()
        .CreateLogger();

    IServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger)); // Consume Serilog.ILogger
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>)); // handles all generics
    services.AddTransient<IHttpBinOrgService, HttpBinOrgService>();
    services.AddHttpClient("HttpBinOrg", client =>
    {
        client.BaseAddress = new Uri(_baseUrl!); // setup capture of URLs
        client.Timeout = Timeout.InfiniteTimeSpan; // default was 100 seconds - changed to infinity - using Polly to set Timeout.
    })
    .AddPolicyHandler(policies);

    _serviceProvider = services.BuildServiceProvider(true);
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L138-L233' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_SetUp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### TearDown

<!-- snippet: WireMockNetPollyPoliciesTests_TearDown -->
<a id='snippet-WireMockNetPollyPoliciesTests_TearDown'></a>
```cs
public async Task TearDown()
{
    // WireMock.Net logs a request only once its response has finished. B_ (cancels after 1s) and
    // D_ (times out after 2s) leave 3-second responses in flight on this shared server; without this wait they'd
    // finish - and be logged - after the next test's ResetLogEntries(). WireMockNetResilienceTests avoids the wait
    // entirely with a fresh server per test.
    await Task.Delay(3000);

    (_serviceProvider as IDisposable)?.Dispose();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L236-L247' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_TearDown' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### One Time TearDown

<!-- snippet: WireMockNetPollyPoliciesTests_OneTimeTearDown -->
<a id='snippet-WireMockNetPollyPoliciesTests_OneTimeTearDown'></a>
```cs
[OneTimeTearDown]
public void OneTimeTearDown()
{
    _wireMockServer.Stop();
    _wireMockServer.Dispose();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L128-L135' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_OneTimeTearDown' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Polly Context Extension Method

These Extension Methods help define the Polly Context, which includes...
- Logger, of the current class.
- OperationalKey, of the current class & method.
 
To use, it does require the `.AddPollyContext()` to be added to every `HttpRequestMessage` or every endpoint call.  

<!-- snippet: PollyContextExtensions -->
<a id='snippet-PollyContextExtensions'></a>
```cs
public static class PollyContextExtensions
{
    private static readonly string LoggerKey = "ILogger";

    public static Context WithLogger<T>(this Context context, ILogger logger)
    {
        context[LoggerKey] = logger;
        return context;
    }

    public static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue(LoggerKey, out object logger))
        {
            return logger as ILogger;
        }

        return null;
    }

    public static HttpRequestMessage AddPollyContext<T>(this HttpRequestMessage @this, ILogger<T> logger, [CallerMemberName] string memberName = "")
    {
        Context context = new Context($"{typeof(T).Name}.{memberName}");

        context.WithLogger<T>(logger);

        @this.SetPolicyExecutionContext(context);

        return @this;
    }
}
```
<sup><a href='/UnitTestingCookbook.Support/PollyContextExtensions.cs#L17-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-PollyContextExtensions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Service Endpoint...
<!-- snippet: HttpBinOrgService_GetStatusAsync -->
<a id='snippet-HttpBinOrgService_GetStatusAsync'></a>
```cs
public async Task<HttpStatusCode> GetStatusAsync(HttpStatusCode status, CancellationToken cancellationToken = default)
{
    HttpClient httpClient = httpClientFactory.CreateClient("HttpBinOrg"); // short-lived

    Uri uri = new Uri("status", UriKind.Relative) // endpoint has no leading slash or trailing slash
        .AppendPathSegment((int) status) // flurl
        .ToUri();

    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
    request.AddPollyContext(logger);

    using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

    response.EnsureSuccessStatusCode();

    return response.StatusCode;
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/HttpBinOrgService.cs#L20-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-HttpBinOrgService_GetStatusAsync' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

### HttpStatusCode OK

This is the Happy Path.

<!-- snippet: WireMockNetPollyPoliciesTests_A_WireMockNet_Polly_OK -->
<a id='snippet-WireMockNetPollyPoliciesTests_A_WireMockNet_Polly_OK'></a>
```cs
public async Task A_WireMockNet_Polly_OK()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    HttpStatusCode result = await service.GetStatusAsync(HttpStatusCode.OK);

    // Assert
    result.Should().Be(HttpStatusCode.OK);

    _wireMockServer.Should()
        .HaveReceivedACall()
        .AtUrl($"{_baseUrl}{ENDPOINT_STATUS_OK}");
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L258-L274' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_A_WireMockNet_Polly_OK' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

### CancellationToken

**NOTES:**
- CancellationToken timeout is 1 second.
- Polly timeout is 2 seconds.
- endpoint delay is 3 seconds.


<!-- snippet: WireMockNetPollyPoliciesTests_B_WireMockNet_Polly_CancellationToken -->
<a id='snippet-WireMockNetPollyPoliciesTests_B_WireMockNet_Polly_CancellationToken'></a>
```cs
public async Task B_WireMockNet_Polly_CancellationToken()
{
    // Arrange
    TimeSpan cancelTimeSpan = TimeSpan.FromSeconds(1);
    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(cancelTimeSpan);
    CancellationToken cancellationToken = cancellationTokenSource.Token;

    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    Func<Task> action = () => service.GetStatusAsync(HttpStatusCode.RequestTimeout, cancellationToken);

    // Assert
    await action.Should().ThrowAsync<TaskCanceledException>();
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L290-L306' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_B_WireMockNet_Polly_CancellationToken' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### WaitAndRetry

---

<!-- snippet: WireMockNetPollyPoliciesTests_C_WireMockNet_Polly_InternalServerError -->
<a id='snippet-WireMockNetPollyPoliciesTests_C_WireMockNet_Polly_InternalServerError'></a>
```cs
public async Task C_WireMockNet_Polly_InternalServerError()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    Func<Task> action = () => service.GetStatusAsync(HttpStatusCode.InternalServerError);

    // Assert
    Stopwatch stopwatch = Stopwatch.StartNew();

    // request, fails
    // retry (1) and wait 3 seconds, fails
    // retry (2) and wait 3 seconds, fails
    // total: 3 attempts, 6 seconds 
    // last attempt that fails is not retried
    await action.Should().ThrowAsync<HttpRequestException>();

    _wireMockServer.LogEntries.Should().HaveCount(3); // 3 attempts

    stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(6000); // 6 seconds
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L318-L341' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_C_WireMockNet_Polly_InternalServerError' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

OUTPUT...

```text
[10:58:47 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:58:50 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
```

---

### WaitAndRetry & Timeout

<!-- snippet: WireMockNetPollyPoliciesTests_D_WireMockNet_Polly_WaitAndRetry_Timeout -->
<a id='snippet-WireMockNetPollyPoliciesTests_D_WireMockNet_Polly_WaitAndRetry_Timeout'></a>
```cs
public async Task D_WireMockNet_Polly_WaitAndRetry_Timeout()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    Func<Task> action = () => service.GetStatusAsync(HttpStatusCode.RequestTimeout);

    // Assert
    Stopwatch stopwatch = Stopwatch.StartNew();

    // timeout after 2 seconds
    // retry (1) and wait 3 seconds
    // timeout and after 2 seconds
    // retry (2) and wait 3 seconds
    // timeout after2 seconds
    // total: 3 attempts, 12 seconds
    // last attempt that fails is not retried, it is still a timeout, which Polly will handle and throw TimeoutRejectedException
    await action.Should().ThrowAsync<TimeoutRejectedException>();

    stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(12000); // 12 seconds
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L353-L376' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_D_WireMockNet_Polly_WaitAndRetry_Timeout' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

OUTPUT...

```text
[10:58:58 WRN] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/408", "Scope": ["HTTP GET http://localhost:52615/status/408"]}] Cookbook-Timeout-Policy at HttpBinOrgService.GetStatusAsync: execution timed out after 2 seconds.
[10:58:58 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/408", "Scope": ["HTTP GET http://localhost:52615/status/408"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:03 WRN] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/408", "Scope": ["HTTP GET http://localhost:52615/status/408"]}] Cookbook-Timeout-Policy at HttpBinOrgService.GetStatusAsync: execution timed out after 2 seconds.
[10:59:03 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/408", "Scope": ["HTTP GET http://localhost:52615/status/408"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:08 WRN] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/408", "Scope": ["HTTP GET http://localhost:52615/status/408"]}] Cookbook-Timeout-Policy at HttpBinOrgService.GetStatusAsync: execution timed out after 2 seconds.
```

---

### WaitAndRetry & CircuitBreaker

<!-- snippet: WireMockNetPollyPoliciesTests_E_WireMockNet_Polly_WaitAndRetry_CircuitBreaker -->
<a id='snippet-WireMockNetPollyPoliciesTests_E_WireMockNet_Polly_WaitAndRetry_CircuitBreaker'></a>
```cs
public async Task E_WireMockNet_Polly_WaitAndRetry_CircuitBreaker()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    int exceptions = 0;
    List<Task> tasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
        Task task = new Task(() =>
        {
            try
            {
                service.GetStatusAsync(HttpStatusCode.InternalServerError).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                Interlocked.Increment(ref exceptions);
            }
        });

        tasks.Add(task);
        task.Start();

        await Task.Delay(100);
    }

    // 10 original attempts
    // all 10 fail and are retried (1st time)
    // @ 16th, circuit breaker trips (opens). 6 + 4 retries are shown in log from client logs.
    // 2nd retry - none of the 10 are retried.
    // all 10 requests failed.
    // circuit breaker closes after 5 seconds.
    await Task.WhenAll(tasks);

    // Assert
    exceptions.Should().Be(10); // all 10 requests fail after WaitAndRetry and CircuitBreaker

    // only 16 requests
    // 10 original + 6 of the 1st retry are sent.
    // NOTE: This is NOT client log messages - these are (WireMock) Server request being logged
    _wireMockServer.LogEntries.Should().HaveCount(16);

    // added just for circuit to log closure
    await Task.Delay(6000);

    // will succeed (and show in client log)
    await service.GetStatusAsync(HttpStatusCode.OK);

    // 16 failed attempts + 1 successful
    _wireMockServer.LogEntries.Should().HaveCount(17);
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L388-L442' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_E_WireMockNet_Polly_WaitAndRetry_CircuitBreaker' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

OUTPUT...

```text
[10:59:11 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:12 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:14 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 WRN] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-CircuitBreaker-Policy at HttpBinOrgService.GetStatusAsync: circuit breaker is in Broken ('Open') for 5 seconds.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:15 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:59:24 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/200", "Scope": ["HTTP GET http://localhost:52615/status/200"]}] Cookbook-CircuitBreaker-Policy at HttpBinOrgService.GetStatusAsync: circuit breaker has Reset ('Closed').

```

### Intermittent Failure, then Success

<!-- snippet: WireMockNetPollyPoliciesTests_F_WireMockNet_Polly_HandleIntermittentFailureThenSuccess -->
<a id='snippet-WireMockNetPollyPoliciesTests_F_WireMockNet_Polly_HandleIntermittentFailureThenSuccess'></a>
```cs
public async Task F_WireMockNet_Polly_HandleIntermittentFailureThenSuccess()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    HttpStatusCode result = await service.GetStatusAsync(HttpStatusCode.Created);

    // Assert
    result.Should().Be(HttpStatusCode.Created);

    _wireMockServer.Should()
        .HaveReceivedACall()
        .AtUrl($"{_baseUrl}{ENDPOINT_STATUS_STATEMACHINE}");

    //
    // 1st attempt is InternalServerError
    // 2nd attempt is Created
    //
    _wireMockServer.LogEntries.Should().HaveCount(2);
}
```
<sup><a href='/UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs#L452-L474' title='Snippet source file'>snippet source</a> | <a href='#snippet-WireMockNetPollyPoliciesTests_F_WireMockNet_Polly_HandleIntermittentFailureThenSuccess' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

OUTPUT...

Polly Policy WaitAndRetry is executed once.  

```
[13:55:53 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:49978/status/201", "Scope": ["HTTP GET http://localhost:49978/status/201"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
```

---

Back to [README](../README.md)
