using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;
using UnitTestingCookbook.Tests.TestData;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("testdatabuilders")]
[TestFixture]
public class TestDataBuildersTests
{
    //
    // Q: How do I clone a prototype and state exactly what's different for this test?
    // NOTE: Force.DeepCloner - see README_DataHangover.md
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I make it impossible to forget DeepClone()?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How does an Object Mother avoid Data Hangover without DeepClone()?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I reuse a named, ready-made scenario?
    //
    [Test]
    [Category("_passes")]
    public void D_ObjectMother_NamedScenario()
    {
        // Arrange
        Offer offer = OfferMother.Lease();

        // Act
        IReadOnlyList<string> errors = OfferValidator.Validate(offer);

        // Assert
        errors.Should().BeEmpty();
    }

    //
    // Q: How do I build test data that shows only what matters to this test?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I vary one value without restating the rest of the object?
    //
    [TestCase(0, true)]
    [TestCase(-1, false)]
    [Category("_passes")]
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

    //
    // Q: Can one builder safely produce several objects?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: Is there a built-in builder for immutable types?
    // NOTE: C# records - `with` copies the record and changes only the named properties
    //
    [Test]
    [Category("_passes")]
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
}
