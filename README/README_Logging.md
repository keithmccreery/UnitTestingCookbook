# Logging

## NuGet Packages Referenced

- Microsoft.Extensions.Diagnostics.Testing https://github.com/dotnet/extensions (`FakeLogger<T>` / `FakeLogCollector`)
- Moq https://github.com/moq/moq
- Serilog.Extensions.Logging https://github.com/serilog/serilog-extensions-logging
- Serilog.Sinks.TestCorrelator https://github.com/MitchBodmer/serilog-sinks-testcorrelator

`UnitTestingCookbook.TestHelpers`'s [`TestCorrelatorExtensions`](../UnitTestingCookbook.TestHelpers/TestCorrelatorExtensions.cs)
adds a handful of extension methods over `IEnumerable<LogEvent>` (what `TestCorrelator.GetLogEventsFromCurrentContext()`
returns) - see below.  

All examples are located in `UnitTestingCookbook.Tests` -> [`LoggingTests`](../UnitTestingCookbook.Tests/LoggingTests.cs)  

**NOTE:** The `ServiceCollection`/DI wiring below follows the same basic pattern as
[Dependency Injection](./README_DependencyInjection.md) (the source of truth for that pattern) - repeated here
inline so this chapter stands on its own.  

## Credits

- [Mocking ILogger with Moq](https://adamstorr.azurewebsites.net/blog/mocking-ilogger-with-moq) for the basis of `.VerifyLogging()`

**Which approach should I use?** See [NullLogger vs. FakeLogger vs. Serilog + TestCorrelator](#nulllogger-vs-fakelogger-vs-serilog--testcorrelator)
at the bottom of this chapter.  

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
        It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains(message)), // message (substring)
        It.IsAny<Exception>(),
        It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)));
}
```

### Answer 3 - Best

This solution uses an Extension Method `.VerifyLogging<T>()`, located in `MockLoggerExtensions.cs` in `UnitTestingCookbook.TestHelpers` project. Written using C# 14's `extension` block syntax, same as
`TestCorrelatorExtensions` further down this same chapter (the first use of this syntax in the repo).  

To verify any log and every log, we can implement an Extension Method.  

**NOTE:** This Extension Method could be parameterized further to include `EventId`, and `Exception`.  

```csharp
extension<T>(Mock<ILogger<T>> logger)
{
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

## How do I count TestCorrelator log events - overall, by severity, or by message template?

`TestCorrelatorExtensions.CountAtLevel()` / `.CountWithMessageTemplate()` - a plain count of everything is just
`logs.Count()` (LINQ, nothing TestCorrelator-specific needed).

```csharp
public void N_TestCorrelator_Count()
{
    // Arrange
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Verbose()
        .WriteTo.TestCorrelator()
        .CreateLogger();

    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger));
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    using (TestCorrelator.CreateContext())
    {
        // Act
        SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
        sampleWithLogging.LogInformationMessage("first");
        sampleWithLogging.LogInformationMessage("second");
        sampleWithLogging.LogInformationStructuredMessage(new { Name = "John" });

        IEnumerable<LogEvent> logs = TestCorrelator.GetLogEventsFromCurrentContext();

        // Assert
        using (new AssertionScope())
        {
            logs.Count().Should().Be(3); // total - plain LINQ, nothing TestCorrelator-specific needed
            logs.CountAtLevel(LogEventLevel.Information).Should().Be(3);
            logs.CountAtLevel(LogEventLevel.Error).Should().Be(0);
            logs.CountWithMessageTemplate("This is the template with a {Message}.").Should().Be(2);
            logs.CountWithMessageTemplate("This is the template with a {@Object}.").Should().Be(1);
        }
    }
}
```

---

## How do I check whether a particular message was logged, without pulling it out and asserting on it manually?

`TestCorrelatorExtensions.HasMessageTemplate()` matches the raw template; `.HasMessage()` matches the fully
*rendered* message (template with property values substituted in - note the string property comes out
double-quoted, since that's how Serilog renders a scalar string by default). `.GetPropertyValue<T>()` unwraps a
scalar property without the caller needing to know about `LogEventPropertyValue`/`ScalarValue` (compare to
`I_Serilog_TestCorrelator` above, which does that unwrapping by hand).

```csharp
public void O_TestCorrelator_HasMessage()
{
    // Arrange
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Verbose()
        .WriteTo.TestCorrelator()
        .CreateLogger();

    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger));
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
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
        using (new AssertionScope())
        {
            logs.HasMessageTemplate("This is the template with a {Message}.").Should().BeTrue();
            logs.HasMessageTemplate("This template was never logged.").Should().BeFalse();

            logs.HasMessage($"This is the template with a \"{message}\".").Should().BeTrue();
            logs.HasMessage("This message was never logged.").Should().BeFalse();

            logs.Single().GetPropertyValue<string>("Message").Should().Be(message);
        }
    }
}
```

---

## How do I compare a whole list of expected log events at once?

`TestCorrelatorExtensions.ToSummaries()` projects `LogEvent`s into a simple, comparable `LogEventSummary` record
(`Level`/`MessageTemplate`/`RenderedMessage`) for use with AwesomeAssertions' `.Should().BeEquivalentTo()` -
`LogEvent` itself isn't practical to compare directly (its `Timestamp` and `Properties` dictionary make an
equality/equivalency comparison noisy or impossible).

```csharp
public void P_TestCorrelator_CompareList()
{
    // Arrange
    Serilog.ILogger serilogLogger = new LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Verbose()
        .WriteTo.TestCorrelator()
        .CreateLogger();

    ServiceCollection services = new ServiceCollection();
    services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilogLogger));
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    using (TestCorrelator.CreateContext())
    {
        // Act
        SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
        sampleWithLogging.LogInformationMessage("first");
        sampleWithLogging.LogInformationMessage("second");

        IEnumerable<LogEvent> logs = TestCorrelator.GetLogEventsFromCurrentContext();

        // Assert
        logs.ToSummaries().Should().BeEquivalentTo(new[]
        {
            new LogEventSummary(LogEventLevel.Information, "This is the template with a {Message}.", "This is the template with a \"first\"."),
            new LogEventSummary(LogEventLevel.Information, "This is the template with a {Message}.", "This is the template with a \"second\"."),
        });
    }
}
```

**NOTE:** `TestCorrelatorExtensions` also has `.WithLevel()` and `.WithMessageTemplate()` (filter to a matching
subset, for chaining into further assertions) - not shown above, but useful once a test logs more events than it
wants to assert on all at once.  

---

## How do I assert Microsoft.Extensions.Logging ILogger logs with FakeLogger, without Serilog or a Mock?

`FakeLogger<T>` (NuGet `Microsoft.Extensions.Diagnostics.Testing`, Microsoft's own package) is a real `ILogger<T>`
that records every log into a `FakeLogCollector`. Nothing to configure: no Serilog pipeline, no `TestCorrelator`
context, no Moq `It.IsAnyType` gymnastics.  

- `.Message` is the **rendered** message, formatted by Microsoft.Extensions.Logging. A string property is **not**
double-quoted, unlike Serilog's rendering in `O_TestCorrelator_HasMessage` above.
- `.GetStructuredStateValue("{OriginalFormat}")` returns the raw message template.
- `.GetStructuredStateValue("Message")` returns a single property, always as a `string?`.

```csharp
public void Q_FakeLogger()
{
    // Arrange
    FakeLogger<SampleWithLogging> logger = new FakeLogger<SampleWithLogging>();

    SampleWithLogging sampleWithLogging = new SampleWithLogging(logger);

    const string message = "anything";

    // Act
    sampleWithLogging.LogInformationMessage(message);

    // Assert
    using (new AssertionScope())
    {
        logger.Collector.Count.Should().Be(1);
        logger.LatestRecord.Level.Should().Be(LogLevel.Information);
        logger.LatestRecord.Message.Should().Be($"This is the template with a {message}."); // rendered message
        logger.LatestRecord.GetStructuredStateValue("{OriginalFormat}").Should().Be("This is the template with a {Message}."); // template
        logger.LatestRecord.GetStructuredStateValue("Message").Should().Be(message); // property
    }
}
```

---

## How do I assert Microsoft.Extensions.Logging ILogger logs with FakeLogger and Dependency Injection?

`services.AddFakeLogging()` registers `ILoggerFactory` and `ILogger<>` (all generics), backed by a single shared
`FakeLogCollector`, which `serviceProvider.GetFakeLogCollector()` hands back for asserting. `.GetSnapshot()` returns
every `FakeLogRecord` in order. Projecting it into an anonymous type and comparing it with `.BeEquivalentTo()` is the
FakeLogger equivalent of `P_TestCorrelator_CompareList`, with no custom extension method needed.

```csharp
public void R_FakeLogger_Via_DependencyInjection()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddFakeLogging(); // registers ILoggerFactory, ILogger<>, and a shared FakeLogCollector
    services.AddSingleton<SampleWithLogging>();
    ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    // Act
    SampleWithLogging sampleWithLogging = serviceProvider.GetRequiredService<SampleWithLogging>();
    sampleWithLogging.LogInformationMessage("first");
    sampleWithLogging.LogInformationMessage("second");

    // Assert
    FakeLogCollector collector = serviceProvider.GetFakeLogCollector();

    collector.GetSnapshot()
        .Select(x => new { x.Level, x.Message })
        .Should().BeEquivalentTo(new[]
        {
            new { Level = LogLevel.Information, Message = "This is the template with a first." },
            new { Level = LogLevel.Information, Message = "This is the template with a second." },
        });
}
```

---

## How does FakeLogger handle a structured (destructured) Object?

Not as well as Serilog does. FakeLogger is the main case where it falls short. Microsoft.Extensions.Logging has no concept of
destructuring, so the `@` in `{@Object}` is just part of the property **name**, and the value is plain `.ToString()`
output (here, an anonymous type's compiler-generated `ToString()`). Compare to `J_Serilog_TestCorrelator_Structured`,
where Serilog captures a real `StructureValue` whose individual properties can be inspected.  

```csharp
public void S_FakeLogger_Structured()
{
    // Arrange
    FakeLogger<SampleWithLogging> logger = new FakeLogger<SampleWithLogging>();

    SampleWithLogging sampleWithLogging = new SampleWithLogging(logger);

    var @object = new
    {
        Name = "John",
        Age = 21,
    };

    // Act
    sampleWithLogging.LogInformationStructuredMessage(@object);

    // Assert
    using (new AssertionScope())
    {
        logger.LatestRecord.GetStructuredStateValue("{OriginalFormat}").Should().Be("This is the template with a {@Object}.");

        // Microsoft.Extensions.Logging has no destructuring - '@' is kept in the key name, and the value is just .ToString()
        logger.LatestRecord.GetStructuredStateValue("@Object").Should().Be("{ Name = John, Age = 21 }");
    }
}
```

---

## NullLogger vs. FakeLogger vs. Serilog + TestCorrelator

All three plug into the same `ILogger<T>` / `ILoggerFactory` constructor parameter, so the class under test never
knows the difference. The choice depends on what the test needs to check about logging.

| | `NullLogger<T>` | `FakeLogger<T>` -> `FakeLogCollector` | Serilog -> `ILogger<T>` -> `TestCorrelator` |
|---|---|---|---|
| **Use when** | Logging is irrelevant to the test; you just need to satisfy the constructor | You want to assert *what* Microsoft.Extensions.Logging logged | Production logs through Serilog, and you want to assert what Serilog actually produces |
| **NuGet** | `Microsoft.Extensions.Logging.Abstractions` (already referenced by anything using `ILogger`) | `Microsoft.Extensions.Diagnostics.Testing` | `Serilog`, `Serilog.Extensions.Logging`, `Serilog.Sinks.TestCorrelator` |
| **Setup (no DI)** | `new NullLogger<T>()` | `new FakeLogger<T>()` | `LoggerConfiguration().WriteTo.TestCorrelator()...CreateLogger()` -> `new SerilogLoggerFactory(...)` -> `new Logger<T>(factory)` |
| **Setup (DI)** | `AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))` | `services.AddFakeLogging()` | Register `SerilogLoggerFactory` **and** `AddSingleton(typeof(ILogger<>), typeof(Logger<>))` |
| **Capturing logs** | None. Everything is discarded | Always on, per logger / per collector | Only inside a `using (TestCorrelator.CreateContext())` block |
| **Reading logs** | n/a | `logger.LatestRecord`, `logger.Collector.GetSnapshot()` | `TestCorrelator.GetLogEventsFromCurrentContext()` |
| **Rendered message** | n/a | `.Message`, string properties unquoted | `.RenderMessage()`, string properties `"quoted"` |
| **Properties** | n/a | `GetStructuredStateValue("Name")`, always `string?` | `.Properties["Name"]`, typed `ScalarValue` / `StructureValue` / ... |
| **Destructured `{@Object}`** | n/a | `.ToString()` only, key keeps the `@` | Full `StructureValue`, each property inspectable |
| **Scopes / enrichers** | n/a | Microsoft.Extensions.Logging scopes only | Serilog enrichers (`.Enrich.FromLogContext()`, etc.), as in production |
| **Parallel-safe** | Yes | Yes. Each `FakeLogger` / `ServiceProvider` owns its own collector | Yes. Each context is isolated via `AsyncLocal` |

**Summary:**
- **`NullLogger<T>`** is the default for the many tests that don't care about logging. Examples `A_` through `E_`.
- **`FakeLogger<T>`** is the simplest way to *assert* on logs. It takes one line of setup, has no extra
configuration and no context block, and it's Microsoft's own package. If the class under test only depends on
Microsoft.Extensions.Logging, start here. It also replaces the Moq approach (`K_` through `M_`), since it records real calls
instead of verifying against `It.IsAnyType` matchers. Examples `Q_` through `S_`.
- **Serilog + TestCorrelator** has more moving parts (three packages, a `LoggerConfiguration`, a `SerilogLoggerFactory`,
an open-generic `Logger<>` registration and a `TestCorrelator` context). In return it tests the logging pipeline
you actually ship, *if* that pipeline is Serilog: destructuring, enrichers, and Serilog's own rendering. If production
doesn't use Serilog, adding Serilog just for tests means asserting on output production never produces. Examples `F_`
through `J_` and `N_` through `P_`.

---

Back to [README](../README.md)
