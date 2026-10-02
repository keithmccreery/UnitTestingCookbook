using System;
using System.Text;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("dependencyinjection")]
[TestFixture]
public class DependencyInjectionTests
{
    //
    // Q: How do I setup IConfiguration for testing?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: DependencyInjectionTests_A_IConfiguration
    public void A_IConfiguration()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Development", })
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("{ \"key\": \"value\" }")))
            .Build();

        // Act / Assert
        configuration["Logging:LogLevel:Default"].Should().Be("Debug");
        configuration["key"].Should().Be("value");
    }
    // end-snippet

    //
    // Q: How do I create a new ‘scope’?
    // NOTE: serviceProvider.CreateScope(); can also be used to create a new scope
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: DependencyInjectionTests_B_Scope
    public void B_Scope()
    {
        // Arrange
        ServiceCollection services = new ServiceCollection();
        services.AddScoped<IScopedService, SampleScopedService>();

        using ServiceProvider serviceProvider = services.BuildServiceProvider(true); // true = validateScopes

        IServiceScopeFactory serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // Act
        // Both scopes stay open until the end of the test, so every instance is still live (not yet disposed by
        // its scope) when it's asserted on - matters if the service were IDisposable.
        using IServiceScope scope1 = serviceScopeFactory.CreateScope();
        using IServiceScope scope2 = serviceScopeFactory.CreateScope();

        IScopedService scope1First = scope1.ServiceProvider.GetRequiredService<IScopedService>();
        IScopedService scope1Second = scope1.ServiceProvider.GetRequiredService<IScopedService>();
        IScopedService scope2First = scope2.ServiceProvider.GetRequiredService<IScopedService>();

        // Assert
        using (new AssertionScope())
        {
            scope1First.Should().BeSameAs(scope1Second); // same scope -> same instance (would fail if registered Transient)
            scope1First.Should().NotBeSameAs(scope2First); // different scope -> new instance (would fail if registered Singleton)
        }
    }
    // end-snippet

    //
    // Q: How do I setup IOptions<T> for testing?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: DependencyInjectionTests_C_IOptions
    public void C_IOptions()
    {
        // Arrange
        IOptions<MemoryDistributedCacheOptions> options = Options.Create<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions());

        // Act / Assert
        options.Should().NotBeNull();
        options.Value.Should().BeOfType<MemoryDistributedCacheOptions>()
            .Which.SizeLimit.Should().Be(200 * 1024 * 1024);
    }
    // end-snippet
}
