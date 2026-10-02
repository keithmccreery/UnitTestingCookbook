# Mocking

## NuGet Packages Referenced

- Moq https://github.com/moq/moq
- NSubstitute https://github.com/nsubstitute/NSubstitute

All examples are located in `UnitTestingCookbook.Tests` -> [`MockingTests`](../UnitTestingCookbook.Tests/MockingTests.cs)

**NOTE:** This chapter is the source of truth for basic mocking mechanics - setting up a return value, verifying
a call was made, matching arguments, and throwing an exception. Later chapters that need mocking
([HttpClientFactory](./README_HttpClientFactory.md), [Logging](./README_Logging.md)'s `MockLoggerExtensions`)
use Moq specifically because they're mocking a concrete framework type (`HttpMessageHandler`, `ILogger<T>`), not
because Moq is this cookbook's recommendation - those existing examples are left as Moq for now rather than
rewritten.

**NOTE:** Moq has fallen out of favor with a meaningful part of the .NET community - most notably after a 2023
incident where a Moq release silently bundled telemetry/data-collection code (since reverted, but trust took a
hit) - and [NSubstitute](https://github.com/nsubstitute/NSubstitute) has become the community's preferred
alternative: a smaller, more readable API with no equivalent controversy. Every example below is shown in both,
side by side, so you can compare and decide for yourself.

---

## How do I mock an interface and set up a return value?

<!-- snippet: IGreetingService -->
<a id='snippet-IGreetingService'></a>
```cs
public interface IGreetingService
{
    string Greet(string name);
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/IGreetingService.cs#L3-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-IGreetingService' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Moq

<!-- snippet: MockingTests_A_SetupReturnValue_Moq -->
<a id='snippet-MockingTests_A_SetupReturnValue_Moq'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L17-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_A_SetupReturnValue_Moq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### NSubstitute

Notice there's no separate `.Object` to unwrap - `Substitute.For<T>()` returns something that already *is* a
`T`, and you configure it by just calling the method directly.

<!-- snippet: MockingTests_B_SetupReturnValue_NSubstitute -->
<a id='snippet-MockingTests_B_SetupReturnValue_NSubstitute'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L34-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_B_SetupReturnValue_NSubstitute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I verify a call was made?

### Moq

<!-- snippet: MockingTests_C_VerifyCallWasMade_Moq -->
<a id='snippet-MockingTests_C_VerifyCallWasMade_Moq'></a>
```cs
public void C_VerifyCallWasMade_Moq()
{
    // Arrange
    Mock<IGreetingService> greetingServiceMock = new Mock<IGreetingService>();

    // Act
    greetingServiceMock.Object.Greet("Bob");

    // Assert
    greetingServiceMock.Verify(x => x.Greet("Bob"), Times.Once());
}
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L51-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_C_VerifyCallWasMade_Moq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### NSubstitute

<!-- snippet: MockingTests_D_VerifyCallWasMade_NSubstitute -->
<a id='snippet-MockingTests_D_VerifyCallWasMade_NSubstitute'></a>
```cs
public void D_VerifyCallWasMade_NSubstitute()
{
    // Arrange
    IGreetingService greetingService = Substitute.For<IGreetingService>();

    // Act
    greetingService.Greet("Bob");

    // Assert
    greetingService.Received(1).Greet("Bob");
}
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L67-L79' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_D_VerifyCallWasMade_NSubstitute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I match "any" argument instead of an exact value?

### Moq

<!-- snippet: MockingTests_E_ArgumentMatching_Moq -->
<a id='snippet-MockingTests_E_ArgumentMatching_Moq'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L83-L96' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_E_ArgumentMatching_Moq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### NSubstitute

<!-- snippet: MockingTests_F_ArgumentMatching_NSubstitute -->
<a id='snippet-MockingTests_F_ArgumentMatching_NSubstitute'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L100-L113' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_F_ArgumentMatching_NSubstitute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I make a mocked call throw?

### Moq

<!-- snippet: MockingTests_G_ThrowException_Moq -->
<a id='snippet-MockingTests_G_ThrowException_Moq'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L117-L130' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_G_ThrowException_Moq' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### NSubstitute

**NOTE:** `.Throws<T>()` requires `using NSubstitute.ExceptionExtensions;`.

<!-- snippet: MockingTests_H_ThrowException_NSubstitute -->
<a id='snippet-MockingTests_H_ThrowException_NSubstitute'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/MockingTests.cs#L134-L147' title='Snippet source file'>snippet source</a> | <a href='#snippet-MockingTests_H_ThrowException_NSubstitute' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

Back to [README](../README.md)
