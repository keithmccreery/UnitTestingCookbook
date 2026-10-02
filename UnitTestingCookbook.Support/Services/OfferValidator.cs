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
