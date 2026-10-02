using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

using Moq;

using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.TestCorrelator;

using UnitTestingCookbook.Support;
using UnitTestingCookbook.TestHelpers;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("logging")]
[TestFixture]
public class LoggingTests
{
    //
    // Q: How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLoggerFactory?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_A_ILogger_Via_NullLoggerFactory
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
    // end-snippet

    //
    // Q: How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLogger?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_B_ILogger_Via_NullLogger
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
    // end-snippet

    //
    // Q: How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLoggerFactory and Dependency Injection?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_C_ILogger_Via_DependencyInjection_NullLoggerFactory
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
    // end-snippet

    //
    // Q: How do I Instantiate a Class with Microsoft.Extensions.Logging ILogger via NullLogger and Dependency Injection?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_D_ILogger_Via_DependencyInjection_NullLogger
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
    // end-snippet

    //
    // Q: How do I Instantiate a Class with Microsoft.Extensions.Logging ILoggerFactory via NullLoggerFactory and Dependency Injection?
    // NOTE: New DI Pattern using ILoggerFactory
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_E_ILoggerFactory_Via_DependencyInjection_NullLoggerFactory
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
    // end-snippet

    //
    // Q: How can I use Serilog with Microsoft.Extensions.Logging ILoggerFactory?
    // NOTE: NuGet Serilog.Extensions.Logging
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_F_Serilog_To_ILoggerFactory
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
    // end-snippet

    //
    // Q: How can I use Serilog with Microsoft.Extensions.Logging ILogger?
    // NOTE: NuGet Serilog.Extensions.Logging
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_G_Serilog_To_ILogger
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
    // end-snippet

    //
    // Q: How can I use Serilog with Microsoft.Extensions.Logging ILogger to display Log Context to Console?
    // NOTE: NuGet Serilog.Extensions.Logging
    // NOTE: Serilog.Sinks.TestCorrelator
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_H_Serilog_To_ILogger_Context_To_Console
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
    // end-snippet

    //
    // Q: How can I assert Logs with Serilog.Sinks.TestCorrelator?
    // NOTE: NuGet Serilog.Extensions.Logging
    // NOTE: Serilog.Sinks.TestCorrelator
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_I_Serilog_TestCorrelator
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
    // end-snippet

    //
    // Q: How can I assert Logs with Serilog.Sinks.TestCorrelator with an Object?
    // NOTE: NuGet Serilog.Extensions.Logging
    // NOTE: Serilog.Sinks.TestCorrelator
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_J_Serilog_TestCorrelator_Structured
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
    // end-snippet

    //
    // Q: How do I Mock and Verify Microsoft.Extensions.Logging ILogger? GOOD
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_K_Mock_ILogger_Good
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
    // end-snippet

    //
    // Q: How do I Mock and Verify Microsoft.Extensions.Logging ILogger? BETTER
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_L_Mock_ILogger_Better
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
    // end-snippet

    //
    // Q: How do I Mock and Verify Microsoft.Extensions.Logging ILogger? BEST
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_M_Mock_ILogger_Best
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
    // end-snippet

    //
    // Q: How do I count TestCorrelator log events - overall, by severity, or by message template?
    // NOTE: UnitTestingCookbook.TestHelpers.TestCorrelatorExtensions
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_N_TestCorrelator_Count
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
    // end-snippet

    //
    // Q: How do I check whether a particular message was logged, without pulling it out and asserting on it manually?
    // NOTE: UnitTestingCookbook.TestHelpers.TestCorrelatorExtensions
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_O_TestCorrelator_HasMessage
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
    // end-snippet

    //
    // Q: How do I compare a whole list of expected log events at once?
    // NOTE: UnitTestingCookbook.TestHelpers.TestCorrelatorExtensions
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_P_TestCorrelator_CompareList
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
    // end-snippet

    //
    // Q: How do I assert Microsoft.Extensions.Logging ILogger logs with FakeLogger, without Serilog or a Mock?
    // NOTE: NuGet Microsoft.Extensions.Diagnostics.Testing
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_Q_FakeLogger
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
    // end-snippet

    //
    // Q: How do I assert Microsoft.Extensions.Logging ILogger logs with FakeLogger and Dependency Injection?
    // NOTE: NuGet Microsoft.Extensions.Diagnostics.Testing
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_R_FakeLogger_Via_DependencyInjection
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
    // end-snippet

    //
    // Q: How does FakeLogger handle a structured (destructured) Object?
    // NOTE: NuGet Microsoft.Extensions.Diagnostics.Testing
    // NOTE: Compare to J_Serilog_TestCorrelator_Structured - FakeLogger only keeps the string form
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: LoggingTests_S_FakeLogger_Structured
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
    // end-snippet
}
