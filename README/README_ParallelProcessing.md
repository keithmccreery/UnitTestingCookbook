# Parallel Processing

All examples are located in `UnitTestingCookbook.Tests` -> [`ParallelProcessingSafeTests`](../UnitTestingCookbook.Tests/ParallelProcessingSafeTests.cs)
and [`ParallelProcessingUnsafeTests`](../UnitTestingCookbook.Tests/ParallelProcessingUnsafeTests.cs)

**NOTE:** This is a "why", not just a "how" - NUnit makes it easy to opt a fixture into parallel execution, but
this repo's own `TestHelpers` (`ManageEnvironmentVariables`, `ManageCaptureConsole`) are exactly the kind of code
that breaks the moment two tests using them run at the same time. The point of this chapter is to show that
concretely, not just describe it.

---

## How do I mark tests as safe to run in parallel?

Two attributes, both required - `[Parallelizable(ParallelScope.Children)]` on the fixture (its *test methods*
can run in parallel with each other - `ParallelScope.Self` only lets the *fixture itself* run in parallel with
*other* fixtures, not its own methods against each other) and `[assembly: LevelOfParallelism(n)]` somewhere in
the assembly (sizes the worker pool everything marked `[Parallelizable]` shares - without it, nothing actually
parallelizes, no matter what's marked). Both were verified empirically before writing this - `Parallelizable`
alone, or `LevelOfParallelism` alone, produced ordinary sequential execution.

```csharp
[assembly: LevelOfParallelism(4)]
```

```csharp
[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class ParallelProcessingSafeTests
{
    public void A_UsesOnlyLocalState_SafeToRunInParallel()
    {
        // Arrange
        List<int> numbers = new List<int> { 1, 2, 3 };

        // Act
        int sum = numbers.Sum();

        // Assert
        sum.Should().Be(6);
    }
}
```

This is safe because it touches nothing but its own local variables. Nothing here would change if another test
happened to run at the exact same instant.

---

## What actually breaks when tests run in parallel?

Anything that mutates **process-wide** state - and `Environment.SetEnvironmentVariable`/`GetEnvironmentVariable`
is exactly that: one shared table for the whole process, not one per test. `ManageEnvironmentVariables`
([General Tips](./README_GeneralTips.md)) captures whatever value is *currently* there before overwriting it,
and restores that captured value on `Dispose()` - a pattern that only works if scopes are properly nested
(innermost disposed first). Two independent tests running "at the same time" have no such guarantee.

The example below doesn't rely on actual thread-scheduling luck to prove the point - it simulates the exact
interleaving two real parallel tests *could* produce, deterministically, on one thread: two overlapping scopes,
disposed out of nested (LIFO) order.

```csharp
public void A_OverlappingEnvironmentVariableScopes_OutOfOrderDisposal_CorruptsSharedState()
{
    // Arrange
    const string key = "UTC_PARALLEL_DEMO_VARIABLE";
    Environment.SetEnvironmentVariable(key, "original");

    // Act
    ManageEnvironmentVariables testA = new ManageEnvironmentVariables(new Dictionary<string, string?> { [key] = "test-a-value" });
    ManageEnvironmentVariables testB = new ManageEnvironmentVariables(new Dictionary<string, string?> { [key] = "test-b-value" });

    testA.Dispose(); // "Test A" finishes first...

    string? valueWhileTestBStillActive = Environment.GetEnvironmentVariable(key);

    testB.Dispose();
    string? finalValue = Environment.GetEnvironmentVariable(key);

    // Assert
    using (new AssertionScope())
    {
        // WRONG - "Test B" is still supposed to be active here, but sees the pre-test value instead of "test-b-value"
        valueWhileTestBStillActive.Should().Be("original");

        // WRONG - should be back to "original" once both are done, but ended up as "test-a-value" instead
        finalValue.Should().Be("test-a-value");
    }
}
```

Walking through why: `testA` captures `"original"` before setting `"test-a-value"`. `testB` then captures
whatever is *currently* there - `"test-a-value"`, not the true original - before setting `"test-b-value"`.
`testA.Dispose()` restores what *it* captured (`"original"`), silently erasing `testB`'s still-active change.
`testB.Dispose()` then restores what *it* captured (`"test-a-value"`) - never the true original. Both the
value seen mid-test and the value left behind afterward are wrong, and this is a real, deterministic outcome
of the disposal order, not a hypothetical.

---

## How do I make shared-looking state actually safe to parallelize?

Don't mutate the shared, process-wide thing at all - give each test its own instance instead. Compare directly
against the broken example above: same "two overlapping tests, disposed out of order" shape, but using a
per-instance `IConfiguration` ([Dependency Injection](./README_DependencyInjection.md) is the source of truth
for that setup pattern) instead of `Environment.SetEnvironmentVariable`.

```csharp
public void C_PerInstanceConfiguration_OutOfOrderDisposal_DoesNotInterfere()
{
    // Arrange
    IConfiguration configurationA = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["MyKey"] = "test-a-value" })
        .Build();
    IConfiguration configurationB = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["MyKey"] = "test-b-value" })
        .Build();

    // Act - order doesn't matter; neither instance can see or overwrite the other's value
    string? valueForA = configurationA["MyKey"];
    string? valueForB = configurationB["MyKey"];

    // Assert
    using (new AssertionScope())
    {
        valueForA.Should().Be("test-a-value");
        valueForB.Should().Be("test-b-value");
    }
}
```

There's no `using`/`Dispose()` lifecycle here at all, and disposal order is irrelevant, because there's nothing
shared left to corrupt.

---

Back to [README](../README.md)
