using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("datahangover")]
// OrderAttribute is obsolete in favor of DependsOnTest/DependsOnFixture, but this chapter's whole point is
// demonstrating a bug that depends on forced, deterministic execution order - kept deliberately, warning
// suppressed below.
#pragma warning disable CS0618
[TestFixture, Order(2)]
public class DataHangoverFailureTests
{
    // begin-snippet: DataHangoverFailureTests_Tests
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
    // end-snippet
}
#pragma warning restore CS0618
