namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class ManageEnvironmentVariablesTests
{
    private const string VariableName = "UNITTESTINGCOOKBOOK_TESTHELPERS_TESTS_VAR";

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(VariableName, null);
    }

    [Test]
    [Category("_passes")]
    public void A_SetEnvironmentVariable_SetsTheValue()
    {
        // Arrange
        using ManageEnvironmentVariables manageEnvironmentVariables = new ManageEnvironmentVariables();

        // Act
        manageEnvironmentVariables.SetEnvironmentVariable(VariableName, "new_value");

        // Assert
        Environment.GetEnvironmentVariable(VariableName).Should().Be("new_value");
    }

    [Test]
    [Category("_passes")]
    public void B_Dispose_RestoresOriginalValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable(VariableName, "original_value");

        // Act
        using (ManageEnvironmentVariables manageEnvironmentVariables = new ManageEnvironmentVariables())
        {
            manageEnvironmentVariables.SetEnvironmentVariable(VariableName, "temporary_value");
        }

        // Assert
        Environment.GetEnvironmentVariable(VariableName).Should().Be("original_value");
    }

    [Test]
    [Category("_passes")]
    public void C_Dispose_RestoresToUnsetIfOriginallyUnset()
    {
        // Arrange
        Environment.SetEnvironmentVariable(VariableName, null);

        // Act
        using (ManageEnvironmentVariables manageEnvironmentVariables = new ManageEnvironmentVariables())
        {
            manageEnvironmentVariables.SetEnvironmentVariable(VariableName, "temporary_value");
        }

        // Assert
        Environment.GetEnvironmentVariable(VariableName).Should().BeNull();
    }

    [Test]
    [Category("_passes")]
    public void D_ConstructorWithDictionary_SetsAllValues()
    {
        // Arrange
        Dictionary<string, string?> variables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [VariableName] = "from_constructor",
        };

        // Act
        using ManageEnvironmentVariables manageEnvironmentVariables = new ManageEnvironmentVariables(variables);

        // Assert
        Environment.GetEnvironmentVariable(VariableName).Should().Be("from_constructor");
    }
}
