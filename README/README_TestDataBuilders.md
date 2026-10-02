# Test Data Builders

## NuGet Packages Referenced

- None. Everything in this chapter is plain C#.

All examples are located in `UnitTestingCookbook.Tests` -> [`TestDataBuildersTests`](../UnitTestingCookbook.Tests/TestDataBuildersTests.cs)  
The builders themselves are in [`UnitTestingCookbook.Tests/TestData`](../UnitTestingCookbook.Tests/TestData). They're
test-only code, so they live in the test project, not in `UnitTestingCookbook.Support`.  

---

## What problem does this solve?

Most tests need an object graph to work with, and building it inline gets long, repetitive, and hides the one
detail the test is actually about. The usual fix is to share test data, but shared **mutable** objects lead to
[Data Hangover](./README_DataHangover.md) (the source of truth for that bug): one test changes the shared object, and a
later test fails, or worse, passes for the wrong reason.

This chapter shows four ways to share test data **safely**, all using the same `Offer` model as Data Hangover, and
all creating a **new** object graph for every test.

The code under test is [`OfferValidator`](../UnitTestingCookbook.Support/Services/OfferValidator.cs):

```csharp
using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

public static class OfferValidator
{
    public const int MaxLeaseTerms = 36;

    public static IReadOnlyList<string> Validate(Offer offer)
    {
        List<string> errors = [];

        if (offer.Finance.PaymentType is not ("P" or "L"))
        {
            errors.Add("PaymentType must be P (purchase) or L (lease).");
        }

        if (offer.Finance.PaymentType == "L" && offer.Finance.MaxTerms > MaxLeaseTerms)
        {
            errors.Add($"Lease terms cannot exceed {MaxLeaseTerms} months.");
        }

        if (offer.Finance.DownPayment < 0)
        {
            errors.Add("DownPayment cannot be negative.");
        }

        return errors;
    }
}
```

---

## 1. Prototype - clone a baseline, then state what changes

