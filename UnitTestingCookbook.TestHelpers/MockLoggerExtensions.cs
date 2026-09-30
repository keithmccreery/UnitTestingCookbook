using Microsoft.Extensions.Logging;

using Moq;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Extension for verifying a Moq-mocked <see cref="ILogger{TCategoryName}"/> call without hand-writing
/// <c>Mock.Verify(...)</c>'s five-argument <c>Log(...)</c> expression every time.
/// </summary>
public static class MockLoggerExtensions
{
    extension<T>(Mock<ILogger<T>> logger)
    {
        /// <summary>
        /// Verifies that a message was logged at a given level a given number of times, comparing the logged
        /// message's rendered text against <paramref name="expectedMessage"/>.
        /// </summary>
        /// <remarks>
        /// https://adamstorr.azurewebsites.net/blog/mocking-ilogger-with-moq
        /// </remarks>
        /// <param name="expectedMessage">The exact rendered log message to match.</param>
        /// <param name="expectedLogLevel">The log level the message is expected to have been logged at. Defaults to <see cref="LogLevel.Debug"/>.</param>
        /// <param name="times">How many times the message is expected to have been logged. Defaults to <see cref="Times.Once()"/>.</param>
        /// <returns>The same <see cref="Mock{T}"/>, so further verifications can be chained.</returns>
        public Mock<ILogger<T>> VerifyLogging(string expectedMessage, LogLevel expectedLogLevel = LogLevel.Debug, Times? times = null)
        {
            times ??= Times.Once();

            logger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == expectedLogLevel),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v != null && v.ToString() == expectedMessage),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true))
                , (Times) times);

            return logger;
        }
    }
}
