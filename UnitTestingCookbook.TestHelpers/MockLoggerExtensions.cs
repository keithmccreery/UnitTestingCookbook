using Microsoft.Extensions.Logging;

using Moq;

namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// MockLoggerExtensions
/// </summary>
public static class MockLoggerExtensions
{
    extension<T>(Mock<ILogger<T>> logger)
    {
        /// <summary>
        /// VerifyLogging
        /// </summary>
        /// <remarks>
        /// https://adamstorr.azurewebsites.net/blog/mocking-ilogger-with-moq
        /// </remarks>
        /// <param name="expectedMessage"></param>
        /// <param name="expectedLogLevel"></param>
        /// <param name="times"></param>
        /// <returns></returns>
        public Mock<ILogger<T>> VerifyLogging(string expectedMessage, LogLevel expectedLogLevel = LogLevel.Debug, Times? times = null)
        {
            times ??= Times.Once();

            Func<object, Type, bool> state = (v, t) => v.ToString()?.CompareTo(expectedMessage) == 0;

            logger.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == expectedLogLevel),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => state(v, t)),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true))
                , (Times) times);

            return logger;
        }
    }
}
