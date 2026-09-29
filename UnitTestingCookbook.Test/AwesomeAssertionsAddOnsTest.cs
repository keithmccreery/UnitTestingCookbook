using System.Net;
using System.Reflection;

using AwesomeAssertions.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Test;

[Category( "unit" )]
[Category( "awesomeassertions_addons" )]
[TestFixture]
public class AwesomeAssertionsAddOnsTest
{
    //
    // AwesomeAssertions.Json
    //
    [Test]
    [Category( "_passes" )]
    public void A_Json()
    {
        // Arrange
        const string json =
@"
{
    ""name"": ""John"",
    ""age"": 22,
    ""gender"": ""male"",
}
";
        JToken jToken = JToken.Parse( json );

        // Act

        // Assert
        using ( new AssertionScope() )
        {
            jToken.Should().HaveCount( 3 );
            jToken.Should().HaveElement( "name" );
            jToken.Should().NotHaveElement( "bozo" );
        }
    }

    //
    // AwesomeAssertions.Web
    //
    [Test]
    [Category( "_passes" )]
    public void B_Web()
    {
        // Arrange
        const string json =
@"
{
    ""name"": ""John"",
    ""age"": 22,
    ""gender"": ""male"",
}
";
        HttpResponseMessage response = new HttpResponseMessage( HttpStatusCode.OK );
        response.Headers.Add( "X-Correlation-ID", Guid.NewGuid().ToString() );
        response.Content = new StringContent( json );

        // Act

        // Assert
        using ( new AssertionScope() )
        {
            response.Should().Be200Ok()
                .And.BeAs( new
                {
                    name = "John",
                    age = 22,
                    gender = "male",
                } );
            response.Should().HaveHeader( "X-Correlation-ID" ).And.NotBeEmpty();
        }
    }

    //
    // DI ServiceCollection assertions
    //
    // NOTE: There is no AwesomeAssertions equivalent of FluentAssertions.Microsoft.Extensions.DependencyInjection
    // (its fluent .Should().HaveService<T>().WithImplementation<T>().AsSingleton() API), so this asserts directly
    // against IServiceCollection / ServiceDescriptor using AwesomeAssertions' core collection assertions.
    //
    [Test]
    [Category( "_passes" )]
    public void X_DependencyInjection()
    {
        // Arrange
        IServiceCollection? serviceCollection = null;
        IHost host = Host.CreateDefaultBuilder()
            .UseConsoleLifetime()
            .UseContentRoot(
                Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) )
            .ConfigureServices( ( hostContext, services ) =>
            {
                services.AddSingleton<IOptions<Animal>>( Options.Create<Animal>( new Animal() ) );
                serviceCollection = services; // HACK: To get IServiceCollection
            } )
            .Build();

        // Act

        // Assert
        using ( new AssertionScope() )
        {
            // NOTE: this count is tied to exactly what Microsoft.Extensions.Hosting registers by default
            // for this package version - it will drift on future framework/package upgrades.
            serviceCollection.Should().HaveCount( 52 );

            // With Implementation
            serviceCollection!.Should()
                .ContainSingle( d => d.ServiceType == typeof( IHostApplicationLifetime ) )
                .Which.Should().Match<ServiceDescriptor>( d =>
                    d.ImplementationType == typeof( ApplicationLifetime )
                    && d.Lifetime == ServiceLifetime.Singleton );

            // Instance only - No Implementation
            serviceCollection.Should()
                .ContainSingle( d => d.ServiceType == typeof( HostBuilderContext ) )
                .Which.Lifetime.Should().Be( ServiceLifetime.Singleton );

            // Factory - No Implementation
            serviceCollection.Should()
                .ContainSingle( d => d.ServiceType == typeof( IHost ) )
                .Which.Lifetime.Should().Be( ServiceLifetime.Singleton );

            // Multiple Implementations
            // NOTE: EventLogLoggerProvider is only registered by the Generic Host on Windows,
            // so it's intentionally excluded here to keep this test cross-platform.
            IEnumerable<ServiceDescriptor> loggerProviderDescriptors =
                serviceCollection.Where( d => d.ServiceType == typeof( ILoggerProvider ) );

            loggerProviderDescriptors.Should().HaveCount( 3 )
                .And.OnlyContain( d => d.Lifetime == ServiceLifetime.Singleton );
            loggerProviderDescriptors.Select( d => d.ImplementationType ).Should().BeEquivalentTo( new[]
            {
                typeof( ConsoleLoggerProvider ),
                typeof( DebugLoggerProvider ),
                typeof( EventSourceLoggerProvider ),
            } );

            // Generic - No Implementation
            serviceCollection.Should()
                .ContainSingle( d => d.ServiceType == typeof( IOptions<Animal> ) )
                .Which.Lifetime.Should().Be( ServiceLifetime.Singleton );
        }
    }
}
