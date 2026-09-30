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
    public void A_IConfiguration()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Development", })
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("{ \"key\": \"value\" }")))
            .Build();

        // Assert

        // Act
        configuration["Logging:LogLevel:Default"].Should().Be("Debug");
        configuration["key"].Should().Be("value");
    }

    //
    // Q: How do I create a new ‘scope’?
    // NOTE: serviceProvider.CreateScope(); can also be used to create a new scope
    //
    [Test]
    [Category("_passes")]
    public void B_Scope()
    {
        // Arrange
        ServiceCollection services = new ServiceCollection();
        services.AddScoped<IScopedService, SampleScopedService>();

        ServiceProvider serviceProvider = services.BuildServiceProvider(true);

        // Assert
        IServiceScopeFactory? serviceScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        IScopedService scoped1;
        IScopedService scoped2;

        using (var scope = serviceScopeFactory.CreateScope())
        {
            scoped1 = scope.ServiceProvider.GetRequiredService<IScopedService>();
        }

        using (var scope = serviceScopeFactory.CreateScope())
        {
            scoped2 = scope.ServiceProvider.GetRequiredService<IScopedService>();
        }

        // Act
        scoped1.Should().NotBeSameAs(scoped2); // same as ReferenceEquals( scoped1, scoped2 ).Should().BeFalse()
    }

    //
    // Q: How do I setup IOptions<T> for testing?
    //
    [Test]
    [Category("_passes")]
    public void C_IOptions()
    {
        // Arrange
        IOptions<MemoryDistributedCacheOptions> options = Options.Create<MemoryDistributedCacheOptions>(new MemoryDistributedCacheOptions());

        // Assert

        // Act
        options.Should().NotBeNull();
        options.Value.Should().BeOfType<MemoryDistributedCacheOptions>()
            .Which.SizeLimit.Should().Be(200 * 1024 * 1024);
    }
}
