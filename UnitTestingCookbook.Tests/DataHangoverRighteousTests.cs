using Force.DeepCloner;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("datahangover")]
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
