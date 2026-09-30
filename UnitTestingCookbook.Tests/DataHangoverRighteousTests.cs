using Force.DeepCloner;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("datahangover")]
// OrderAttribute is obsolete in favor of DependsOnTest/DependsOnFixture, but this chapter's whole point is
// demonstrating a bug that depends on forced, deterministic execution order - kept deliberately, warning
// suppressed below.
#pragma warning disable CS0618
[TestFixture, Order(1)]
public class DataHangoverRighteousTests
{
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
}
#pragma warning restore CS0618
