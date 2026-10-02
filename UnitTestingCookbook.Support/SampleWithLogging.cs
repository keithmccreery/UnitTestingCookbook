using Microsoft.Extensions.Logging;

namespace UnitTestingCookbook.Support;

public class SampleWithLogging
{
    private readonly ILogger<SampleWithLogging> logger;

    // begin-snippet: SampleWithLogging_Constructor
    public SampleWithLogging(ILogger<SampleWithLogging> logger)
    {
        this.logger = logger;
    }
    // end-snippet

    public void LogInformationMessage(string? message)
    {
        using (logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { { "WrappedContext", "sample" } }))
        {
            logger.LogInformation("This is the template with a {Message}.", message);
        }

        System.Console.WriteLine(logger.GetType().FullName);
    }

    public void LogInformationStructuredMessage(object @object)
    {
        logger.LogInformation("This is the template with a {@Object}.", @object);

        System.Console.WriteLine(logger.GetType().FullName);
    }
}
