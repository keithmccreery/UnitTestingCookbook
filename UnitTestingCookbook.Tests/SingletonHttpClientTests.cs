using Microsoft.Extensions.DependencyInjection;

using UnitTestingCookbook.Support.Services;
using UnitTestingCookbook.TestHelpers;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[TestFixture]
public class SingletonHttpClientTests
{
    [Test]
    [Category("_passes")]
    // begin-snippet: SingletonHttpClientTests_A_ConfigurePrimaryHttpMessageHandler_RegistersHandlerWithPooledConnectionLifetime
    public void A_ConfigurePrimaryHttpMessageHandler_RegistersHandlerWithPooledConnectionLifetime()
    {
        // Arrange
        TimeSpan expectedPooledConnectionLifetime = TimeSpan.FromMinutes(5);

        IServiceCollection services = new ServiceCollection();
        // begin-snippet: SingletonHttpClientTests_AddHttpClient
        services.AddHttpClient(nameof(SingletonHttpService))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = expectedPooledConnectionLifetime,
            });
        services.AddSingleton<ISingletonHttpService>(serviceProvider =>
            new SingletonHttpService(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SingletonHttpService))));
        // end-snippet

        using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

        // Act
        ISingletonHttpService singletonHttpService = serviceProvider.GetRequiredService<ISingletonHttpService>();

        // Assert
        using (new AssertionScope())
        {
            // IServiceCollection registration - the consumer is registered as a true Singleton
            services.Should()
                .ContainSingle(d => d.ServiceType == typeof(ISingletonHttpService))
                .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);

            // Resolving twice returns the same instance - proves Singleton, not just the descriptor's Lifetime enum
            serviceProvider.GetRequiredService<ISingletonHttpService>().Should().BeSameAs(singletonHttpService);

            // There is no public API to read a configured primary handler back out of a built HttpClient -
            // GetPrimaryHttpMessageHandler<T>() (TestHelpers) walks the private handler chain to find it.
            SocketsHttpHandler socketsHttpHandler = singletonHttpService.HttpClient.GetPrimaryHttpMessageHandler<SocketsHttpHandler>();
            socketsHttpHandler.PooledConnectionLifetime.Should().Be(expectedPooledConnectionLifetime);
        }
    }
    // end-snippet
}
