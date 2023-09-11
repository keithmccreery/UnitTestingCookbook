using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTestingCookbook.Support;

public class SampleWithLoggingFactory
{
    private readonly ILogger<SampleWithLoggingFactory> logger;

    public SampleWithLoggingFactory() : this( NullLoggerFactory.Instance )
    {
    }

    //
    // NOTE: The constructor with the most parameters where the types are DI-resolvable is selected.
    //
    public SampleWithLoggingFactory( ILoggerFactory loggerFactory )
    {
        logger = loggerFactory.CreateLogger<SampleWithLoggingFactory>();
    }

    public void LogInformationMessage( string? message )
    {
        logger.LogInformation( "This is the template with a {Message}.", message );

        System.Console.WriteLine( logger.GetType().FullName );
    }
}
