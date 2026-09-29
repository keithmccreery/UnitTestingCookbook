using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

using Polly;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Support;

/// <summary>
/// Polly Context Extensions
/// </summary>
/// <remarks>
/// https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory#configuring-httpclientfactory-policies-to-use-an-iloggert-from-the-call-site
/// </remarks>
public static class PollyContextExtensions
{
    private static readonly string LoggerKey = "ILogger";

    public static Context WithLogger<T>(this Context context, ILogger logger)
    {
        context[LoggerKey] = logger;
        return context;
    }

    public static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue(LoggerKey, out object logger))
        {
            return logger as ILogger;
        }

        return null;
    }

    public static HttpRequestMessage AddPollyContext<T>(this HttpRequestMessage @this, ILogger<T> logger, [CallerMemberName] string memberName = "")
    {
        Context context = new Context($"{typeof(T).Name}.{memberName}");

        context.WithLogger<T>(logger);

        @this.SetPolicyExecutionContext(context);

        return @this;
    }
}
