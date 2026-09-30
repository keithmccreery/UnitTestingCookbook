using Serilog;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class TestCorrelatorExtensionsTests
{
    private static List<LogEvent> CaptureLogs(Action<ILogger> act)
    {
        ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TestCorrelator()
            .CreateLogger();

        using (TestCorrelator.CreateContext())
        {
            act(logger);
            return TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        }
    }

    [Test]
    [Category("_passes")]
    public void A_CountAtLevel_CountsOnlyMatchingLevel()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger =>
        {
            logger.Information("info");
            logger.Warning("warning");
            logger.Error("error");
        });

        // Act

        // Assert
        using (new AssertionScope())
        {
            logs.CountAtLevel(LogEventLevel.Information).Should().Be(1);
            logs.CountAtLevel(LogEventLevel.Warning).Should().Be(1);
            logs.CountAtLevel(LogEventLevel.Fatal).Should().Be(0);
        }
    }

    [Test]
    [Category("_passes")]
    public void B_CountWithMessageTemplate_CountsOnlyMatchingTemplate()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger =>
        {
            logger.Information("Hello {Name}", "Alice");
            logger.Information("Hello {Name}", "Bob");
            logger.Information("Goodbye {Name}", "Alice");
        });

        // Act

        // Assert
        logs.CountWithMessageTemplate("Hello {Name}").Should().Be(2);
    }

    [Test]
    [Category("_passes")]
    public void C_HasMessageTemplate_MatchesTemplateNotRenderedText()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger => logger.Information("Hello {Name}", "Alice"));

        // Act

        // Assert
        using (new AssertionScope())
        {
            logs.HasMessageTemplate("Hello {Name}").Should().BeTrue();
            logs.HasMessageTemplate("Hello Alice").Should().BeFalse();
        }
    }

    [Test]
    [Category("_passes")]
    public void D_HasMessage_MatchesRenderedTextNotTemplate()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger => logger.Information("Hello {Name}", "Alice"));

        // Act

        // Assert
        using (new AssertionScope())
        {
            logs.HasMessage("Hello \"Alice\"").Should().BeTrue();
            logs.HasMessage("Hello {Name}").Should().BeFalse();
        }
    }

    [Test]
    [Category("_passes")]
    public void E_WithLevel_FiltersToMatchingEvents()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger =>
        {
            logger.Information("info");
            logger.Warning("warning");
        });

        // Act
        IEnumerable<LogEvent> result = logs.WithLevel(LogEventLevel.Warning);

        // Assert
        result.Should().ContainSingle()
            .Which.Level.Should().Be(LogEventLevel.Warning);
    }

    [Test]
    [Category("_passes")]
    public void F_WithMessageTemplate_FiltersToMatchingEvents()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger =>
        {
            logger.Information("Hello {Name}", "Alice");
            logger.Information("Goodbye {Name}", "Alice");
        });

        // Act
        IEnumerable<LogEvent> result = logs.WithMessageTemplate("Hello {Name}");

        // Assert
        result.Should().ContainSingle();
    }

    [Test]
    [Category("_passes")]
    public void G_GetPropertyValue_UnwrapsScalarValue()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger => logger.Information("Hello {Name}", "Alice"));

        // Act
        string? result = logs.Single().GetPropertyValue<string>("Name");

        // Assert
        result.Should().Be("Alice");
    }

    [Test]
    [Category("_passes")]
    public void H_GetPropertyValue_UnknownProperty_Throws()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger => logger.Information("Hello {Name}", "Alice"));

        // Act
        Action action = () => logs.Single().GetPropertyValue<string>("DoesNotExist");

        // Assert
        action.Should().Throw<KeyNotFoundException>();
    }

    [Test]
    [Category("_passes")]
    public void I_ToSummaries_ProjectsToComparableRecords()
    {
        // Arrange
        List<LogEvent> logs = CaptureLogs(logger => logger.Information("Hello {Name}", "Alice"));

        // Act
        IEnumerable<LogEventSummary> summaries = logs.ToSummaries();

        // Assert
        summaries.Should().BeEquivalentTo(new[]
        {
            new LogEventSummary(LogEventLevel.Information, "Hello {Name}", "Hello \"Alice\""),
        });
    }
}
