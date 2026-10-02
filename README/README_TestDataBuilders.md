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

## 1. Object Mother + DeepClone() (Prototype) - what Data Hangover does

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
```

It works, but:
- **It works around the shared state instead of removing it.** Forget one `.DeepClone()` and the bug is back.
- **It copies everything.** [DeepCloner](https://github.com/force-net/DeepCloner) copies the *entire* object graph
using reflection, including anything you didn't mean to copy (services, caches, event handlers).
- **The test doesn't show its data.** What the test depends on lives in `TestValues`, a file away.
- **DeepCloner itself is effectively unmaintained.** Its last release, 0.10.4, was in April 2022, and the repo
hasn't had a push since November 2023 (as of 2026-10). It still works on .NET 10 in this repo, but it's not a
dependency to build new tests on.

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
public void A_ObjectMother_EachCall_IsAFreshObject()
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
public void B_ObjectMother_NamedScenario()
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
public void C_Builder_OnlyWhatMatters()
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
public void D_Builder_VaryOneValue(int downPayment, bool expectedValid)
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
public void E_Builder_EachBuild_IsAFreshObject()
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

**NOTE:** `A_` and `E_` were checked by deliberately breaking the code they protect. Temporarily changing
`OfferMother.Purchase()` and `OfferBuilder.Build()` to return a cached instance makes exactly those two tests
fail, while the other tests keep passing.  

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
public void F_Record_With_Expression()
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

| | Fresh object per test | Shows only relevant data | Scales to many variations | Extra code |
|---|---|---|---|---|
| Object Mother (`static readonly` fields) | **No** - see [Data Hangover](./README_DataHangover.md) | No | No | Low |
| Object Mother + `DeepClone()` | Yes, if you never forget to clone | No | No | Low (+ a package) |
| Object Mother with factory methods | Yes | Somewhat (the method name) | No - one method per variation | Low |
| Test Data Builder | Yes | **Yes** | **Yes** | Medium (one class per type) |
| Records + `with` | Yes, if immutable all the way down | Yes | Yes | None |
| Bogus `Faker<T>` | Yes | Varies | Yes | Low (+ a package) |

**Recommendation:**
- **Records + `with`** when the types are (or can be) immutable records. It needs no extra code.
- **Test Data Builder** for mutable classes or deep object graphs that many tests use.
- **Object Mother with factory methods** for a handful of typical, named scenarios. Combine it with builders
once it grows.
- **Avoid shared `static readonly` test objects**, and treat `DeepClone()` as a workaround, not the design.

---

Back to [README](../README.md)
