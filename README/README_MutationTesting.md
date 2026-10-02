# Mutation Testing

## Tools Referenced

- Stryker.NET https://stryker-mutator.io/docs/stryker-net/introduction/ (Apache-2.0) - a **.NET tool**, not a NuGet
package reference. It's pinned at 5.0.0 in the repo's local tool manifest, [`dotnet-tools.json`](../dotnet-tools.json).

All examples are located in `UnitTestingCookbook.Tests` -> [`MutationTestingTests`](../UnitTestingCookbook.Tests/MutationTestingTests.cs)  
Stryker configuration: [`UnitTestingCookbook.Tests/stryker-config.json`](../UnitTestingCookbook.Tests/stryker-config.json)  

---

## What is mutation testing, and why isn't code coverage enough?

Code coverage tells you which lines your tests **executed**. It doesn't tell you whether any test would notice if those
lines were **wrong**.

Mutation testing checks exactly that. Stryker makes many small changes to your production code, one at a time
(`>=` to `>`, `+` to `-`, `true` to `false`, deleting a statement). Each changed version is a **mutant**, and Stryker
runs your tests against each one:

- **Killed** - at least one test failed. Good: your tests noticed the bug.
- **Survived** - every test still passed. The bug went unnoticed: either a missing test or a missing assertion.
- **No Coverage** - no test even ran that code.

**Mutation score** = killed / (killed + survived + no coverage), as a percentage.  

This repo already has a real example of a test that "passed with the wrong code": the original
[`DependencyInjectionTests.B_Scope`](./README_DependencyInjection.md) only asserted that two *different* scopes returned
different instances, so it kept passing when `AddScoped` was changed to `AddTransient`. That bug was found by making
that change by hand. Stryker automates making those changes across your production code.

---

## The code under test

[`ShippingCalculator`](../UnitTestingCookbook.Support/Services/ShippingCalculator.cs) - small, but it has a guard
clause, a boundary (`>=`), and a branch, which is where mutants tend to survive:

