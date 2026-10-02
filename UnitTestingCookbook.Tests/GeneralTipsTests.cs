using System.Diagnostics;

using UnitTestingCookbook.Support;
using UnitTestingCookbook.TestHelpers;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("general_tips")]
[TestFixture]
public class GeneralTipsTests
{
    //
    // Q: How do I Capture Console to test?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_A_CaptureConsole
    public void A_CaptureConsole()
    {
        using (StringWriter capturedConsole = new StringWriter())
        {
            TextWriter originalOutput = System.Console.Out;

            try
            {
                System.Console.SetOut(capturedConsole);

                // Arrange
                string expected = "Hello World" + System.Environment.NewLine;

                // Act
                System.Console.WriteLine("Hello World");

                string result = capturedConsole.ToString();

                // Assert
                result.Should().Be(expected);
            }
            finally
            {
                System.Console.SetOut(originalOutput);
            }
        }
    }
    // end-snippet

    //
    // Q: How do I Capture Console to test?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_A_CaptureConsole_TestHelpers
    public void A_CaptureConsole_TestHelpers()
    {
        // Arrange
        using ManageCaptureConsole capturedConsole = new ManageCaptureConsole();

        string expected = "Hello World" + System.Environment.NewLine;

        // Act
        System.Console.WriteLine("Hello World");

        string? result = capturedConsole.ToString();

        // Assert
        result.Should().Be(expected);
    }
    // end-snippet

    //
    // Q: How do I change ENVIRONMENT variables for testing?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_B_EnvironmentVariables
    public void B_EnvironmentVariables()
    {
        const string ASPNETCORE_ENVIRONMENT = "ASPNETCORE_ENVIRONMENT";
        const string AWS_DEFAULT_REGION = "AWS_DEFAULT_REGION";

        Dictionary<string, string?> originalValues = new Dictionary<string, string?>();

        try
        {
            // Arrange
            originalValues[ASPNETCORE_ENVIRONMENT] = Environment.GetEnvironmentVariable(ASPNETCORE_ENVIRONMENT);
            originalValues[AWS_DEFAULT_REGION] = Environment.GetEnvironmentVariable(AWS_DEFAULT_REGION);

            // Act
            Environment.SetEnvironmentVariable(ASPNETCORE_ENVIRONMENT, "QA");
            Environment.SetEnvironmentVariable(AWS_DEFAULT_REGION, "us-east-1");

            const bool result = true; // Do some work

            // Assert
            result.Should().BeTrue(); // Check the work
        }
        finally
        {
            // If value is null, the environment variable will be deleted.
            originalValues
                .ToList()
                .ForEach(kvp => Environment.SetEnvironmentVariable(kvp.Key, kvp.Value));
        }
    }
    // end-snippet

    //
    // Q: How do I change ENVIRONMENT variables for testing?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_B_EnvironmentVariables_TestHelpers
    public void B_EnvironmentVariables_TestHelpers()
    {
        // Arrange
        Dictionary<string, string?> environmentVariables = new Dictionary<string, string?>()
        {
            { "ASPNETCORE_ENVIRONMENT", "QA" },
            { "AWS_DEFAULT_REGION", "us-east-1" }
        };

        using ManageEnvironmentVariables manageEnvironmentVariables = new ManageEnvironmentVariables(environmentVariables);

        // Act
        const bool result = true; // Do some work

        // Assert
        result.Should().BeTrue(); // Check the work
    }
    // end-snippet

    //
    // CAUTION: C_Private_Property, D_Private_Field, and E_Private_Method below use reflection to reach into a
    // class's private implementation. This is useful as a last resort (e.g. testing legacy code you can't safely
    // change the shape of), but reaching for it often is usually a sign the class needs a seam - a constructor
    // parameter, an extracted interface, an internal member exposed via InternalsVisibleTo (see F_Internal_Constructor
    // below) - rather than a test that reaches past the public API.
    //
    // Q: How do I access a private Property?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_C_Private_Property
    public void C_Private_Property()
    {
        // Arrange
        Miscellaneous miscellaneous = new Miscellaneous();

        // Act
        string? result = miscellaneous.GetPropertyValue<string>("PrivateProperty");

        // Assert
        result.Should().Be("private_property");
    }
    // end-snippet

    //
    // Q: How do I access a private Field?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_D_Private_Field
    public void D_Private_Field()
    {
        // Arrange
        Miscellaneous miscellaneous = new Miscellaneous();

        // Act
        string? result = miscellaneous.GetFieldValue<string>("privateField");

        // Assert
        result.Should().Be("private_field");
    }
    // end-snippet

    //
    // Q: How do I invoke a private Method?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_E_Private_Method
    public void E_Private_Method()
    {
        // Arrange
        Miscellaneous miscellaneous = new Miscellaneous();

        // Act
        bool result = miscellaneous.ExecuteMethod<bool>("PrivateMethod", "a message");

        // Assert
        result.Should().BeTrue();
    }
    // end-snippet

    //
    // Q: How do I access an Internal Constructor (for unit testing)?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_F_Internal_Constructor
    public void F_Internal_Constructor()
    {
        // Arrange

        // Act
        Miscellaneous miscellaneous = TestHelper.InstantiateInternalConstructor<Miscellaneous>("default_private_property_value");

        // Assert
        miscellaneous.Should().NotBeNull();
    }
    // end-snippet

    //
    // Q: How do I Pretty Print a Stack Trace
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: GeneralTipsTests_G_Demystifier
    public void G_Demystifier()
    {
        // Arrange

        // Act
        try
        {
            false.Should().BeTrue();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine("Before...");
            System.Console.WriteLine(ex.ToString());

            ex.Demystify();
            System.Console.WriteLine();

            System.Console.WriteLine("After...");
            System.Console.WriteLine(ex.ToString());
        }

        // Assert
    }
    // end-snippet
}
