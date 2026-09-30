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

```csharp
public interface IGreetingService
{
    string Greet(string name);
}
```

### Moq

```csharp
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

### NSubstitute

Notice there's no separate `.Object` to unwrap - `Substitute.For<T>()` returns something that already *is* a
`T`, and you configure it by just calling the method directly.

```csharp
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

---

## How do I verify a call was made?

### Moq

```csharp
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

### NSubstitute

```csharp
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

---

## How do I match "any" argument instead of an exact value?

### Moq

```csharp
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

### NSubstitute

```csharp
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

---

## How do I make a mocked call throw?

### Moq

```csharp
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

### NSubstitute

**NOTE:** `.Throws<T>()` requires `using NSubstitute.ExceptionExtensions;`.

```csharp
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

---

Back to [README](../README.md)
