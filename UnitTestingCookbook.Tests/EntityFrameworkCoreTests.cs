using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using UnitTestingCookbook.Support;
using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[TestFixture]
public class EntityFrameworkCoreTests
{
    [Test]
    [Category("_passes")]
    public void A_InMemory_AddAndQuery_RoundTrips()
    {
        // Arrange
        DbContextOptions<CatalogDbContext> options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using CatalogDbContext dbContext = new CatalogDbContext(options);
        dbContext.Products.Add(new Product { Name = "Widget", Price = 9.99m });
        dbContext.SaveChanges();

        // Act
        Product? result = dbContext.Products.SingleOrDefault(p => p.Name == "Widget");

        // Assert
        result.Should().NotBeNull();
        result!.Price.Should().Be(9.99m);
    }

    [Test]
    [Category("_passes")]
    public void B_InMemory_DoesNotEnforceUniqueIndex()
    {
        // Arrange
        DbContextOptions<CatalogDbContext> options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using CatalogDbContext dbContext = new CatalogDbContext(options);
        dbContext.Products.Add(new Product { Name = "Widget", Price = 1m });
        dbContext.Products.Add(new Product { Name = "Widget", Price = 2m });

        // Act
        Action action = () => dbContext.SaveChanges();

        // Assert - the unique index configured in OnModelCreating is NOT enforced by the InMemory provider
        action.Should().NotThrow();
        dbContext.Products.Count().Should().Be(2);
    }

    [Test]
    [Category("_passes")]
    public void C_Sqlite_EnforcesUniqueIndex_Throws()
    {
        // Arrange
        using SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        connection.Open(); // keep open for the test's duration - the :memory: database is destroyed on close

        DbContextOptions<CatalogDbContext> options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(connection)
            .Options;

        using CatalogDbContext dbContext = new CatalogDbContext(options);
        dbContext.Database.EnsureCreated();
        dbContext.Products.Add(new Product { Name = "Widget", Price = 1m });
        dbContext.Products.Add(new Product { Name = "Widget", Price = 2m });

        // Act
        Action action = () => dbContext.SaveChanges();

        // Assert - Sqlite is a real relational engine, so the unique index is actually enforced
        action.Should().Throw<DbUpdateException>();
    }
}
