using System.Diagnostics;
using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Testing;

using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

using UnitTestingCookbook.Support.Services;

using WireMock.AwesomeAssertions;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace UnitTestingCookbook.Tests;

//
// WireMock.Net + Polly v8 resilience pipelines (Microsoft.Extensions.Http.Resilience).
// The same scenarios as WireMockNetPollyPoliciesTests (Polly v7 policies), rewritten - see README_WireMockNetPollyPolicies.md.
//
[Category("unit")]
[Category("wiremocknet_resilience")]
[TestFixture]
public class WireMockNetResilienceTests
{
    // Milliseconds, not seconds: the ratios are what matter (a timeout shorter than the server's delay, a retry delay
    // short enough to keep the suite fast), and the whole fixture runs in a few seconds.
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SlowResponseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(1);

    private const string ENDPOINT_STATUS_OK = "/status/200";
    private const string ENDPOINT_STATUS_STATEMACHINE = "/status/201";
    private const string ENDPOINT_STATUS_INTERNALSERVER = "/status/500";
    private const string ENDPOINT_STATUS_REQUESTTIMEOUT = "/status/408";

    private WireMockServer _wireMockServer = null!;
    private ServiceProvider? _serviceProvider;

    // begin-snippet: WireMockNetResilienceTests_SetUp
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
    // end-snippet

    // begin-snippet: WireMockNetResilienceTests_CookbookPipeline
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
    // end-snippet

    // begin-snippet: WireMockNetResilienceTests_WaitForRequestsAsync
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
    // end-snippet

    //
    // Q: How do I verify the happy path passes straight through the pipeline?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_A_Resilience_OK
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
    // end-snippet

    //
    // Q: How do I verify retries - how many, and how far apart - from the server's point of view?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_B_Resilience_Retry_InternalServerError
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
    // end-snippet

    //
    // Q: How do I verify a timeout cuts off a slow server - and is retried?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_C_Resilience_Timeout_ThenRetry
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
    // end-snippet

    //
    // Q: How do I verify an intermittent failure (fail, then succeed) is retried successfully?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_D_Resilience_IntermittentFailureThenSuccess
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
    // end-snippet

    //
    // Q: How do I verify a circuit breaker opens - protecting the server - and then recovers?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_E_Resilience_CircuitBreaker_OpensThenRecovers
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
    // end-snippet

    //
    // Q: When the caller cancels, does the pipeline stop - or keep retrying?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_F_Resilience_CallerCancellation_StopsRetries
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
    // end-snippet

    //
    // Q: Where did the v7 logging plumbing (PollyContextExtensions, Context.GetLogger()) go?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_G_Resilience_Telemetry
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
    // end-snippet

    //
    // Q: How do I test the recommended "standard" pipeline instead of hand-building one?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetResilienceTests_H_StandardResilienceHandler
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
    // end-snippet
}
