namespace UnitTestingCookbook.Support.Models;

public class Order
{
    public Guid Id { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Customer { get; set; } = string.Empty;

    public string? InternalNotes { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    public decimal Total => Lines.Sum(x => x.Quantity * x.UnitPrice);
}

public class OrderLine
{
    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
