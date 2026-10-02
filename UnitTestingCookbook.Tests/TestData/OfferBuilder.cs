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
