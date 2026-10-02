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
