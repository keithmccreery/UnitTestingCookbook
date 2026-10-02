using System.Collections;

using Bogus;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

//
// Bogus https://github.com/bchavez/Bogus
//
[Category("unit")]
[Category("bogus")]
[TestFixture]
public class BogusTests
{
    internal static readonly string[] Species = ["Blue", "Humpback", "Beluga", "Orca"];

    //
    // Q: How do I generate fake/randomized test data instead of hand-typed literals?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: BogusTests_A_FakerBasics
    public void A_FakerBasics()
    {
        // Arrange
        Faker<Whale> whaleFaker = new Faker<Whale>()
            .RuleFor(w => w.Species, f => f.PickRandom(Species))
            .RuleFor(w => w.Length, f => f.Random.Int(10, 100));

        // Act
        Whale whale = whaleFaker.Generate();

        // Assert
        using (new AssertionScope())
        {
            // the exact values are random - assert on shape/range, not equality
            whale.Species.Should().BeOneOf(Species);
            whale.Length.Should().BeInRange(10, 100);
        }
    }
    // end-snippet

    //
    // Q: How do I get reproducible fake data (so a "random" test isn't flaky)?
    //
    // NOTE: Bogus data is non-deterministic by default - a Faker<T> without a seed generates
    // different values every run. That's fine when you're only asserting shape/range (as above),
    // but a test that asserts an exact generated value needs `.UseSeed()` to pin the sequence -
    // the same seed always produces the same values.
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: BogusTests_B_DeterministicSeed
    public void B_DeterministicSeed()
    {
        // Arrange
        Faker<Whale> whaleFaker = new Faker<Whale>()
            .UseSeed(8675309)
            .RuleFor(w => w.Species, f => f.PickRandom(Species))
            .RuleFor(w => w.Length, f => f.Random.Int(10, 100));

        // Act
        Whale whale = whaleFaker.Generate();

        // Assert
        using (new AssertionScope())
        {
            whale.Species.Should().Be("Orca");
            whale.Length.Should().Be(100);
        }
    }
    // end-snippet

    //
    // Q: How do I feed Bogus-generated data into a Data Driven test?
    //
    // See Data Driven (README_DataDriven.md) for the TestCaseSource pattern itself - this generates
    // the TestCaseData's values with Bogus instead of hand-typing them, using .UseSeed() (see above)
    // so the generated set - and the resulting test names - are stable across runs.
    //
    [Category("_passes")]
    // begin-snippet: BogusTests_C_TestCaseSourceWithBogus
    [TestCaseSource(typeof(BogusTestData), nameof(BogusTestData.TestCaseSourceData))]
    public void C_TestCaseSourceWithBogus(Whale whale)
    {
        // Arrange

        // Act

        // Assert
        whale.Length.Should().BeInRange(10, 100);
    }
    // end-snippet
}

// begin-snippet: BogusTests_BogusTestData
// This class can be named anything
public static class BogusTestData
{
    public static IEnumerable TestCaseSourceData
    {
        get
        {
            Faker<Whale> whaleFaker = new Faker<Whale>()
                .UseSeed(42)
                .RuleFor(w => w.Species, f => f.PickRandom(BogusTests.Species))
                .RuleFor(w => w.Length, f => f.Random.Int(10, 100));

            foreach (Whale whale in whaleFaker.Generate(3))
            {
                yield return new TestCaseData(whale)
                    .SetName($"{{m}}[ {whale.Species} Whale, Length of {whale.Length} ]");
            }
        }
    }
}
// end-snippet
