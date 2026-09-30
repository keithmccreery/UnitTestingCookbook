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
    extension(IEnumerable<LogEvent> logEvents)
    {
        /// <summary>
        /// Count At Level
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public int CountAtLevel(LogEventLevel level)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Count(e => e.Level == level);
        }

        /// <summary>
        /// Count With Message Template
        /// </summary>
        /// <param name="messageTemplate"></param>
        /// <returns></returns>
        public int CountWithMessageTemplate(string messageTemplate)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Count(e => e.MessageTemplate.Text == messageTemplate);
        }

        /// <summary>
        /// Has Message Template
        /// </summary>
        /// <param name="messageTemplate"></param>
        /// <returns></returns>
        public bool HasMessageTemplate(string messageTemplate)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Any(e => e.MessageTemplate.Text == messageTemplate);
        }

        /// <summary>
        /// Has Message
        /// </summary>
        /// <param name="renderedMessage"></param>
        /// <returns></returns>
        public bool HasMessage(string renderedMessage)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Any(e => e.RenderMessage() == renderedMessage);
        }

        /// <summary>
        /// With Level
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public IEnumerable<LogEvent> WithLevel(LogEventLevel level)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Where(e => e.Level == level);
        }

        /// <summary>
        /// With Message Template
        /// </summary>
        /// <param name="messageTemplate"></param>
        /// <returns></returns>
        public IEnumerable<LogEvent> WithMessageTemplate(string messageTemplate)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Where(e => e.MessageTemplate.Text == messageTemplate);
        }

        /// <summary>
        /// To Summaries
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LogEventSummary> ToSummaries()
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Select(e => new LogEventSummary(e.Level, e.MessageTemplate.Text, e.RenderMessage()));
        }
    }

    extension(LogEvent logEvent)
    {
        /// <summary>
        /// Get Property Value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        /// <exception cref="InvalidCastException"></exception>
        public T? GetPropertyValue<T>(string propertyName)
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
    }
}

/// <summary>
/// LogEventSummary
/// </summary>
/// <param name="Level"></param>
/// <param name="MessageTemplate"></param>
/// <param name="RenderedMessage"></param>
public record LogEventSummary(LogEventLevel Level, string MessageTemplate, string RenderedMessage);
