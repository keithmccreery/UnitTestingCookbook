using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// Stryker.NET https://stryker-mutator.io/docs/stryker-net/introduction/ - run via `dotnet stryker`, see README_MutationTesting.md
// NOTE: Stryker only runs tests in this category (stryker-config.json "test-case-filter")
//
[Category("unit")]
[Category("mutationtesting")]
[TestFixture]
public class MutationTestingTests
{
    //
    // Q: Is 100% code coverage enough?
    // NOTE: These two tests execute every line of ShippingCalculator.Calculate() except the throw - and still
    //       let most mutants survive. See README_MutationTesting.md, Run 1.
    //
    [Test]
    [Category("_passes")]
    public void A_HappyPath_Only()
    {
        // Act
        decimal overThreshold = ShippingCalculator.Calculate(100m, isExpress: false);
        decimal underThreshold = ShippingCalculator.Calculate(10m, isExpress: false);

        // Assert
        using (new AssertionScope())
        {
            overThreshold.Should().Be(0m);
            underThreshold.Should().Be(5.99m);
        }
    }

    //
    // Q: How do I kill a boundary mutant (>= mutated to >)?
    //
    [TestCase(50.00, 0.00)] // exactly at the threshold - only >= gives free shipping
    [TestCase(49.99, 5.99)] // just under
    [Category("_passes")]
    public void B_FreeShipping_Boundary(decimal orderTotal, decimal expected)
    {
        // Act
        decimal shipping = ShippingCalculator.Calculate(orderTotal, isExpress: false);

        // Assert
        shipping.Should().Be(expected);
    }

    //
    // Q: How do I kill a mutant in a branch no test asserts on?
    //
    [TestCase(10.00, 15.99)] // standard rate + express surcharge
    [TestCase(100.00, 10.00)] // free shipping + express surcharge
    [Category("_passes")]
    public void C_Express_AddsSurcharge(decimal orderTotal, decimal expected)
    {
        // Act
        decimal shipping = ShippingCalculator.Calculate(orderTotal, isExpress: true);

        // Assert
        shipping.Should().Be(expected);
    }

    //
    // Q: How do I kill mutants in a guard clause?
    //
    [Test]
    [Category("_passes")]
    public void D_NegativeTotal_Throws()
    {
        // Act
        Action act = () => ShippingCalculator.Calculate(-0.01m, isExpress: false);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("orderTotal");
    }

    //
    // Q: Does a zero total still pass the guard clause?
    //
    [Test]
    [Category("_passes")]
    public void E_ZeroTotal_DoesNotThrow()
    {
        // Act
        decimal shipping = ShippingCalculator.Calculate(0m, isExpress: false);

        // Assert
        shipping.Should().Be(5.99m);
    }
}
