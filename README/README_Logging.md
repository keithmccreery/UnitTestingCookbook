# Logging

## NuGet Packages Referenced

- Moq https://github.com/moq/moq
- Serilog.Extensions.Logging https://github.com/serilog/serilog-extensions-logging
- Serilog.Sinks.TestCorrelator https://github.com/MitchBodmer/serilog-sinks-testcorrelator

All examples are located in `UnitTestingCookbook.Test` -> [`LoggingTest`](../UnitTestingCookbook.Test/LoggingTest.cs)  

## Credits

- [Mocking ILogger with Moq](https://adamstorr.azurewebsites.net/blog/mocking-ilogger-with-moq) for the basis of `.VerifyLogging()`

---

## How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLoggerFactory?

Given a Class Constructor...  

```csharp
public SampleWithLogging(ILogger<SampleWithLogging> logger)
{
    this.logger = logger;
}
```

The Unit Test...  

```csharp
public void A_ILogger_Via_NullLoggerFactory()
{
    // Arrange
    ILoggerFactory nullLoggerFactory = new NullLoggerFactory();
    ILogger<SampleWithLogging> logger = new Logger<SampleWithLogging>(nullLoggerFactory);

    SampleWithLogging sampleWithLogging = new SampleWithLogging(logger);

    // Act
    sampleWithLogging.LogInformationMessage("anything");

    // Assert
    logger.Should().NotBeNull();
}
```

---

## How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLogger?

```csharp
public void B_ILogger_Via_NullLogger()
{
    // Arrange
    ILogger<SampleWithLogging> logger = new NullLogger<SampleWithLogging>();

    SampleWithLogging sampleWithLogging = new SampleWithLogging(logger);

    // Act
    sampleWithLogging.LogInformationMessage("anything");

    // Assert
    logger.Should().NotBeNull();
}
```

---

## How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLoggerFactory and Dependency Injection?

```csharp
public void C_ILogger_Via_DependencyInjection_NullLoggerFactory()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
    services.AddSingleton<ILogger<SampleWithLogging>>(x => x.GetRequiredService<ILoggerFactory>().CreateLogger<SampleWithLogging>());
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
    sampleWithLogging.LogInformationMessage("anything");

    // Assert
    sampleWithLogging.Should().NotBeNull();
}
```

---

## How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLogger and Dependency Injection?

Because most implementations of `Microsoft.Extensions.Logging.ILogger` are the generic `ILogger<T>`,
a generic Injection `ILogger<>` of `services.AddSingleton( typeof( ILogger<> ), typeof( NullLogger<> ) )` must be defined.

```csharp
public void D_ILogger_Via_DependencyInjection_NullLogger()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)); // handles all generics
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
    sampleWithLogging.LogInformationMessage("anything");

    // Assert
    sampleWithLogging.Should().NotBeNull();
}
```

---

## How do I Instantiate a Class with Microsoft.Extensions.Logging ILoggerFactory via NullLoggerFactory and Dependency Injection?

Given the class with multiple constructors...  

**NOTE:** The constructor with the most parameters where the types are DI-resolvable is selected.  

**NOTE:** This approach allows for better handling of the logger.  

Tests are not required to instantiate or Mock a logger and the class has full control over the creation of the logger.  

```csharp
public class SampleWithLoggingFactory
{
    private readonly ILogger<SampleWithLoggingFactory> logger;

    public SampleWithLoggingFactory() : this(NullLoggerFactory.Instance)
    {
    }

    public SampleWithLoggingFactory(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger<SampleWithLoggingFactory>();
    }

    public void LogInformationMessage(string? message)
    {
        logger.LogInformation("This is the template with a {Message}.", message);

        System.Console.WriteLine(logger.GetType().FullName);
    }
}
```

If all classes only required an `ILoggerFactory`, then there is no need to define an `ILogger<T>` for injection.  

```csharp
public void E_ILoggerFactory_Via_DependencyInjection_NullLoggerFactory()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
    services.AddSingleton<SampleWithLoggingFactory>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLoggingFactory sampleWithLoggingFactory = serviceProvider.GetRequiredService<SampleWithLoggingFactory>();
    sampleWithLoggingFactory.LogInformationMessage("anything");

    // Assert
    sampleWithLoggingFactory.Should().NotBeNull();
}
```

---

## How can I use Serilog with Microsoft.Extensions.Logging ILoggerFactory?

**Why?** So I can have all the functionality of Serilog, with the Microsoft.Extensions.Logging ILogger or ILoggerFactory pattern,
including TestCorrelator for easier Testing (see below).  

```csharp
public void F_Serilog_To_ILoggerFactory()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
    services.AddSingleton<SampleWithLoggingFactory>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLoggingFactory sampleWithLoggingFactory = serviceProvider.GetRequiredService<SampleWithLoggingFactory>();
    sampleWithLoggingFactory.LogInformationMessage("anything");

    // Assert
    sampleWithLoggingFactory.Should().NotBeNull();
}
```

---

## How can I use Serilog with Microsoft.Extensions.Logging ILogger?

```csharp
public void G_Serilog_To_ILogger()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>)); // handles all generics
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
    sampleWithLogging.LogInformationMessage("anything");

    // Assert
    sampleWithLogging.Should().NotBeNull();
}
```

---

## How can I use Serilog with Microsoft.Extensions.Logging ILogger to display Log Context to Console?

Because we are defining `ILogger<>` and `ILoggerFactory`, this approach works for all constructor implementations.
This sample shows `ILogger<>`, but will also support `ILoggerFactory`.  

The `outputTemplate` or `"[{Timestamp:HH:mm:ss} {Level:u3}] [{Properties:j}] {Message:lj}{NewLine}{Exception}"`, specifically `[{Properties:j}]`, illustrates how to display the log context(s) to the console.  

```csharp
public void H_Serilog_To_ILogger_Context_To_Console()
{
    // Arrange
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Properties:j}] {Message:lj}{NewLine}{Exception}")
        .Enrich.FromLogContext()
        .CreateLogger();

    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger)); // Consume Serilog.ILogger
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>)); // handles all generics
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
    sampleWithLogging.LogInformationMessage("message");

    // Assert
    sampleWithLogging.Should().NotBeNull();
}
```

---

## How can I assert Logs with Serilog.Sinks.TestCorrelator?

**NOTE:** By casting to a `ScalarValue`, we can easily get the message.  

```csharp
public void I_Serilog_TestCorrelator()
{
    // Arrange
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Verbose()
        .WriteTo.TestCorrelator() // Catches Log Messages
        .CreateLogger();

    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger)); // Consume Serilog.ILogger
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>)); // handles all generics
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    const string message = "anything";

    using (TestCorrelator.CreateContext())
    {
        // Act
        SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
        sampleWithLogging.LogInformationMessage(message);

        IEnumerable<LogEvent> logs = TestCorrelator.GetLogEventsFromCurrentContext();

        // Assert
        logs.Should()
            .ContainSingle()
            .Which.MessageTemplate.Text.Should().Be("This is the template with a {Message}.");

        // simple strings
        logs.Should()
            .ContainSingle()
            .Which.Properties["Message"] // LogEventPropertyValue
            .As<ScalarValue>() // ScalarValue (use for string)
            .Value.Should().Be(message);
    }
}
```

---

## How can I assert Logs with Serilog.Sinks.TestCorrelator with an Object?

```csharp
public void J_Serilog_TestCorrelator_Structured()
{
    // Arrange
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Verbose()
        .WriteTo.TestCorrelator() // Catches Log Messages
        .CreateLogger();

    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger)); // Consume Serilog.ILogger
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>)); // handles all generics
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    var @object = new
    {
        Name = "John",
        Age = 21,
    };

    using (TestCorrelator.CreateContext())
    {
        // Act
        SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
        sampleWithLogging.LogInformationStructuredMessage(@object);

        IEnumerable<LogEvent> logs = TestCorrelator.GetLogEventsFromCurrentContext();

        // Assert
        logs.Should()
            .ContainSingle()
            .Which.MessageTemplate.Text.Should().Be("This is the template with a {@Object}.");

        logs.Should()
            .ContainSingle()
            .Which.Properties["Object"] // LogEventPropertyValue
            .As<StructureValue>() // StructureValue (use for object)
            .ToString()
            .Should().Be("{ Name: \"John\", Age: 21 }");
        // StructureValue has a property named 'Properties' which is an array of LogEventProperty
        // LogEventProperty is a key/value object as 'Name' and 'Value'
    }
}
```

---

## How do I Mock and Verify Microsoft.Extensions.Logging ILogger?

### Answer 1 - Good

This Mock `.Verify()` will catch **ALL** logs.  

```csharp
public void K_Mock_ILogger_Good()
{
    // Arrange
    var loggerMock = new Mock<ILogger<SampleWithLogging>>();
    ILogger<SampleWithLogging> logger = loggerMock.Object;

    SampleWithLogging sample = new SampleWithLogging(logger);

    const string message = "anything";

    // Act
    sample.LogInformationMessage(message);

    // Assert
    loggerMock.Verify(
    x => x.Log(
        It.IsAny<LogLevel>(),
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => true),
        It.IsAny<Exception>(),
        It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)));
}
```

### Answer 2 - Better

This Mock `.Verify()` will catch the specific log we want.   

```csharp
public void L_Mock_ILogger_Better()
{
    // Arrange
    var loggerMock = new Mock<ILogger<SampleWithLogging>>();
    ILogger<SampleWithLogging> logger = loggerMock.Object;

    SampleWithLogging sample = new SampleWithLogging(logger);

    const string message = "anything";

    // Act
    sample.LogInformationMessage(message);

    // Assert
    loggerMock.Verify(
    x => x.Log(
        It.Is<LogLevel>(l => l == LogLevel.Information), // severity
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)), // message (substring)
        It.IsAny<Exception>(),
        It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)));
}
```

### Answer 3 - Best

This solution uses an Extension Method `.VerifyLogging<T>()`, located in `ExtensionMethods.cs` in `UnitTestingCookbook.TestHelpers` project.  

To verify any log and every log, we can implement an Extension Method.  

**NOTE:** This Extension Method could be parameterized further to include `EventId`, and `Exception`.  

```csharp
public static Mock<ILogger<T>> VerifyLogging<T>(this Mock<ILogger<T>> logger, string expectedMessage, LogLevel expectedLogLevel = LogLevel.Debug, Times? times = null)
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
```

Now the verification can be specific and also verify multiple logs, by chaining.  

```csharp
public void M_Mock_ILogger_Best()
{
    // Arrange
    var loggerMock = new Mock<ILogger<SampleWithLogging>>();
    ILogger<SampleWithLogging> logger = loggerMock.Object;

    SampleWithLogging sample = new SampleWithLogging(logger);

    const string message = "anything";

    // Act
    sample.LogInformationMessage(message);

    // Assert
    loggerMock.VerifyLogging($"This is the template with a {message}.", LogLevel.Information, Times.Once());
}
```

---

Back to [README](../README.md)
