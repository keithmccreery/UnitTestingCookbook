using CsCheck;

using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// CsCheck https://github.com/AnthonyLloyd/CsCheck - see README_PropertyBasedTesting.md
// A property is an ordinary NUnit test: .Sample(...) runs it against 100 generated inputs (by default) and throws,
// with the simplest failing input it can shrink to, if any of them return false.
//
[Category("unit")]
[Category("propertybased")]
[TestFixture]
public class PropertyBasedTestingTests
{
    // Order totals in whole cents, from $0.00 to $1,000.00. Generating cents (not raw decimals) keeps values realistic
    // and lets shrinking walk toward simple values like 50.00.
    private static readonly Gen<decimal> GenOrderTotal = Gen.Int[0, 100_000].Select(cents => cents / 100m);

    //
    // Q: How do I state a rule that must hold for every input, instead of picking examples?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_A_Shipping_IsNeverNegative
    public void A_Shipping_IsNeverNegative()
    {
        Gen.Select(GenOrderTotal, Gen.Bool)
            .Sample((orderTotal, isExpress) => ShippingCalculator.Calculate(orderTotal, isExpress) >= 0m);
    }
    // end-snippet

    //
    // Q: How do I test a relationship between two calls, when I don't want to restate the exact expected value?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_B_Express_CostsExactlyTheSurchargeMore
    public void B_Express_CostsExactlyTheSurchargeMore()
    {
        GenOrderTotal.Sample(orderTotal =>
            ShippingCalculator.Calculate(orderTotal, isExpress: true) - ShippingCalculator.Calculate(orderTotal, isExpress: false)
                == ShippingCalculator.ExpressSurcharge);
    }
    // end-snippet

    //
    // Q: How do I test a rule like "free shipping if and only if the order is at least $50"?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_C_FreeShipping_IfAndOnlyIf_AtThreshold
    public void C_FreeShipping_IfAndOnlyIf_AtThreshold()
    {
        GenOrderTotal.Sample(orderTotal =>
            (ShippingCalculator.Calculate(orderTotal, isExpress: false) == 0m)
                == (orderTotal >= ShippingCalculator.FreeShippingThreshold));
    }
    // end-snippet

    //
    // Q: What does a failing property look like? (Fails by design - excluded from CI.)
    // NOTE: "Shipping always costs something" is a wrong assumption - orders of $50 or more ship free.
    //
    [Test]
    [Category("_fails")]
    // begin-snippet: PropertyBasedTestingTests_D_WrongAssumption_ShrinksToTheBoundary
    public void D_WrongAssumption_ShrinksToTheBoundary()
    {
        GenOrderTotal.Sample(orderTotal => ShippingCalculator.Calculate(orderTotal, isExpress: false) > 0m, iter: 10_000);
    }
    // end-snippet
}