<!-- snippet: ShippingCalculator.cs -->
<a id='snippet-ShippingCalculator.cs'></a>
```cs
namespace UnitTestingCookbook.Support.Services;

public static class ShippingCalculator
{
    public const decimal FreeShippingThreshold = 50m;
    public const decimal StandardRate = 5.99m;
    public const decimal ExpressSurcharge = 10m;

    public static decimal Calculate(decimal orderTotal, bool isExpress)
    {
        if (orderTotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderTotal), orderTotal, "Order total cannot be negative.");
        }

        decimal shipping = orderTotal >= FreeShippingThreshold ? 0m : StandardRate;

        return isExpress ? shipping + ExpressSurcharge : shipping;
    }
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/ShippingCalculator.cs#L1-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-ShippingCalculator.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I run Stryker?

```
dotnet tool restore                # once - installs the version pinned in dotnet-tools.json
cd UnitTestingCookbook.Tests
dotnet stryker                     # reads stryker-config.json from the current folder
```

Stryker runs **from the test project folder**. The report goes to `UnitTestingCookbook.Tests/StrykerOutput/<timestamp>/`
(git-ignored), as an HTML file with every mutant shown inline against the source, plus a summary table in the console.

```json
{
  "stryker-config": {
    "project": "UnitTestingCookbook.Support.csproj",
    "mutate": [
      "**/Services/ShippingCalculator.cs"
    ],
    "test-case-filter": "TestCategory=mutationtesting",
    "reporters": [
      "progress",
      "cleartext",
      "html"
    ],
    "thresholds": {
      "high": 80,
      "low": 60,
      "break": 0
    }
  }
}
```

- `project` - **required here.** Stryker mutates one project under test per run, and this test project references
three (Support, TestHelpers, MinimalApi).
- `mutate` - limits mutation to the file this chapter is about. Without it, Stryker mutates all of Support.
- `test-case-filter` - only runs this chapter's tests. Two reasons: Stryker's initial test run must pass
**completely**, and this repo deliberately has `_fails` / `_false_negative` tests; and the full suite takes about a
minute (Polly delays), which Stryker would repeat for every mutant. In Stryker 5.0.0 this option is **config-file only**
(there's no `--test-case-filter` command-line flag).
- `thresholds.break: 0` - never fails the run. Raise it (e.g. `"break": 80`) to make a low score exit non-zero in CI.

---

## Run 1 - only a happy-path test: is "covered" the same as "tested"?

<!-- snippet: MutationTestingTests_A_HappyPath_Only -->
<a id='snippet-MutationTestingTests_A_HappyPath_Only'></a>
```cs
public void A_HappyPath_Only()
{
    // Act
    decimal overThreshold = ShippingCalculator.Calculate(100m, isExpress: false);
    decimal underThreshold = ShippingCalculator.Calculate(10m, isExpress: false);

    // Assert
    using (new AssertionScope())
    {
        overThreshold.Should().Be(0m);
        underThreshold.Should().Be(5.99m);
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/MutationTestingTests.cs#L21-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutationTestingTests_A_HappyPath_Only' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Both branches of the free-shipping ternary run, and the test passes. Running Stryker with **only this test**
gives (real output, Stryker 5.0.0):

| Line | Mutation | Mutated code | Status |
|---|---|---|---|
| 11 | Equality | `orderTotal > 0` | Killed |
| 11 | Equality | `orderTotal <= 0` | **Survived** |
| 11 | Negate expression | `!(orderTotal < 0)` | Killed |
| 13 | Statement | `throw` removed | **No Coverage** |
| 13 | String | exception message `""` | **No Coverage** |
| 16 | Conditional (true) | `true ? 0m : StandardRate` | Killed |
| 16 | Conditional (false) | `false ? 0m : StandardRate` | Killed |
| 16 | Equality | `orderTotal < FreeShippingThreshold` | Killed |
| 16 | Equality | `orderTotal > FreeShippingThreshold` | **Survived** |
| 18 | Conditional (true) | `true ? shipping + ExpressSurcharge : shipping` | Killed |
| 18 | Conditional (false) | `false ? shipping + ExpressSurcharge : shipping` | **Survived** |
| 18 | Arithmetic | `shipping - ExpressSurcharge` | **No Coverage** |

**Mutation score: 50.00%** (6 killed / 12).  

Each survivor points at a specific missing test:
- **`>=` to `>` survived:** nothing tests an order of *exactly* 50. The boundary is untested.
- **`isExpress` forced to `false` survived:** nothing passes `isExpress: true`. The express branch is untested.
- **`< 0` to `<= 0` survived:** nothing tests a total of exactly `0`, so "0 is allowed" isn't tested.
- **The `throw` has no coverage:** nothing tests a negative total.

---

## Run 2 - killing the survivors

One test per gap:

<!-- snippet: MutationTestingTests_B_FreeShipping_Boundary -->
<a id='snippet-MutationTestingTests_B_FreeShipping_Boundary'></a>
```cs
[TestCase(50.00, 0.00)] // exactly at the threshold - only >= gives free shipping
[TestCase(49.99, 5.99)] // just under
public void B_FreeShipping_Boundary(decimal orderTotal, decimal expected)
{
    // Act
    decimal shipping = ShippingCalculator.Calculate(orderTotal, isExpress: false);

    // Assert
    shipping.Should().Be(expected);
}
```
<sup><a href='/UnitTestingCookbook.Tests/MutationTestingTests.cs#L41-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutationTestingTests_B_FreeShipping_Boundary' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: MutationTestingTests_C_Express_AddsSurcharge -->
<a id='snippet-MutationTestingTests_C_Express_AddsSurcharge'></a>
```cs
[TestCase(10.00, 15.99)] // standard rate + express surcharge
[TestCase(100.00, 10.00)] // free shipping + express surcharge
public void C_Express_AddsSurcharge(decimal orderTotal, decimal expected)
{
    // Act
    decimal shipping = ShippingCalculator.Calculate(orderTotal, isExpress: true);

    // Assert
    shipping.Should().Be(expected);
}
```
<sup><a href='/UnitTestingCookbook.Tests/MutationTestingTests.cs#L58-L69' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutationTestingTests_C_Express_AddsSurcharge' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: MutationTestingTests_D_NegativeTotal_Throws -->
<a id='snippet-MutationTestingTests_D_NegativeTotal_Throws'></a>
```cs
public void D_NegativeTotal_Throws()
{
    // Act
    Action act = () => ShippingCalculator.Calculate(-0.01m, isExpress: false);

    // Assert
    act.Should().Throw<ArgumentOutOfRangeException>()
        .WithParameterName("orderTotal");
}
```
<sup><a href='/UnitTestingCookbook.Tests/MutationTestingTests.cs#L76-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutationTestingTests_D_NegativeTotal_Throws' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: MutationTestingTests_E_ZeroTotal_DoesNotThrow -->
<a id='snippet-MutationTestingTests_E_ZeroTotal_DoesNotThrow'></a>
```cs
public void E_ZeroTotal_DoesNotThrow()
{
    // Act
    decimal shipping = ShippingCalculator.Calculate(0m, isExpress: false);

    // Assert
    shipping.Should().Be(5.99m);
}
```
<sup><a href='/UnitTestingCookbook.Tests/MutationTestingTests.cs#L93-L102' title='Snippet source file'>snippet source</a> | <a href='#snippet-MutationTestingTests_E_ZeroTotal_DoesNotThrow' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Running `dotnet stryker` with the committed config (all 5 tests, 7 test cases):

| Line | Mutation | Mutated code | Status |
|---|---|---|---|
| 11 | Equality | `orderTotal > 0` | Killed |
| 11 | Equality | `orderTotal <= 0` | Killed (by `E_`) |
| 11 | Negate expression | `!(orderTotal < 0)` | Killed |
| 13 | Statement | `throw` removed | Killed (by `D_`) |
| 13 | String | exception message `""` | **Survived** |
| 16 | Conditional (true) | `true ? 0m : StandardRate` | Killed |
| 16 | Conditional (false) | `false ? 0m : StandardRate` | Killed |
| 16 | Equality | `orderTotal < FreeShippingThreshold` | Killed |
| 16 | Equality | `orderTotal > FreeShippingThreshold` | Killed (by `B_`) |
| 18 | Conditional (true) | `true ? shipping + ExpressSurcharge : shipping` | Killed |
| 18 | Conditional (false) | `false ? shipping + ExpressSurcharge : shipping` | Killed (by `C_`) |
| 18 | Arithmetic | `shipping - ExpressSurcharge` | Killed (by `C_`) |

**Mutation score: 91.67%** (11 killed / 12) - in about 8 seconds.  

### Not every survivor needs a test

The one remaining survivor replaces the exception **message** with `""`. `D_` asserts the exception **type** and
**parameter name**, which is the actual contract callers depend on, but not the message wording. Adding
`.WithMessage("Order total cannot be negative.*")` would kill this mutant, but then rewording the message would break a test.
Whether that's worth it is a judgment call. Here it's deliberately left surviving, as a reminder that 100% isn't
the goal: **each survivor is a question** ("would I want a test to fail if this changed?"), not automatically a
defect.  

**NOTE:** The 2 mutants Stryker reports as *Ignored* are block-removal mutations it filtered out itself ("block
already covered filter") because other mutants already cover them - nothing to act on.  

---

## Where does mutation testing fit?

- **Cost:** every mutant is a test run. Scope it with `mutate` + `test-case-filter` (as here), or use Stryker's
`--since` / baseline options to only mutate code changed on a branch.
- **Not in this repo's CI:** run on demand. A CI job with `thresholds.break` set works well for a focused, fast
project. On a large suite, run it nightly or only on changed files.
- **Use it alongside coverage, not instead of it:** coverage finds code no test runs; mutation testing finds code that tests run
without actually checking. See [Code Coverage](./README_CodeCoverage.md), which measures the same `ShippingCalculator` both ways.

---

Back to [README](../README.md)
