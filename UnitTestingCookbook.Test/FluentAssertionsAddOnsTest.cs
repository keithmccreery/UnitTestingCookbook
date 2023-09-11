using System.Net;
using System.Reflection;

using FluentAssertions.Json;
using FluentAssertions.Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Test;

[Category( "unit" )]
[Category( "fluentassertions_addons" )]
[TestFixture]
public class FluentAssertionsAddOnsTest
{
    //
    // FluentAssertions.Json 
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
    // FluentAssertions.Web
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
    // FluentAssertions.Microsoft.Extensions.DependencyInjection
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
            serviceCollection.Should().HaveCount( 44 );

            // With Implementation
            serviceCollection.Should()
                .HaveService<IHostApplicationLifetime>()
                .WithImplementation<ApplicationLifetime>()
                .AsSingleton();

            // Instance only - No Implementation
            serviceCollection.Should()
                .HaveService<HostBuilderContext>()
                .AsSingleton();

            // Factory - No Implementation
            serviceCollection.Should()
                .HaveService<IHost>()
                .AsSingleton();

            // Multiple Implementations
            serviceCollection.Should()
                .HaveService<ILoggerProvider>()
                .WithCount( 4 )
                .WithImplementation<ConsoleLoggerProvider>()
                .WithImplementation<DebugLoggerProvider>()
                .WithImplementation<EventLogLoggerProvider>()
                .WithImplementation<EventSourceLoggerProvider>()
                .AsSingleton();

            // Generic - No Implementation
            serviceCollection.Should()
                .HaveService<IOptions<Animal>>()
                .AsSingleton();
        }
    }
}
