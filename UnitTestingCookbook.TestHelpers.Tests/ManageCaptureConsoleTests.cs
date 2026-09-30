namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class ManageCaptureConsoleTests
{
    [Test]
    [Category("_passes")]
    public void A_CapturesConsoleOutputWrittenWhileActive()
    {
        // Arrange
        using ManageCaptureConsole captureConsole = new ManageCaptureConsole();

        // Act
        Console.WriteLine("captured line");

        // Assert
        captureConsole.ToString().Should().Contain("captured line");
    }

    [Test]
    [Category("_passes")]
    public void B_RestoresOriginalConsoleOutputOnDispose()
    {
        // Arrange
        TextWriter originalOut = Console.Out;

        // Act
        using (new ManageCaptureConsole())
        {
            Console.Out.Should().NotBeSameAs(originalOut);
        }

        // Assert
        Console.Out.Should().BeSameAs(originalOut);
    }
}
