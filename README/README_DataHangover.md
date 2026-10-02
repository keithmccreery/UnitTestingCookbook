# Data Hangover

## NuGet Packages Referenced

- DeepCloner https://github.com/force-net/DeepCloner

All examples are located in `UnitTestingCookbook.Tests` -> [`DataHangoverFailureTests`](../UnitTestingCookbook.Tests/DataHangoverFailureTests.cs) and [`DataHangoverRighteousTests`](../UnitTestingCookbook.Tests/DataHangoverRighteousTests.cs)  

---

## How do I ensure Data Integrity (avoid Data Hangover)?

When using a data set repeatedly, it is a good idea to Deep Clone the data to a new Object reference.
In this example, `static` is used. But `static` is only handling the reference of the Object,
not the data within the Objects, creating Data Hangover (from the previous test).  

**NOTE:** The test `Second_Test_Payment_Fails()` can be run alone, and it will pass.  

**NOTE:** To illustrate this issue, the `OrderAttribute` is necessary.  

### Data Hangover

```csharp
[Test, Order(1)]
[Category("_passes")]
public void First_Test_Lease_Passes()
{
    // Arrange
    Offer offer = new Offer()
    {
        Dealer = TestValues.DealerValues,
        Customer = TestValues.CustomerValues,
        Finance = TestValues.FinanceValues, // PaymentType default is P
    };
    offer.Finance.PaymentType = "L"; // was P

    // Act

    // Assert
    offer.Finance.PaymentType.Should().Be("L");
}

[Test, Order(2)]
[Category("_false_negative")]
public void Second_Test_Payment_Fails()
{
    // Arrange
    Offer offer = new Offer()
    {
        Dealer = TestValues.DealerValues,
        Customer = TestValues.CustomerValues,
        Finance = TestValues.FinanceValues, // PaymentType default is P
    };

    // Act

    // Assert
    offer.Finance.PaymentType.Should().Be("P");
}
```

### Righteous

To avoid the False Negative and Data Hangover,
create a base / original data set,
then Deep Clone the data set,
and make the desired changes on the cloned data to test against.  

**NOTE:** The Righteous tests use the `OrderAttribute` **only** to help demonstrate the solution: they run in the
same execution order that exhibited the problem above (change a property first, then check the original value),
and now pass.  

```csharp
private readonly Offer _originalOffer = new Offer()
{
    Dealer = TestValues.DealerValues,
    Customer = TestValues.CustomerValues,
    Finance = TestValues.FinanceValues,
};

[Test, Order(1)]
[Category("_passes")]
public void First_Test_Lease_Passes()
{
    // Arrange
    Offer offer = _originalOffer.DeepClone(); // Clone original
    offer.Finance.PaymentType = "L"; // was P

    // Act

    // Assert
    offer.Finance.PaymentType.Should().Be("L");
}

[Test, Order(2)]
[Category("_passes")]
public void Second_Test_Payment_Passes()
{
    // Arrange
    Offer offer = _originalOffer.DeepClone(); // Clone original

    // Act

    // Assert
    offer.Finance.PaymentType.Should().Be("P");
}
```

### What these patterns are called

- `TestValues` is an **Object Mother**: a central place holding ready-made test objects. Its objects are
`static readonly`, so they're shared by every test that uses them, which is what makes Data Hangover possible.
- `_originalOffer.DeepClone()` is the **Prototype** pattern: keep one canonical instance and copy it for each test.
Every test starts from the same baseline and then states exactly what it changes (`offer.Finance.PaymentType = "L";`).

**Going one step further:** the Righteous fix asks every test to remember `.DeepClone()`. Making the original
private, and exposing it only through a property that clones on every read, keeps the same "baseline, plus explicit
changes" style while making it impossible to reach the shared instance. [Test Data Builders](./README_TestDataBuilders.md)
shows this as `OfferPrototype`, alongside Object Mother factory methods, Test Data Builders, and C# records with
`with` expressions.

---

Back to [README](../README.md)
