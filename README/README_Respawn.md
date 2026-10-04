# Respawn (Resetting a Test Database)

## NuGet Packages Referenced

- Respawn https://github.com/jbogard/Respawn (Apache-2.0)
- Testcontainers.PostgreSql https://dotnet.testcontainers.org and Npgsql.EntityFrameworkCore.PostgreSQL (the PostgreSQL example only)

All examples are located in `UnitTestingCookbook.Tests` -> [`RespawnTests`](../UnitTestingCookbook.Tests/RespawnTests.cs)  

---

## What problem does Respawn solve?

[Data Hangover](./README_DataHangover.md) (the source of truth for the idea) happens with objects in memory - one test
changes shared data, and a later test sees the change. **A database shared by tests has exactly the same problem**,
just with rows: whatever one test inserts is still there when the next test runs. That breaks tests that count rows,
insert something with a unique value, or expect a table to start empty - and *which* tests break depends on the order
they happen to run in.

There are a few ways to give every test a clean database:

| Approach | Good | Not so good |
|---|---|---|
| **A fresh in-memory SQLite database per test** | Fastest and simplest - the [Entity Framework Core](./README_EntityFrameworkCore.md) chapter does this | Only if SQLite can stand in for your real database |
| **Recreate the database per test** (`EnsureDeleted` + `EnsureCreated`, or re-running migrations) | Always a perfectly clean schema | Gets slower as the schema grows; easy to trip over (see the PostgreSQL notes below) |
| **Roll back a transaction per test** | Fast; nothing is ever committed | Breaks as soon as the code under test opens its own connection or commits - e.g. a `WebApplicationFactory` integration test |
| **Respawn** - keep the schema, empty the tables | Fast; works with any number of connections; keeps reference data if asked | The database (and its schema) must already exist |

Respawn is for the common case of a **real, shared test database**: it inspects the schema once, works out how to
empty every table despite foreign keys, and then resets the database in one round trip before each test.

---

## Setting it up

One database for the whole fixture, created once; Respawn resets it before every test. (SQLite is used here so these
tests run anywhere, including CI, with no Docker - the PostgreSQL example further down shows the same thing against a
real database server.)

