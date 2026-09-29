# Polly Policies

## NuGet Packages Referenced

- Polly https://github.com/App-vNext/Polly
- WireMock.Net https://github.com/wiremock/WireMock.Net
- WireMock.Net.AwesomeAssertions https://github.com/wiremock/WireMock.Net/tree/master/src/WireMock.Net.AwesomeAssertions
- Serilog.Extensions.Logging https://github.com/serilog/serilog-extensions-logging
- Humanizer https://github.com/Humanizr/Humanizer

## Web Sites Referenced

- HttpBin.org https://httpbin.org

All examples are located in `UnitTestingCookbook.Test` -> [`WireMockNetPollyPoliciesTest`](../UnitTestingCookbook.Test/WireMockNetPollyPoliciesTest.cs)  

---

## How do I verify Polly Policies?

How many people actually test their Polly Policies? Here is how you can with WireMock.Net.  

**Fun Fact:** I have worked on code where
(1) The Polly Policies were not defined in the correct order;
(2) produced `NullReferenceExceptions` in the functions;
and/or (3) failed to specify the proper Exceptions to handle.  

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
    _baseUrl = _wireMockServer.Urls[ 0 ];

    // status endpoint
    _wireMockServer
        .Given(
            Request.Create()
                .WithPath( ENDPOINT_STATUS_OK )
                .UsingGet()
        )
        .RespondWith(
            Response.Create()
                .WithStatusCode( HttpStatusCode.OK )
        );

    _wireMockServer
        .Given(
            Request.Create()
                .WithPath( ENDPOINT_STATUS_INTERNALSERVER )
                .UsingGet()
        )
        .RespondWith(
            Response.Create()
                .WithStatusCode( HttpStatusCode.InternalServerError )
        );

    _wireMockServer
        .Given(
            Request.Create()
                .WithPath( ENDPOINT_STATUS_REQUESTTIMEOUT )
                .UsingGet()
        )
        .RespondWith(
            Response.Create()
                .WithDelay( 3000 ) // simulate (short) timeout
                .WithStatusCode( HttpStatusCode.RequestTimeout )
        );

    // State Machine - 1st call fails, 2nd call (and onward) succeeds
    _wireMockServer
        .Given(
            Request.Create()
                .WithPath( ENDPOINT_STATUS_STATEMACHINE )
                .UsingGet()
        )
        .InScenario( STATEMACHINE_NAME_FAILED_THEN_SUCCEED )
        .WillSetStateTo( STATEMACHINE_STATUS_FAILED )
        .RespondWith(
            Response.Create()
            .WithStatusCode( HttpStatusCode.InternalServerError )
        );

    _wireMockServer
        .Given(
            Request.Create()
                .WithPath( ENDPOINT_STATUS_STATEMACHINE )
                .UsingGet()
        )
        .InScenario( STATEMACHINE_NAME_FAILED_THEN_SUCCEED )
        .WhenStateIs( STATEMACHINE_STATUS_FAILED )
        .RespondWith(
            Response.Create()
            .WithStatusCode( HttpStatusCode.Created )
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
- Polly does contain a few bugs (in 6.0.x).
- Use of Serilog `.Override()`.

:exclamation: A nice chart of the recommended order of policies https://github.com/App-vNext/Polly/wiki/PolicyWrap  

:exclamation: PolicyWrap reference https://github.com/App-vNext/Polly/wiki/PolicyWrap  

:exclamation: Polly Policy context example https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory#configuring-httpclientfactory-policies-to-use-an-iloggert-from-the-call-site  

```csharp
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
    AsyncTimeoutPolicy timeoutPolicy = ( AsyncTimeoutPolicy ) Policy  // explicit cast required due to WithPolicyKey()
        .TimeoutAsync(
            TimeSpan.FromSeconds( 2 ),
            TimeoutStrategy.Optimistic, // we are co-operative cancellation via CancellationToken
            // onTimeout
            ( context, timespan, task, exception ) =>
            {
                context.GetLogger()?.LogWarning( "{PolicyKey} at {OperationKey}: execution timed out after {TimeSpan}.", context.PolicyKey, context.OperationKey, timespan.Humanize() );
                return Task.CompletedTask;
            }
        )
        .WithPolicyKey( "Cookbook-Timeout-Policy" );
    AsyncCircuitBreakerPolicy<HttpResponseMessage> circuitBreakerPolicy = ( AsyncCircuitBreakerPolicy<HttpResponseMessage> ) HttpPolicyExtensions  // explicit cast required due to WithPolicyKey()
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>() // handle Polly timeouts
        .CircuitBreakerAsync(
            16,
            TimeSpan.FromSeconds( 5 ),
            // onBreak
            ( delegateResult, circuitState, timespan, context ) =>
            {
                // BUG: circuitState is not set correctly
                context.GetLogger()?.LogWarning( "{PolicyKey} at {OperationKey}: circuit breaker is in {CircuitState} for {TimeSpan}.", context.PolicyKey, context.OperationKey, "Broken ('Open')", timespan.Humanize() );
            },
            // onReset
            ( context ) =>
            {
                context.GetLogger()?.LogInformation( "{PolicyKey} at {OperationKey}: circuit breaker has Reset ('Closed').", context.PolicyKey, context.OperationKey );
            },
            // onHalfOpen
            () =>
            {
                // BUG: no context to obtain logger
            }
        )
        .WithPolicyKey( "Cookbook-CircuitBreaker-Policy" );
    AsyncRetryPolicy<HttpResponseMessage> waitAndRetryPolicy = ( AsyncRetryPolicy<HttpResponseMessage> ) HttpPolicyExtensions  // explicit cast required due to WithPolicyKey()
        .HandleTransientHttpError()
        .Or<TimeoutRejectedException>() // handle Polly timeouts
        .Or<BrokenCircuitException>() // wait and retry for circuit breaker
        .WaitAndRetryAsync(
            2, // retries
            ( duration ) => TimeSpan.FromSeconds( 3 ), // delay
            // onRetry
            ( delegateResult, timespan, context ) =>
            {
                context.GetLogger()?.LogInformation( "{PolicyKey} at {OperationKey}: execution is waiting for {TimeSpan} before retry.", context.PolicyKey, context.OperationKey, timespan.Humanize() );
            }
        ).WithPolicyKey( "Cookbook-WaitAndRetry-Policy" );

    // https://github.com/App-vNext/Polly/wiki/PolicyWrap
    // outermost: waitAndRetryPolicy / innermost: timeoutPolicy
    AsyncPolicyWrap<HttpResponseMessage> policies = ( AsyncPolicyWrap<HttpResponseMessage> ) Policy
        .WrapAsync<HttpResponseMessage>( waitAndRetryPolicy, circuitBreakerPolicy, timeoutPolicy.AsAsyncPolicy<HttpResponseMessage>() )
        .WithPolicyKey( "Cookbook-PolicyWrap" );

    //
    // services
    //
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override( "System", LogEventLevel.Warning )
        .MinimumLevel.Override( "Microsoft", LogEventLevel.Warning )
        .MinimumLevel.Override( "Microsoft.AspNetCore.Mvc", LogEventLevel.Error )
        .WriteTo.Console( outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Properties:j}] {Message:lj}{NewLine}{Exception}" )
        .Enrich.FromLogContext()
        .CreateLogger();

    IServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>( new SerilogLoggerFactory( serilogLogger ) ); // Consume Serilog.ILogger
    services.AddSingleton( typeof( ILogger<> ), typeof( Logger<> ) ); // handles all generics
    services.AddTransient<IHttpBinOrgService, HttpBinOrgService>();
    services.AddHttpClient( "HttpBinOrg", client =>
    {
        client.BaseAddress = new Uri( _baseUrl! ); // setup capture of URLs
        client.Timeout = Timeout.InfiniteTimeSpan; // default was 100 seconds - changed to infinity - using Polly to set Timeout.
    } )
    .AddPolicyHandler( policies );

    _serviceProvider = services.BuildServiceProvider( true );
}
```

### TearDown

```csharp
public async Task TearDown()
{
    await Task.Delay( 3000 ); // BUG in WireMock.Net LogEntries. Need to wait for this call to be logged, to allow .ResetLogEntries() to work

    ( _serviceProvider as IDisposable )?.Dispose();
}
```

### One Time TearDown

```csharp
[OneTimeTearDown]
public void OneTimeTearDown()
{
    _wireMockServer.Stop();
    _wireMockServer.Dispose();
}
```

### Polly Context Extension Method

These Extension Methods help define the Polly Context, which includes...
- Logger, of the current class.
- OperationalKey, of the current class & method.
 
To use, it does require the `.AddPollyContext()` to be added to every `HttpRequestMessage` or every endpoint call.  

```csharp
public static class PollyContextExtensions
{
    private static readonly string LoggerKey = "ILogger";

    public static Context WithLogger<T>( this Context context, ILogger logger )
    {
        context[ LoggerKey ] = logger;
        return context;
    }

    public static ILogger? GetLogger( this Context context )
    {
        if ( context.TryGetValue( LoggerKey, out object logger ) )
        {
            return logger as ILogger;
        }

        return null;
    }

    public static HttpRequestMessage AddPollyContext<T>( this HttpRequestMessage @this, ILogger<T> logger, [CallerMemberName] string memberName = "" )
    {
        Context context = new Context( $"{typeof( T ).Name}.{memberName}" );

        context.WithLogger<T>( logger );

        @this.SetPolicyExecutionContext( context );

        return @this;
    }
}
```

Service Endpoint...
```csharp
public async Task<HttpStatusCode> GetStatusAsync( HttpStatusCode status, CancellationToken cancellationToken = default )
{
    HttpClient httpClient = httpClientFactory.CreateClient( "HttpBinOrg" ); // short-lived

    Uri uri = new Uri( "status", UriKind.Relative ) // endpoint has no leading slash or trailing slash
        .AppendPathSegment( ( int ) status ) // flurl
        .ToUri();

    using HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Get, uri );
    request.AddPollyContext( logger );

    using HttpResponseMessage response = await httpClient.SendAsync( request, cancellationToken );

    response.EnsureSuccessStatusCode();

    return response.StatusCode;
}
```

---

### HttpStatusCode OK

This is the Happy Path.

```csharp
public async Task A_WireMockNet_Polly_OK()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    HttpStatusCode result = await service.GetStatusAsync( HttpStatusCode.OK );

    // Assert
    result.Should().Be( HttpStatusCode.OK );

    _wireMockServer.Should()
        .HaveReceivedACall()
        .AtUrl( $"{_baseUrl}{ENDPOINT_STATUS_OK}" );
}
```

---

### CancellationToken

**NOTES:**
- CancellationToken timeout is 1 second.
- Polly timeout is 2 seconds.
- endpoint delay is 3 seconds.


```csharp
public async Task B_WireMockNet_Polly_CancellationToken()
{
    // Arrange
    TimeSpan cancelTimeSpan = TimeSpan.FromSeconds( 1 );
    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource( cancelTimeSpan );
    CancellationToken cancellationToken = cancellationTokenSource.Token;

    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    Func<Task> action = () => service.GetStatusAsync( HttpStatusCode.RequestTimeout, cancellationToken );

    // Assert
    await action.Should().ThrowAsync<TaskCanceledException>();
}
```

### WaitAndRetry

---

```csharp
public async Task C_WireMockNet_Polly_InternalServerError()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    Func<Task> action = () => service.GetStatusAsync( HttpStatusCode.InternalServerError );

    // Assert
    Stopwatch stopwatch = Stopwatch.StartNew();

    // request, fails
    // retry (1) and wait 3 seconds, fails
    // retry (2) and wait 3 seconds, fails
    // total: 3 attempts, 6 seconds 
    // last attempt that fails is not retried
    await action.Should().ThrowAsync<HttpRequestException>();

    _wireMockServer.LogEntries.Should().HaveCount( 3 ); // 3 attempts

    stopwatch.ElapsedMilliseconds.Should().BeGreaterThan( 6000 ); // 6 seconds
}
```

OUTPUT...

```text
[10:58:47 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
[10:58:50 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:52615/status/500", "Scope": ["HTTP GET http://localhost:52615/status/500"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
```

---

### WaitAndRetry & Timeout

```csharp
public async Task D_WireMockNet_Polly_WaitAndRetry_Timeout()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    Func<Task> action = () => service.GetStatusAsync( HttpStatusCode.RequestTimeout );

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

    stopwatch.ElapsedMilliseconds.Should().BeGreaterThan( 12000 ); // 12 seconds
}
```

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

```csharp
public async Task E_WireMockNet_Polly_WaitAndRetry_CircuitBreaker()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    int exceptions = 0;
    List<Task> tasks = new List<Task>();
    for ( int i = 0; i < 10; i++ )
    {
        Task task = new Task( () =>
        {
            try
            {
                service.GetStatusAsync( HttpStatusCode.InternalServerError ).GetAwaiter().GetResult();
            }
            catch ( Exception )
            {
                Interlocked.Increment( ref exceptions );
            }
        } );

        tasks.Add( task );
        task.Start();

        await Task.Delay( 100 );
    }

    // 10 original attempts
    // all 10 fail and are retried (1st time)
    // @ 16th, circuit breaker trips (opens). 6 + 4 retries are shown in log from client logs.
    // 2nd retry - none of the 10 are retried.
    // all 10 requests failed.
    // circuit breaker closes after 5 seconds.
    await Task.WhenAll( tasks );

    // Assert
    exceptions.Should().Be( 10 ); // all 10 requests fail after WaitAndRetry and CircuitBreaker

    // only 16 requests
    // 10 original + 6 of the 1st retry are sent.
    // NOTE: This is NOT client log messages - these are (WireMock) Server request being logged
    _wireMockServer.LogEntries.Should().HaveCount( 16 );

    // added just for circuit to log closure
    await Task.Delay( 6000 );

    // will succeed (and show in client log)
    await service.GetStatusAsync( HttpStatusCode.OK );

    // 16 failed attempts + 1 successful
    _wireMockServer.LogEntries.Should().HaveCount( 17 );
}
```

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

```csharp
public async Task F_WireMockNet_Polly_HandleIntermittentFailureThenSuccess()
{
    // Arrange
    IHttpBinOrgService service = _serviceProvider!.GetRequiredService<IHttpBinOrgService>();

    // Act
    HttpStatusCode result = await service.GetStatusAsync( HttpStatusCode.Created );

    // Assert
    result.Should().Be( HttpStatusCode.Created );

    _wireMockServer.Should()
        .HaveReceivedACall()
        .AtUrl( $"{_baseUrl}{ENDPOINT_STATUS_STATEMACHINE}" );

    //
    // 1st attempt is InternalServerError
    // 2nd attempt is Created
    //
    _wireMockServer.LogEntries.Should().HaveCount( 2 );
}
```

OUTPUT...

Polly Policy WaitAndRetry is executed once.  

```
[13:55:53 INF] [{"SourceContext": "UnitTestingCookbook.Support.Services.HttpBinOrgService", "HttpMethod": "GET", "Uri": "http://localhost:49978/status/201", "Scope": ["HTTP GET http://localhost:49978/status/201"]}] Cookbook-WaitAndRetry-Policy at HttpBinOrgService.GetStatusAsync: execution is waiting for 3 seconds before retry.
```

---

Back to [README](../README.md)
