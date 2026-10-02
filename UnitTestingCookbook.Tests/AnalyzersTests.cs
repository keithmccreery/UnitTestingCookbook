using NUnit.Framework.Legacy;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("analyzers")]
[TestFixture]
public class AnalyzersTests
{
    //
    // AwesomeAssertions.Analyzers
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AnalyzersTests_A_AwesomeAssertions
    public void A_AwesomeAssertions()
    {
        // Arrange
        List<bool> list = new List<bool>() { true, true };

        // Act

        // Assert
        ClassicAssert.IsTrue(list.All(b => b)); // NUnit 4+: classic Assert.* moved to NUnit.Framework.Legacy.ClassicAssert
        Assert.That(list.All(b => b));

        list.All(b => b).Should().BeTrue();

        list.Should().OnlyContain(b => b);
    }
    // end-snippet

    //
    // False-Positive
    //
    [Test]
    [Category("_false_positive")]
    // begin-snippet: AnalyzersTests_B_False_Positive
    public void B_False_Positive()
    {
        // Arrange
        string? name = null;

        // Act

        // Assert
        name?.Should().NotBeNull();
    }
    // end-snippet
}
