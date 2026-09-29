namespace UnitTestingCookbook.Support.Models;

public static class TestValues
{
    public static readonly Dealer DealerValues = new Dealer()
    {
        PostalCode = "12345",
        DealerName = "John Deere",
        DealerId = 1000,
    };

    public static readonly Customer CustomerValues = new Customer()
    {
        CustomerId = 12345,
        CustomerName = "John Doe",
    };

    public static readonly Finance FinanceValues = new Finance()
    {
        PaymentType = "P",
        MaxTerms = 48,
        DownPayment = 1000,
    };
}

public class Offer
{
    public Dealer Dealer { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
    public Finance Finance { get; set; } = default!;
}

public class Finance
{
    public string PaymentType { get; set; } = default!;
    public int MaxTerms { get; set; } = default!;
    public int DownPayment { get; set; } = default!;
}

public class Customer
{
    public int CustomerId { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
}

public class Dealer
{
    public string PostalCode { get; set; } = default!;
    public string DealerName { get; set; } = default!;
    public int DealerId { get; set; } = default!;
}
