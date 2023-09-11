using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Test;

[Category( "unit" )]
[Category( "datahangover" )]
[TestFixture, Order( 2 )]
public class DataHangoverFailureTest
{
    [Test, Order( 1 )]
    [Category( "_passes" )]
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
        offer.Finance.PaymentType.Should().Be( "L" );
    }

    [Test, Order( 2 )]
    [Category( "_false_negative" )]
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
        offer.Finance.PaymentType.Should().Be( "P" );
    }
}
