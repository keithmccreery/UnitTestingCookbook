using Microsoft.EntityFrameworkCore;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support;

public class CatalogDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(p => p.Name).IsUnique();
    }
}
