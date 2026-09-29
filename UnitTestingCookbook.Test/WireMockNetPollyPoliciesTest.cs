using System.Diagnostics;
using System.Net;

using Humanizer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;

using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

using UnitTestingCookbook.Support;
using UnitTestingCookbook.Support.Services;

using WireMock.AwesomeAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace UnitTestingCookbook.Test;

//
// Wiremock.NET https://github.com/WireMock-Net/WireMock.Net
//   Stubbing (responses) https://github.com/WireMock-Net/WireMock.Net/wiki/Stubbing
//   Request matching https://github.com/WireMock-Net/WireMock.Net/wiki/Request-Matching
//     Matchers https://github.com/WireMock-Net/WireMock.Net/wiki/Request-Matchers
//   Response Templating https://github.com/WireMock-Net/WireMock.Net/wiki/Response-Templating
//   Scenarios and States https://github.com/WireMock-Net/WireMock.Net/wiki/Scenarios-and-States
//
[Category("unit")]
[Category("wiremocknet_pollypolicies")]
[TestFixture]
public class WireMockNetPollyPoliciesTest
{
    private static WireMockServer _wireMockServer;
    private string? _baseUrl;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Structure", "NUnit1032:An IDisposable field/property should be Disposed in a TearDown method", Justification = "IServiceProvider is cast to IDisposable")]
    private IServiceProvider? _serviceProvider;

    private const string STATEMACHINE_STATUS_FAILED = "Failed";
    private const string STATEMACHINE_NAME_FAILED_THEN_SUCCEED = "Fail then Succeed";

    private const string ENDPOINT_STATUS_STATEMACHINE = "/status/201"; // requires leading slash
    private const string ENDPOINT_STATUS_OK = "/status/200"; // requires leading slash
    private const string ENDPOINT_STATUS_INTERNALSERVER = "/status/500"; // requires leading slash
    private const string ENDPOINT_STATUS_REQUESTTIMEOUT = "/status/408"; // requires leading slash

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

        // State Machine
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

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _wireMockServer.Stop();
        _wireMockServer.Dispose();
    }

    [SetUp]
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
                    // BUG: circuitState is not set correctly
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
                    // BUG: no context to obtain logger
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

    [TearDown]
    public async Task TearDown()
    {
        await Task.Delay(3000); // BUG in WireMock.Net LogEntries. Need to wait for this call to be logged, to allow .ResetLogEntries() to work

        (_serviceProvider as IDisposable)?.Dispose();
    }

    //
    // Q: How do I verify Polly Policies - OK
    //
    // Timeout: pass through (success)
    // CircuitBreaker: pass through (success)
    // WaitAndRetry: pass through (success)
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I verify Polly Policies - CancellationToken
    //
    // This test will bypass/ignore all policies since CancellationTokenSource timed out and Polly will honor...
    // Timeout: n/a
    // CircuitBreaker: n/a
    // WaitAndRetry: n/a
    //
    // CancellationToken timeout is 1 second
    // Polly timeout is 2 seconds
    // endpoint delay is 3 seconds
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I verify Polly Policies - WaitAndRetry
    //
    // This test will...
    // Timeout: pass through (returned within timeout)
    // CircuitBreaker: pass through (not enough to trigger)
    // WaitAndRetry: triggered
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I verify Polly Policies WaitAndRetry & Timeout
    //
    // This test will...
    // Timeout: triggered
    // CircuitBreaker: pass through (not enough to trigger)
    // WaitAndRetry: triggered
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I verify Polly Policies WaitAndRetry & CircuitBreaker
    //
    // This test will...
    // Timeout: bypassed
    // CircuitBreaker: triggered - 5 second delay
    // WaitAndRetry: triggered - 3 second delay
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I verify Polly Policies with an Intermittent Failure, then Succcess?
    //
    // First attempt, InternalServerError (500)
    // Second attempt, Created (201)
    //
    [Test]
    [Category("_passes")]
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
}
