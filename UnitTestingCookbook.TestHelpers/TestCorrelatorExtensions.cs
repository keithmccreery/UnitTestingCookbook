using Serilog.Events;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Extensions for asserting on captured Serilog log events - counting, filtering, and reading structured
/// properties off them.
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
        /// Counts how many log events were logged at the given level.
        /// </summary>
        /// <param name="level">The log level to count.</param>
        /// <returns>The number of log events at that level.</returns>
        public int CountAtLevel(LogEventLevel level)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Count(e => e.Level == level);
        }

        /// <summary>
        /// Counts how many log events were logged using the given message template - the unrendered template
        /// text (e.g. <c>"Hello {Name}"</c>), not the rendered message.
        /// </summary>
        /// <param name="messageTemplate">The message template to match, exactly as passed to the logging call.</param>
        /// <returns>The number of log events using that template.</returns>
        public int CountWithMessageTemplate(string messageTemplate)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Count(e => e.MessageTemplate.Text == messageTemplate);
        }

        /// <summary>
        /// Checks whether any log event was logged using the given message template - the unrendered template
        /// text, not the rendered message.
        /// </summary>
        /// <param name="messageTemplate">The message template to match, exactly as passed to the logging call.</param>
        /// <returns><see langword="true"/> if at least one log event used that template; otherwise <see langword="false"/>.</returns>
        public bool HasMessageTemplate(string messageTemplate)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Any(e => e.MessageTemplate.Text == messageTemplate);
        }

        /// <summary>
        /// Checks whether any log event's rendered message (its template with arguments substituted in) equals
        /// the given text.
        /// </summary>
        /// <param name="renderedMessage">The fully rendered message text to match.</param>
        /// <returns><see langword="true"/> if at least one log event rendered to that exact text; otherwise <see langword="false"/>.</returns>
        public bool HasMessage(string renderedMessage)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Any(e => e.RenderMessage() == renderedMessage);
        }

        /// <summary>
        /// Filters to only the log events logged at the given level.
        /// </summary>
        /// <param name="level">The log level to filter to.</param>
        /// <returns>The matching log events.</returns>
        public IEnumerable<LogEvent> WithLevel(LogEventLevel level)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Where(e => e.Level == level);
        }

        /// <summary>
        /// Filters to only the log events logged using the given message template - the unrendered template
        /// text, not the rendered message.
        /// </summary>
        /// <param name="messageTemplate">The message template to match, exactly as passed to the logging call.</param>
        /// <returns>The matching log events.</returns>
        public IEnumerable<LogEvent> WithMessageTemplate(string messageTemplate)
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Where(e => e.MessageTemplate.Text == messageTemplate);
        }

        /// <summary>
        /// Projects each log event to a <see cref="LogEventSummary"/> - a plain record comparable with
        /// AwesomeAssertions' <c>BeEquivalentTo()</c>, since <see cref="LogEvent"/> itself isn't practical to
        /// compare directly.
        /// </summary>
        /// <returns>One <see cref="LogEventSummary"/> per log event, in the same order.</returns>
        public IEnumerable<LogEventSummary> ToSummaries()
        {
            ArgumentNullException.ThrowIfNull(logEvents);

            return logEvents.Select(e => new LogEventSummary(e.Level, e.MessageTemplate.Text, e.RenderMessage()));
        }
    }

    extension(LogEvent logEvent)
    {
        /// <summary>
        /// Gets a structured logging property's value by name, unwrapping it from the <see cref="ScalarValue"/>
        /// Serilog wraps simple property values in.
        /// </summary>
        /// <typeparam name="T">The property value's expected type.</typeparam>
        /// <param name="propertyName">The property name, as it appears in the message template (e.g. <c>"Name"</c> for <c>"Hello {Name}"</c>).</param>
        /// <returns>The property's value, cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="KeyNotFoundException">No property named <paramref name="propertyName"/> was found on this log event.</exception>
        /// <exception cref="InvalidCastException">The property exists but isn't a scalar value (e.g. it's a structured/destructured object), so it can't be unwrapped this way.</exception>
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
/// A flattened, directly comparable snapshot of a <see cref="LogEvent"/>'s level, message template, and
/// rendered message - for asserting on a whole list of log events at once (e.g. via AwesomeAssertions'
/// <c>BeEquivalentTo()</c>), which <see cref="LogEvent"/> itself doesn't support well.
/// </summary>
/// <param name="Level">The log event's level.</param>
/// <param name="MessageTemplate">The unrendered message template text, exactly as passed to the logging call.</param>
/// <param name="RenderedMessage">The message template with its arguments substituted in.</param>
public record LogEventSummary(LogEventLevel Level, string MessageTemplate, string RenderedMessage);
