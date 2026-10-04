using Microsoft.EntityFrameworkCore;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support;

public class CatalogDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductReview> Reviews => Set<ProductReview>();

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(p => p.Name).IsUnique();

        // Restrict, not EF Core's default Cascade: deleting a product that still has reviews fails, as it does in many
        // real schemas. That's what makes the order of test-data cleanup matter (see README_Respawn.md).
        modelBuilder.Entity<ProductReview>()
            .HasOne(review => review.Product)
            .WithMany()
            .HasForeignKey(review => review.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
