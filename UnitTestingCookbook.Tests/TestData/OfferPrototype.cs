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
