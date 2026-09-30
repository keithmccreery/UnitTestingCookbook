namespace UnitTestingCookbook.TestHelpers.Tests;

[Category("unit")]
[TestFixture]
public class TestHelperTests
{
    private class Subject
    {
        public string Value { get; }

        internal Subject(string value)
        {
            Value = value;
        }
    }

    [Test]
    [Category("_passes")]
    public void A_InstantiateInternalConstructor_CreatesInstance()
    {
        // Arrange

        // Act
        Subject result = TestHelper.InstantiateInternalConstructor<Subject>("anything");

        // Assert
        result.Value.Should().Be("anything");
    }

    [Test]
    [Category("_passes")]
    public void B_InstantiateInternalConstructor_NoMatchingConstructor_Throws()
    {
        // Arrange

        // Act
        Action action = () => TestHelper.InstantiateInternalConstructor<Subject>(42);

        // Assert
        action.Should().Throw<NotImplementedException>();
    }
}
