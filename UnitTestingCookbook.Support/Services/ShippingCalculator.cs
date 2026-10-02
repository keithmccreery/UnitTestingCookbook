namespace UnitTestingCookbook.Support.Services;

public static class ShippingCalculator
{
    public const decimal FreeShippingThreshold = 50m;
    public const decimal StandardRate = 5.99m;
    public const decimal ExpressSurcharge = 10m;

    public static decimal Calculate(decimal orderTotal, bool isExpress)
    {
        if (orderTotal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderTotal), orderTotal, "Order total cannot be negative.");
        }

        decimal shipping = orderTotal >= FreeShippingThreshold ? 0m : StandardRate;

        return isExpress ? shipping + ExpressSurcharge : shipping;
    }
}
