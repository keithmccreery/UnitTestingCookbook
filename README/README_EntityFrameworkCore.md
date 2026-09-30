# Entity Framework Core

## NuGet Packages Referenced

- Microsoft.EntityFrameworkCore https://github.com/dotnet/efcore
- Microsoft.EntityFrameworkCore.InMemory
- Microsoft.EntityFrameworkCore.Sqlite

All examples are located in `UnitTestingCookbook.Tests` -> [`EntityFrameworkCoreTests`](../UnitTestingCookbook.Tests/EntityFrameworkCoreTests.cs)

**NOTE:** This chapter's `CatalogDbContext`/`Product` are separate, dedicated types (`UnitTestingCookbook.Support`)
- not reused from elsewhere in the cookbook - kept minimal on purpose, existing purely to give this chapter
something realistic to persist and query.

---

## How do I test code that uses a DbContext, without a real database?

EF Core's `InMemory` provider swaps the storage engine for an in-process dictionary - fast, no external
dependency, good for basic add/query round trips.

```csharp
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
```

**NOTE:** Give each `InMemory` test its own database name (`Guid.NewGuid().ToString()`) - the same name reuses
the same underlying in-process store, so two tests sharing a name would see each other's data.

## Why isn't InMemory enough on its own?

Microsoft's own docs are explicit about this: `InMemory` is *not* a relational database, and it does not enforce
things a real one would - constraints, unique indexes, cascading behavior, transaction semantics, and provider-
specific SQL translation all differ. A configured unique index is a clean, concrete example:

```csharp
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
```

`CatalogDbContext` configures `HasIndex(p => p.Name).IsUnique()` in `OnModelCreating` - a real database would
reject the second insert. `InMemory` lets both through silently. A test suite that only ever runs against
`InMemory` would never catch a uniqueness bug like this.

## How do I get closer-to-real relational behavior, still without a real database server?

Use EF Core's `Sqlite` provider against an **open** `Data Source=:memory:` connection. SQLite is a real
relational engine - constraints, unique indexes, and foreign keys are genuinely enforced - and an in-memory
SQLite database needs no file on disk, no server process, and no Docker.

```csharp
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
```

**NOTE:** The `SqliteConnection` must stay open for the whole test - a `:memory:` SQLite database only exists
for the lifetime of the connection that created it, so closing (or disposing) the connection early destroys it.
This is the same `Data Source=:memory:` technique [Connection String Validation](./README_ConnectionStringValidation.md)
uses, applied here through EF Core instead of raw ADO.NET.

**NOTE:** For true production parity (a specific database engine's exact behavior - Postgres, SQL Server,
etc.), see [Docker](./README_Docker.md)'s Testcontainers pattern - the tradeoff there is a real container
dependency and slower test runs, which is exactly why that chapter's example is disabled by default.

---

Back to [README](../README.md)
