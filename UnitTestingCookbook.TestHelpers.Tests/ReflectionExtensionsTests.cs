namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class ReflectionExtensionsTests
{
    private class Subject
    {
        private string PrivateProperty { get; set; } = "property_value";
#pragma warning disable CS0414 // read via reflection in the tests below, not directly
        private string privateField = "field_value";
#pragma warning restore CS0414

        private string PrivateMethod(string message) => $"method_{message}";
    }

    [Test]
    [Category("_passes")]
    public void A_GetPropertyValue_ReturnsPrivatePropertyValue()
    {
        // Arrange
        Subject subject = new Subject();

        // Act
        string? result = subject.GetPropertyValue<string>("PrivateProperty");

        // Assert
        result.Should().Be("property_value");
    }

    [Test]
    [Category("_passes")]
    public void B_GetPropertyValue_UnknownProperty_Throws()
    {
        // Arrange
        Subject subject = new Subject();

        // Act
        Action action = () => subject.GetPropertyValue<string>("DoesNotExist");

        // Assert
        action.Should().Throw<MissingMemberException>();
    }

    [Test]
    [Category("_passes")]
    public void C_GetFieldValue_ReturnsPrivateFieldValue()
    {
        // Arrange
        Subject subject = new Subject();

        // Act
        string? result = subject.GetFieldValue<string>("privateField");

        // Assert
        result.Should().Be("field_value");
    }

    [Test]
    [Category("_passes")]
    public void D_GetFieldValue_UnknownField_Throws()
    {
        // Arrange
        Subject subject = new Subject();

        // Act
        Action action = () => subject.GetFieldValue<string>("doesNotExist");

        // Assert
        action.Should().Throw<MissingFieldException>();
    }

    [Test]
    [Category("_passes")]
    public void E_ExecuteMethod_InvokesPrivateMethod()
    {
        // Arrange
        Subject subject = new Subject();

        // Act
        string? result = subject.ExecuteMethod<string>("PrivateMethod", "anything");

        // Assert
        result.Should().Be("method_anything");
    }

    [Test]
    [Category("_passes")]
    public void F_ExecuteMethod_UnknownMethod_Throws()
    {
        // Arrange
        Subject subject = new Subject();

        // Act
        Action action = () => subject.ExecuteMethod<string>("DoesNotExist", "anything");

        // Assert
        action.Should().Throw<MissingMethodException>();
    }
}
