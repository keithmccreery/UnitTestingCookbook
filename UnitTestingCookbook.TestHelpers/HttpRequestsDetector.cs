using System.Diagnostics;
using System.Reflection;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Detects real (non-mocked) outbound HTTP requests via DiagnosticListener and throws if the target host isn't
/// in <see cref="AllowedHosts"/>. Adapted from
/// https://www.meziantou.net/prevent-http-requests-to-external-services-in-unit-tests.htm - that article applies
/// this globally via a static class + [ModuleInitializer] (so it protects a whole test assembly automatically,
/// the moment the assembly loads). This version is instance-based with an explicit Subscribe()/Dispose()
/// instead, so a single chapter's test fixture can demonstrate it without silently instrumenting every other
/// chapter sharing this test assembly - several of which (WireMock.Net, Polly Policies) deliberately make real
/// HTTP calls to localhost. Only catches requests that actually reach a real SocketsHttpHandler/HttpClientHandler
/// transport - a mocked HttpMessageHandler (Moq, etc.) never raises this diagnostic event.
/// </summary>
public sealed class HttpRequestsDetector : IDisposable
{
    /// <summary>
    /// The hostnames real outbound HTTP requests are allowed to reach while this detector is
    /// <see cref="Subscribe"/>d. Defaults to just <c>"localhost"</c>. Matched case-insensitively against each
    /// request's URI host - add to this set (before subscribing, to avoid a race with in-flight requests)
    /// for any other host a test legitimately needs to call.
    /// </summary>
    public HashSet<string> AllowedHosts { get; } = new(StringComparer.OrdinalIgnoreCase) { "localhost" };

    private readonly DiagnosticSourceSubscriber subscriber;

    public HttpRequestsDetector()
    {
        subscriber = new DiagnosticSourceSubscriber(AllowedHosts);
    }

    /// <summary>
    /// Starts intercepting real outbound HTTP requests - call once, typically in <c>[SetUp]</c>/<c>[OneTimeSetUp]</c>.
    /// </summary>
    public void Subscribe() => subscriber.Subscribe();

    /// <summary>
    /// Stops intercepting real outbound HTTP requests, restoring normal behavior.
    /// </summary>
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
            // Request property has to be reached via reflection. Verified empirically against net10.0's
            // System.Net.Http before relying on this type name here.
            private static Func<object, HttpRequestMessage?> CreateGetRequestPropertyValue()
            {
                Type requestDataType = Type.GetType("System.Net.Http.DiagnosticsHandler+RequestData, System.Net.Http", throwOnError: true)!;
                PropertyInfo requestProperty = requestDataType.GetProperty("Request")!;
                return o => (HttpRequestMessage?) requestProperty.GetValue(o);
            }
        }
    }
}
