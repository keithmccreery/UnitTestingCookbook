using Microsoft.Extensions.Logging;

using Moq;

namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class MockLoggerExtensionsTests
{
    // Must be public - Moq's proxy generator needs access to this type as ILogger<T>'s generic argument.
    public class Subject;

    [Test]
    [Category("_passes")]
    public void A_VerifyLogging_MatchingMessageAndLevel_Succeeds()
    {
        // Arrange
        Mock<ILogger<Subject>> loggerMock = new Mock<ILogger<Subject>>();
        loggerMock.Object.LogInformation("expected message");

        // Act
        Action action = () => loggerMock.VerifyLogging("expected message", LogLevel.Information);

        // Assert
        action.Should().NotThrow();
    }

    [Test]
    [Category("_passes")]
    public void B_VerifyLogging_WrongLevel_Throws()
    {
        // Arrange
        Mock<ILogger<Subject>> loggerMock = new Mock<ILogger<Subject>>();
        loggerMock.Object.LogInformation("expected message");

        // Act
        Action action = () => loggerMock.VerifyLogging("expected message", LogLevel.Error);

        // Assert
        action.Should().Throw<MockException>();
    }

    [Test]
    [Category("_passes")]
    public void C_VerifyLogging_WrongMessage_Throws()
    {
        // Arrange
        Mock<ILogger<Subject>> loggerMock = new Mock<ILogger<Subject>>();
        loggerMock.Object.LogInformation("actual message");

        // Act
        Action action = () => loggerMock.VerifyLogging("different message", LogLevel.Information);

        // Assert
        action.Should().Throw<MockException>();
    }

    [Test]
    [Category("_passes")]
    public void D_VerifyLogging_ReturnsSameMockForChaining()
    {
        // Arrange
        Mock<ILogger<Subject>> loggerMock = new Mock<ILogger<Subject>>();
        loggerMock.Object.LogInformation("expected message");

        // Act
        Mock<ILogger<Subject>> result = loggerMock.VerifyLogging("expected message", LogLevel.Information);

        // Assert
        result.Should().BeSameAs(loggerMock);
    }
}
