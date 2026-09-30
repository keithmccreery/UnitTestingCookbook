namespace UnitTestingCookbook.Support.Services;

/// <summary>
/// A Singleton-lifetime consumer of a single, app-lifetime HttpClient - exists to demonstrate
/// SocketsHttpHandler.PooledConnectionLifetime, not real request behavior. Exposes HttpClient
/// so a test can inspect the handler chain it was actually built with.
/// </summary>
public class SingletonHttpService : ISingletonHttpService
{
    public HttpClient HttpClient { get; }

    public SingletonHttpService(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }
}
