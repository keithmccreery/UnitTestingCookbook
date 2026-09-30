using Serilog.Events;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// TestCorrelatorExtensions
/// </summary>
/// <remarks>
/// Operates on <see cref="LogEvent"/> - the type Serilog.Sinks.TestCorrelator's
/// TestCorrelator.GetLogEventsFromCurrentContext() returns - without this project needing to
/// reference the TestCorrelator package itself.
/// </remarks>
public static class TestCorrelatorExtensions
{
    /// <summary>
    /// Count At Level
    /// </summary>
    /// <param name="logEvents"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public static int CountAtLevel(this IEnumerable<LogEvent> logEvents, LogEventLevel level)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Count(e => e.Level == level);
    }

    /// <summary>
    /// Count With Message Template
    /// </summary>
    /// <param name="logEvents"></param>
    /// <param name="messageTemplate"></param>
    /// <returns></returns>
    public static int CountWithMessageTemplate(this IEnumerable<LogEvent> logEvents, string messageTemplate)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Count(e => e.MessageTemplate.Text == messageTemplate);
    }

    /// <summary>
    /// Has Message Template
    /// </summary>
    /// <param name="logEvents"></param>
    /// <param name="messageTemplate"></param>
    /// <returns></returns>
    public static bool HasMessageTemplate(this IEnumerable<LogEvent> logEvents, string messageTemplate)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Any(e => e.MessageTemplate.Text == messageTemplate);
    }

    /// <summary>
    /// Has Message
    /// </summary>
    /// <remarks>
    /// Compares against the fully rendered message (template + property values substituted in),
    /// not the raw template - use <see cref="HasMessageTemplate"/> to match on the template alone.
    /// </remarks>
    /// <param name="logEvents"></param>
    /// <param name="renderedMessage"></param>
    /// <returns></returns>
    public static bool HasMessage(this IEnumerable<LogEvent> logEvents, string renderedMessage)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Any(e => e.RenderMessage() == renderedMessage);
    }

    /// <summary>
    /// With Level
    /// </summary>
    /// <param name="logEvents"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public static IEnumerable<LogEvent> WithLevel(this IEnumerable<LogEvent> logEvents, LogEventLevel level)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Where(e => e.Level == level);
    }

    /// <summary>
    /// With Message Template
    /// </summary>
    /// <param name="logEvents"></param>
    /// <param name="messageTemplate"></param>
    /// <returns></returns>
    public static IEnumerable<LogEvent> WithMessageTemplate(this IEnumerable<LogEvent> logEvents, string messageTemplate)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Where(e => e.MessageTemplate.Text == messageTemplate);
    }

    /// <summary>
    /// Get Property Value
    /// </summary>
    /// <remarks>
    /// Unwraps a scalar (string/number/bool/...) logged property without the caller needing to know
    /// about LogEventPropertyValue/ScalarValue. For a structured (object) property, use the LogEvent's
    /// Properties dictionary directly - see the Logging chapter's example (J_Serilog_TestCorrelator_Structured).
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    /// <param name="logEvent"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static T? GetPropertyValue<T>(this LogEvent logEvent, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (!logEvent.Properties.TryGetValue(propertyName, out LogEventPropertyValue? value))
        {
            throw new KeyNotFoundException($"No property named '{propertyName}' was found on this LogEvent.");
        }

        return value switch
        {
            ScalarValue scalar => (T?) scalar.Value,
            _ => throw new InvalidCastException($"Property '{propertyName}' is a {value.GetType().Name}, not a scalar value."),
        };
    }

    /// <summary>
    /// To Summaries
    /// </summary>
    /// <remarks>
    /// Projects LogEvents into a simple, comparable record - useful for asserting a whole list of
    /// expected log events at once with AwesomeAssertions' .Should().BeEquivalentTo(), since LogEvent
    /// itself isn't practical to compare directly (its Timestamp and Properties dictionary make an
    /// equality/equivalency comparison noisy or impossible).
    /// </remarks>
    /// <param name="logEvents"></param>
    /// <returns></returns>
    public static IEnumerable<LogEventSummary> ToSummaries(this IEnumerable<LogEvent> logEvents)
    {
        ArgumentNullException.ThrowIfNull(logEvents);

        return logEvents.Select(e => new LogEventSummary(e.Level, e.MessageTemplate.Text, e.RenderMessage()));
    }
}

/// <summary>
/// LogEventSummary
/// </summary>
/// <param name="Level"></param>
/// <param name="MessageTemplate"></param>
/// <param name="RenderedMessage"></param>
public record LogEventSummary(LogEventLevel Level, string MessageTemplate, string RenderedMessage);
