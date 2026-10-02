using Moq;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("mocking")]
[TestFixture]
public class MockingTests
{
    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_A_SetupReturnValue_Moq
    public void A_SetupReturnValue_Moq()
    {
        // Arrange
        Mock<IGreetingService> greetingServiceMock = new Mock<IGreetingService>();
        greetingServiceMock.Setup(x => x.Greet("Alice")).Returns("Hello, Alice!");

        // Act
        string result = greetingServiceMock.Object.Greet("Alice");

        // Assert
        result.Should().Be("Hello, Alice!");
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_B_SetupReturnValue_NSubstitute
    public void B_SetupReturnValue_NSubstitute()
    {
        // Arrange
        IGreetingService greetingService = Substitute.For<IGreetingService>();
        greetingService.Greet("Alice").Returns("Hello, Alice!");

        // Act
        string result = greetingService.Greet("Alice");

        // Assert
        result.Should().Be("Hello, Alice!");
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_C_VerifyCallWasMade_Moq
    public void C_VerifyCallWasMade_Moq()
    {
        // Arrange
        Mock<IGreetingService> greetingServiceMock = new Mock<IGreetingService>();

        // Act
        greetingServiceMock.Object.Greet("Bob");

        // Assert
        greetingServiceMock.Verify(x => x.Greet("Bob"), Times.Once());
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_D_VerifyCallWasMade_NSubstitute
    public void D_VerifyCallWasMade_NSubstitute()
    {
        // Arrange
        IGreetingService greetingService = Substitute.For<IGreetingService>();

        // Act
        greetingService.Greet("Bob");

        // Assert
        greetingService.Received(1).Greet("Bob");
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_E_ArgumentMatching_Moq
    public void E_ArgumentMatching_Moq()
    {
        // Arrange
        Mock<IGreetingService> greetingServiceMock = new Mock<IGreetingService>();
        greetingServiceMock.Setup(x => x.Greet(It.IsAny<string>())).Returns("Hello, whoever you are!");

        // Act
        string result = greetingServiceMock.Object.Greet("Anyone");

        // Assert
        result.Should().Be("Hello, whoever you are!");
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_F_ArgumentMatching_NSubstitute
    public void F_ArgumentMatching_NSubstitute()
    {
        // Arrange
        IGreetingService greetingService = Substitute.For<IGreetingService>();
        greetingService.Greet(Arg.Any<string>()).Returns("Hello, whoever you are!");

        // Act
        string result = greetingService.Greet("Anyone");

        // Assert
        result.Should().Be("Hello, whoever you are!");
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_G_ThrowException_Moq
    public void G_ThrowException_Moq()
    {
        // Arrange
        Mock<IGreetingService> greetingServiceMock = new Mock<IGreetingService>();
        greetingServiceMock.Setup(x => x.Greet(It.IsAny<string>())).Throws<InvalidOperationException>();

        // Act
        Action action = () => greetingServiceMock.Object.Greet("Anyone");

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: MockingTests_H_ThrowException_NSubstitute
    public void H_ThrowException_NSubstitute()
    {
        // Arrange
        IGreetingService greetingService = Substitute.For<IGreetingService>();
        greetingService.Greet(Arg.Any<string>()).Throws<InvalidOperationException>();

        // Act
        Action action = () => greetingService.Greet("Anyone");

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }
    // end-snippet
}