[`TestValues`](../UnitTestingCookbook.Support/Models/TestValues.cs) is an **Object Mother**: a central place for
ready-made test objects. Because its objects are `static readonly` and *shared*, the
[Data Hangover "Righteous" fix](./README_DataHangover.md#righteous) keeps one canonical instance and deep-copies it
for every test. That's the **Prototype** pattern:

```csharp
private readonly Offer _originalOffer = new Offer()
{
    Dealer = TestValues.DealerValues,
    Customer = TestValues.CustomerValues,
    Finance = TestValues.FinanceValues,
};

// in each test
Offer offer = _originalOffer.DeepClone(); // Clone original
offer.Finance.PaymentType = "L"; // was P
```

**Its strength is explicitness.** Every test starts from one known baseline and then **states exactly what's
different**, right there in the test, as plain assignments. There's no builder class to write or maintain, and
any property can be varied without first adding a `With...()` method for it.

**The catch is that it relies on remembering.** Each test has to call `.DeepClone()`. Forget once and that test
mutates the shared original, and Data Hangover is back. That's easy to design away, though, while keeping the same style:
make the original **private**, and expose it only through a property that clones on every read:

```csharp
using Force.DeepCloner;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests.TestData;

//
// Prototype, hardened - the Data Hangover "Righteous" fix (see README_DataHangover.md), but the canonical instance is
// private and the only way to read it is through a property that clones it. Tests keep the "clone, then state exactly
// what changes" style, and forgetting to call DeepClone() is no longer possible.
//
// NOTE: The prototype owns its data (its own literals - deliberately NOT TestValues.DealerValues etc.). Cloning copies
//       whatever the referenced objects hold at that moment, so a prototype built on shared objects that some other
//       test mutates (as DataHangoverFailureTests does to TestValues, on purpose) would hand out corrupted clones.
//
public static class OfferPrototype
{
    private static readonly Offer Prototype = new Offer()
    {
        Dealer = new Dealer() { PostalCode = "12345", DealerName = "John Deere", DealerId = 1000 },
        Customer = new Customer() { CustomerId = 12345, CustomerName = "John Doe" },
        Finance = new Finance() { PaymentType = "P", MaxTerms = 48, DownPayment = 1000 },
    };

    // A fresh deep copy on every read
    public static Offer Offer => Prototype.DeepClone();
}
```

Tests read the same as before, with the baseline plus explicit changes, but there's no longer any way to get the
shared instance:

```csharp
public void A_Prototype_ExplicitChanges()
{
    // Arrange
    Offer offer = OfferPrototype.Offer; // always a fresh deep copy
    offer.Finance.PaymentType = "L"; // the changes this test is about, stated explicitly
    offer.Finance.MaxTerms = 48;

    // Act
    IReadOnlyList<string> errors = OfferValidator.Validate(offer);

    // Assert
    errors.Should().ContainSingle()
        .Which.Should().Be("Lease terms cannot exceed 36 months.");
}
```

```csharp
public void B_Prototype_EachRead_IsAFreshObject()
{
    // Arrange
    Offer first = OfferPrototype.Offer;
    first.Finance.PaymentType = "L"; // a test mutating its data - exactly what caused Data Hangover

    // Act
    Offer second = OfferPrototype.Offer;

    // Assert
    using (new AssertionScope())
    {
        second.Finance.PaymentType.Should().Be("P"); // unaffected
        second.Finance.Should().NotBeSameAs(first.Finance);
    }
}
```

**The prototype must own its data.** An earlier draft of `OfferPrototype` built its prototype from
`TestValues.DealerValues` / `CustomerValues` / `FinanceValues`. It passed when run alone, but **failed** when run
together with `DataHangoverFailureTests`, which mutates `TestValues.FinanceValues` on purpose. Cloning copies
whatever the referenced objects hold *at that moment*, so a prototype built on shared objects that something else
mutates hands out already-corrupted clones. Hence the prototype's own literals above.  

**NOTE:** `B_` was checked by deliberately breaking the code it protects. Temporarily changing the property to
return the shared instance (`=> Prototype;`) makes exactly that test fail.  

**About DeepCloner itself:** [DeepCloner](https://github.com/force-net/DeepCloner) is effectively unmaintained. Its
last release, 0.10.4, was in April 2022, and the repo hasn't had a push since November 2023 (as of 2026-10). It works on
.NET 10 in this repo. With the clone-on-read property, the dependency is limited to **one line**: if a future
runtime ever breaks it, only that line needs to change (to a hand-written copy, for example), not every test.  

---

## 2. Object Mother with factory methods

The simplest fix of all: make the Object Mother's members **methods that build a new graph on every call**,
instead of shared `static readonly` fields. There's nothing to clone, so there's nothing to forget.

```csharp
using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests.TestData;

//
// Object Mother - named, ready-made test objects. Unlike TestValues (see README_DataHangover.md), every call
// builds a brand-new object graph, so a test that mutates what it gets back can't affect any other test.
//
public static class OfferMother
{
    public static Offer Purchase() => new Offer()
    {
        Dealer = new Dealer() { PostalCode = "12345", DealerName = "John Deere", DealerId = 1000 },
        Customer = new Customer() { CustomerId = 12345, CustomerName = "John Doe" },
        Finance = new Finance() { PaymentType = "P", MaxTerms = 48, DownPayment = 1000 },
    };

    public static Offer Lease()
    {
        Offer offer = Purchase();
        offer.Finance.PaymentType = "L";
        offer.Finance.MaxTerms = 36;
        return offer;
    }
}
```

Mutating what one call returns can't affect the next call:

```csharp
public void C_ObjectMother_EachCall_IsAFreshObject()
{
    // Arrange
    Offer first = OfferMother.Purchase();
    first.Finance.PaymentType = "L"; // a test mutating its data - exactly what caused Data Hangover

    // Act
    Offer second = OfferMother.Purchase();

    // Assert
    using (new AssertionScope())
    {
        second.Finance.PaymentType.Should().Be("P"); // unaffected
        second.Finance.Should().NotBeSameAs(first.Finance);
    }
}
```

And a named scenario reads well when the test needs a *typical* object, with no special values:

```csharp
public void D_ObjectMother_NamedScenario()
{
    // Arrange
    Offer offer = OfferMother.Lease();

    // Act
    IReadOnlyList<string> errors = OfferValidator.Validate(offer);

    // Assert
    errors.Should().BeEmpty();
}
```

**Where Object Mothers break down:** every variation needs its own method (`LeaseWithZeroDown()`,
`LeaseWith48Terms()`, `PurchaseForCustomerJane()`, ...). With enough variations the mother becomes a large file of
near-duplicates, which is the problem builders solve.

---

## 3. Test Data Builder

A builder holds a **sensible default for every value**, has a `With...()` method for anything a test might
vary, and `Build()` creates a brand-new graph each time:

```csharp
using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests.TestData;

//
// Test Data Builder - sensible defaults for everything, a With...() method for anything a test might care about,
// and Build() creates a brand-new object graph every time it's called.
//
public sealed class OfferBuilder
{
    private string paymentType = "P";
    private int maxTerms = 48;
    private int downPayment = 1000;
    private string customerName = "John Doe";
    private int dealerId = 1000;

    public OfferBuilder WithPaymentType(string value)
    {
        paymentType = value;
        return this;
    }

    public OfferBuilder WithMaxTerms(int value)
    {
        maxTerms = value;
        return this;
    }

    public OfferBuilder WithDownPayment(int value)
    {
        downPayment = value;
        return this;
    }

    public OfferBuilder WithCustomerName(string value)
    {
        customerName = value;
        return this;
    }

    public OfferBuilder WithDealerId(int value)
    {
        dealerId = value;
        return this;
    }

    // Intention-revealing shortcuts are just pre-set With...() calls
    public OfferBuilder AsLease() => WithPaymentType("L").WithMaxTerms(36);

    public Offer Build() => new Offer()
    {
        Dealer = new Dealer() { PostalCode = "12345", DealerName = "John Deere", DealerId = dealerId },
        Customer = new Customer() { CustomerId = 12345, CustomerName = customerName },
        Finance = new Finance() { PaymentType = paymentType, MaxTerms = maxTerms, DownPayment = downPayment },
    };
}
```

The test now says exactly what's special about its data, and nothing else:

```csharp
public void E_Builder_OnlyWhatMatters()
{
    // Arrange
    Offer offer = new OfferBuilder()
        .AsLease()
        .WithMaxTerms(48) // the one detail this test is about
        .Build();

    // Act
    IReadOnlyList<string> errors = OfferValidator.Validate(offer);

    // Assert
    errors.Should().ContainSingle()
        .Which.Should().Be("Lease terms cannot exceed 36 months.");
}
```

Varying a single value doesn't require restating the rest of the object, so it combines well with
[Data Driven](./README_DataDriven.md) tests:

```csharp
[TestCase(0, true)]
[TestCase(-1, false)]
public void F_Builder_VaryOneValue(int downPayment, bool expectedValid)
{
    // Arrange
    Offer offer = new OfferBuilder()
        .WithDownPayment(downPayment)
        .Build();

    // Act
    IReadOnlyList<string> errors = OfferValidator.Validate(offer);

    // Assert
    errors.Should().HaveCount(expectedValid ? 0 : 1);
}
```

Because `Build()` creates new objects, one builder can safely produce several instances:

```csharp
public void G_Builder_EachBuild_IsAFreshObject()
{
    // Arrange
    OfferBuilder builder = new OfferBuilder().AsLease();

    // Act
    Offer first = builder.Build();
    Offer second = builder.Build();
    first.Finance.MaxTerms = 99;

    // Assert
    using (new AssertionScope())
    {
        second.Should().NotBeSameAs(first);
        second.Finance.Should().NotBeSameAs(first.Finance);
        second.Finance.MaxTerms.Should().Be(36); // unaffected
    }
}
```

**NOTE:** `C_` and `G_` were checked the same way. Temporarily changing `OfferMother.Purchase()` and
`OfferBuilder.Build()` to return a cached instance makes exactly those two tests fail, while the other tests keep
passing.  

**Builder variations worth knowing:**
- **Shortcuts** like `AsLease()` are just preset `With...()` calls, which keeps named scenarios without an Object
Mother.
- **Object Mother + Builder** combine well: `OfferMother` methods can return pre-configured *builders*
(`OfferMother.Lease().WithMaxTerms(48).Build()`).
- **An implicit conversion** (`public static implicit operator Offer(OfferBuilder b) => b.Build();`) lets tests skip the
`.Build()` call. It's convenient, but it makes the conversion easy to miss when reading a test.

---

## 4. C# records + `with` expressions

For **immutable** types, the language has a builder built in. `with` copies a record and changes only the named
properties. The original is never modified, so sharing a default instance is safe:

```csharp
namespace UnitTestingCookbook.Support.Models;

// Immutable counterpart to Finance (see TestValues.cs) - used by the Test Data Builders chapter to show `with` expressions
public sealed record FinanceTerms(string PaymentType, int MaxTerms, int DownPayment);
```

```csharp
public void H_Record_With_Expression()
{
    // Arrange
    FinanceTerms purchase = new FinanceTerms(PaymentType: "P", MaxTerms: 48, DownPayment: 1000);

    // Act
    FinanceTerms lease = purchase with { PaymentType = "L", MaxTerms = 36 };

    // Assert
    using (new AssertionScope())
    {
        lease.Should().Be(new FinanceTerms("L", 36, 1000)); // records compare by value
        purchase.Should().Be(new FinanceTerms("P", 48, 1000)); // original unchanged
    }
}
```

**Watch out:** `with` is a **shallow** copy. If a record has a property of a *mutable* reference type (a `List<T>`,
or a class like `Finance`), the copy and the original share that object, and Data Hangover is back. `with` is only
safe all the way down if the types are immutable all the way down.

---

## Randomized builders: Bogus

A Bogus `Faker<T>` is a builder that fills in **randomized, realistic-looking** values instead of fixed defaults, with
`.RuleFor()` taking the place of `With...()`. See [Bogus](./README_Bogus.md) (the source of truth), including how to
seed it so a test stays reproducible.

---

## Which should I use?

| | Fresh object per test | Shows what this test changes | Scales to many variations | Extra code |
|---|---|---|---|---|
| Object Mother (`static readonly` fields) | **No** - see [Data Hangover](./README_DataHangover.md) | No | No | Low |
| Prototype, `.DeepClone()` in each test | Yes, if every test remembers to clone | **Yes** - explicit assignments | Yes | Low (+ a package) |
| Prototype, clone-on-read property | **Yes** | **Yes** - explicit assignments | Yes | Low (+ a package) |
| Object Mother with factory methods | Yes | Somewhat (the method name) | No - one method per variation | Low |
| Test Data Builder | Yes | **Yes** - `With...()` calls | Yes | Medium (one class per type) |
| Records + `with` | Yes, if immutable all the way down | **Yes** - `with { ... }` | Yes | None |
| Bogus `Faker<T>` | Yes | Varies | Yes | Low (+ a package) |

**Recommendation:**
- **Records + `with`** when the types are (or can be) immutable records. It needs no extra code.
- **Prototype with a clone-on-read property** when you like tests that read as "baseline, plus exactly these changes",
for mutable classes, with almost no extra code. Keep the original private, and let it own its data.
- **Test Data Builder** when you want named, reusable variations (`AsLease()`), or when the object graph needs
logic to stay valid (e.g. setting one value should also adjust a related one).
- **Object Mother with factory methods** for a handful of typical, named scenarios. Combine it with builders
once it grows.
- **Avoid shared `static readonly` test objects that tests use directly.** That's the Data Hangover bug.

---

Back to [README](../README.md)
