namespace UnitTestingCookbook.Support.Models;

public class ProductReview
{
    public int Id { get; set; }

    public int ProductId { get; set; } // foreign key to Product

    public Product Product { get; set; } = null!;

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;
}
