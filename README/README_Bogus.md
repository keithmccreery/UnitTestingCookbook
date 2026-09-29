# Bogus

## NuGet Packages Referenced

- Bogus https://github.com/bchavez/Bogus

All examples are located in `UnitTestingCookbook.Test` -> [`BogusTest`](../UnitTestingCookbook.Test/BogusTest.cs)

---

## What is Bogus?

A fake data generator for .NET. Instead of hand-typing test literals (`"Blue"`, `60`, ...), you describe the
*shape* of the data once with a `Faker<T>` and generate as many (randomized, but realistic-looking) instances as
you need.

Why bother, when a hand-typed literal works fine? Two reasons:
- **It forces your assertions to test behavior, not coincidence.** A test written against `Length = 60` can
  accidentally pass because of that specific number (off-by-one errors, boundary conditions) without you noticing -
  randomized data surfaces those every run.
- **It scales.** Need 3 test cases or 300? The `Faker<T>` doesn't change; see [How do I feed Bogus-generated data
  into a Data Driven test?](#how-do-i-feed-bogus-generated-data-into-a-data-driven-test) below.

---

## How do I generate fake/randomized test data instead of hand-typed literals?

Using `new Faker<T>()`, describe each property with `.RuleFor()`, then `.Generate()` an instance.

```csharp
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
```

**NOTE:** Because the values are random, the Assert has to check *shape* (is it one of the expected species? is it
in range?) rather than an exact value - that's the trade-off for the extra coverage above.

---

## How do I get reproducible fake data (so a "random" test isn't flaky)?

Bogus data is non-deterministic by default - a `Faker<T>` without a seed generates different values every run.
That's fine when you're only asserting shape/range (as above), but a test that asserts an *exact* generated value
needs `.UseSeed()` to pin the sequence - the same seed always produces the same values.

```csharp
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
```

**NOTE:** `.UseSeed()` on a `Faker<T>` instance is preferred over Bogus's global `Randomizer.Seed` static - it
scopes the seed to this one faker instead of mutating shared state that could affect other tests.

---

## How do I feed Bogus-generated data into a Data Driven test?

See [Data Driven](./README_DataDriven.md) for the `TestCaseSource` pattern itself (the source of truth for that
pattern) - this generates the `TestCaseData`'s values with Bogus instead of hand-typing them, using `.UseSeed()`
(see above) so the generated set - and the resulting test names - are stable across runs.

```csharp
[TestCaseSource(typeof(BogusTestData), nameof(BogusTestData.TestCaseSourceData))]
public void C_TestCaseSourceWithBogus(Whale whale)
{
    // Arrange

    // Act

    // Assert
    whale.Length.Should().BeInRange(10, 100);
}
```

```csharp
// This class can be named anything
public static class BogusTestData
{
    public static IEnumerable TestCaseSourceData
    {
        get
        {
            Faker<Whale> whaleFaker = new Faker<Whale>()
                .UseSeed(42)
                .RuleFor(w => w.Species, f => f.PickRandom(BogusTest.Species))
                .RuleFor(w => w.Length, f => f.Random.Int(10, 100));

            foreach (Whale whale in whaleFaker.Generate(3))
            {
                yield return new TestCaseData(whale)
                    .SetName($"{{m}}[ {whale.Species} Whale, Length of {whale.Length} ]");
            }
        }
    }
}
```

---

Back to [README](../README.md)