<!-- snippet: RespawnTests_Setup -->
<a id='snippet-RespawnTests_Setup'></a>
```cs
// One database for the whole fixture - created once, like a real test database would be.
[OneTimeSetUp]
public async Task OneTimeSetUp()
{
    _connection = new SqliteConnection("Data Source=:memory:"); // an in-memory SQLite database lives as long as this connection
    await _connection.OpenAsync();

    _options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(_connection).Options;
    await using CatalogDbContext dbContext = new CatalogDbContext(_options);
    await dbContext.Database.EnsureCreatedAsync();

    // Respawn inspects the schema once, here, and works out how to empty every table
    _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
    {
        DbAdapter = DbAdapter.Sqlite, // detected automatically for SQL Server, PostgreSQL, MySQL, Oracle, and Informix - not SQLite
    });
}

// ...and emptied before every test, so no test sees another test's rows
[SetUp]
public Task SetUp() => _respawner.ResetAsync(_connection);

[OneTimeTearDown]
public async Task OneTimeTearDown()
{
    await _connection.DisposeAsync();
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L28-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_Setup' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**NOTE:** Respawn detects the database type from the connection for SQL Server, PostgreSQL, MySQL, Oracle, and
Informix. For SQLite (supported since Respawn 7.0), the `DbAdapter` has to be set explicitly.  

---

## How do I give every test an empty database, without recreating it?

Both tests insert a product named `"Widget"`, and `Product.Name` has a unique index. Whichever test runs second only
passes because `[SetUp]` emptied the database in between:

<!-- snippet: RespawnTests_A_EachTest_StartsEmpty -->
<a id='snippet-RespawnTests_A_EachTest_StartsEmpty'></a>
```cs
public async Task A_EachTest_StartsEmpty()
{
    // Act
    await AddProductWithReviewAsync("Widget");

    // Assert
    await using CatalogDbContext dbContext = new CatalogDbContext(_options);
    (await dbContext.Products.CountAsync()).Should().Be(1);
    (await dbContext.Reviews.CountAsync()).Should().Be(1);
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L75-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_A_EachTest_StartsEmpty' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: RespawnTests_B_EachTest_StartsEmpty_Again -->
<a id='snippet-RespawnTests_B_EachTest_StartsEmpty_Again'></a>
```cs
public async Task B_EachTest_StartsEmpty_Again()
{
    // Act - the same unique product name as A_
    await AddProductWithReviewAsync("Widget");

    // Assert
    await using CatalogDbContext dbContext = new CatalogDbContext(_options);
    (await dbContext.Products.CountAsync()).Should().Be(1);
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L90-L100' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_B_EachTest_StartsEmpty_Again' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Checked by breaking it:** with the `[SetUp]` reset temporarily removed, the later tests fail with
`SQLite Error 19: 'UNIQUE constraint failed: Products.Name'` - rows left behind by an earlier test.  

---

## Why not just DELETE FROM every table?

Because of foreign keys. In [`CatalogDbContext`](../UnitTestingCookbook.Support/CatalogDbContext.cs), each
`ProductReview` points at a `Product`, and the relationship is configured with `OnDelete(DeleteBehavior.Restrict)` -
deleting a product that still has reviews fails, as it does in many real schemas. (EF Core's *default* for a required
relationship is `Cascade`, where deleting the product would silently delete its reviews too, and the order wouldn't
matter.) So a hand-written cleanup that deletes the parent table first fails:

<!-- snippet: RespawnTests_C_ForeignKeys_MakeTheOrderMatter -->
<a id='snippet-RespawnTests_C_ForeignKeys_MakeTheOrderMatter'></a>
```cs
public async Task C_ForeignKeys_MakeTheOrderMatter()
{
    // Arrange
    await AddProductWithReviewAsync("Widget");

    // Act - delete the parent table first, the way a hand-written cleanup often does
    Func<Task> naiveCleanup = async () =>
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM \"Products\";";
        await command.ExecuteNonQueryAsync();
    };

    // Assert - the review still points at the product, and the relationship is Restrict
    await naiveCleanup.Should().ThrowAsync<SqliteException>().WithMessage("*FOREIGN KEY constraint failed*");

    // Respawn gets around the foreign keys for you (on SQLite, by switching foreign-key checks off while it deletes)
    await _respawner.ResetAsync(_connection);

    await using CatalogDbContext dbContext = new CatalogDbContext(_options);
    (await dbContext.Products.CountAsync()).Should().Be(0);
    (await dbContext.Reviews.CountAsync()).Should().Be(0);
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L107-L131' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_C_ForeignKeys_MakeTheOrderMatter' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A hand-written cleanup has to list every table, in the right order, and be kept up to date as the schema changes.
Respawn reads the schema and handles the foreign keys itself - differently for each database:

- **SQLite** - switches foreign-key checks off, deletes, and switches them back on (`PRAGMA foreign_keys = OFF; ...`) -
seen in its generated `DeleteSql`.
- **PostgreSQL** - one `TRUNCATE ... CASCADE` statement for every table - seen in the PostgreSQL example below.
- **SQL Server** - a `DELETE` per table, in dependency order (children before parents), only disabling constraints for
tables with circular relationships - from Respawn 7.0.0's source, not run here.

`respawner.DeleteSql` shows exactly what Respawn will run against your database.

---

## How do I keep reference data (lookup tables, seed data) between tests?

`TablesToIgnore` leaves those tables alone. (There's also `TablesToInclude`, and `SchemasToInclude` /
`SchemasToExclude` for whole schemas.)

<!-- snippet: RespawnTests_D_TablesToIgnore_KeepsReferenceData -->
<a id='snippet-RespawnTests_D_TablesToIgnore_KeepsReferenceData'></a>
```cs
public async Task D_TablesToIgnore_KeepsReferenceData()
{
    // Arrange - treat Products as reference data that every test expects to be there
    await AddProductWithReviewAsync("Widget");

    Respawner keepProducts = await Respawner.CreateAsync(_connection, new RespawnerOptions
    {
        DbAdapter = DbAdapter.Sqlite,
        TablesToIgnore = [new Table("Products")],
    });

    // Act
    await keepProducts.ResetAsync(_connection);

    // Assert
    await using CatalogDbContext dbContext = new CatalogDbContext(_options);
    (await dbContext.Products.CountAsync()).Should().Be(1); // kept
    (await dbContext.Reviews.CountAsync()).Should().Be(0); // emptied
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L138-L158' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_D_TablesToIgnore_KeepsReferenceData' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How does this look against a real database server?

The same idea against PostgreSQL 17, in a throwaway container via [Testcontainers](./README_Docker.md) (the source of
truth for Testcontainers). Respawn detects PostgreSQL from the connection on its own.

<!-- snippet: RespawnTests_E_PostgreSql -->
<a id='snippet-RespawnTests_E_PostgreSql'></a>
```cs
public async Task E_PostgreSql()
{
    // Arrange - a throwaway PostgreSQL 17 container. WithDatabase matters: the default database is "postgres",
    // PostgreSQL's own maintenance database, which EF Core can't drop and shouldn't be testing against anyway.
    await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("catalog").Build();
    await postgres.StartAsync();

    DbContextOptions<CatalogDbContext> options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
    await using (CatalogDbContext dbContext = new CatalogDbContext(options))
    {
        await dbContext.Database.EnsureCreatedAsync();
        Product product = new Product { Name = "Widget", Price = 9.99m };
        await dbContext.Products.AddAsync(product);
        await dbContext.SaveChangesAsync();
        await dbContext.Reviews.AddAsync(new ProductReview { ProductId = product.Id, Rating = 5 });
        await dbContext.SaveChangesAsync();
    }

    await using NpgsqlConnection connection = new NpgsqlConnection(postgres.GetConnectionString());
    await connection.OpenAsync();

    // No DbAdapter needed - Respawn detects PostgreSQL from the connection
    Respawner respawner = await Respawner.CreateAsync(connection, new RespawnerOptions { SchemasToInclude = ["public"] });

    // Act
    await respawner.ResetAsync(connection);

    // Assert
    await using (CatalogDbContext dbContext = new CatalogDbContext(options))
    {
        (await dbContext.Products.CountAsync()).Should().Be(0);
        (await dbContext.Reviews.CountAsync()).Should().Be(0);
    }

    respawner.DeleteSql.Should().Contain("truncate table"); // on PostgreSQL: TRUNCATE ... CASCADE, in one statement
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L167-L204' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_E_PostgreSql' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**NOTE:** Like [`DockerTests.A_Container`](./README_Docker.md), this test is `[Ignore]`d by default because it needs
Docker. It was verified to pass locally (Docker running, `[Ignore]` temporarily removed) - about 3 seconds, including
starting the container.  

**Two PostgreSQL gotchas found while writing this:**
- **`WithDatabase(...)`.** Without it, the Testcontainers PostgreSQL container's database is `postgres` - PostgreSQL's
own maintenance database. Recreating it with `EnsureDeleted()` fails with `55006: cannot drop the currently open
database`.
- **Recreating vs. resetting, measured.** With this 2-table schema on PostgreSQL 17, recreating the database
(`EnsureDeleted` + `EnsureCreated`) took about **40ms** per reset; inserting a product and resetting with Respawn took
about **15ms** (averages of 5, over two runs). With a 2-table schema the gap is small - but recreating grows with the
size of the schema (and with migrations, if you apply them), while emptying tables doesn't grow nearly as fast.

---

## When should I use Respawn?

- **Integration tests against a real, shared database** (SQL Server, PostgreSQL, ...), especially through a
`WebApplicationFactory` or anything else that opens its own connections - **Respawn** in `[SetUp]`.
- **Tests that can run on SQLite** - a fresh in-memory database per test is simpler still
(see [Entity Framework Core](./README_EntityFrameworkCore.md)). Respawn earns its place when the database has to
persist across tests, as above.
- **Reference data that every test needs** - keep it with `TablesToIgnore`, rather than re-seeding it before every test.

---

Back to [README](../README.md)
