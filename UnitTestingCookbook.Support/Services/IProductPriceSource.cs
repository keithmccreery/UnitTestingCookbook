namespace UnitTestingCookbook.Support.Services;

// The slow, expensive thing worth caching - e.g. a pricing API or a database query.
public interface IProductPriceSource
{
    Task<decimal> GetPriceAsync(string sku, CancellationToken cancellationToken = default);
}
