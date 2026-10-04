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
| **A new database container per test** | Perfect isolation, real database | About a second per test, just to start the container (measured below) |
| **Respawn** - keep the schema, empty the tables | Fast; works with any number of connections; keeps reference data if asked | The database (and its schema) must already exist |

Respawn is for the common case of a **real, shared test database**: it inspects the schema once, works out how to
empty every table despite foreign keys, and then resets the database in one round trip before each test.

### Why not just start a new container for every test?

Because of what it costs - and because containers and Respawn aren't alternatives; they work at different scopes.
Measured on this machine with PostgreSQL 17 via Testcontainers:

| | Time |
|---|---|
| Start a PostgreSQL container - first time (cold) | about **4.1 s** |
| Start a PostgreSQL container - after that | about **1.2 s** each |
| Create the schema (`EnsureCreated`) | about **40 ms** |
| Insert a row **and** reset with Respawn | about **15 ms** |

A container per *test* means roughly a second of startup per test - about 10 minutes for 500 tests - against a few
seconds of Respawn resets for the same 500. So the usual pattern combines them:

- **A container per test run (or per fixture)** gives an **ephemeral database**: nothing shared with anyone else, gone
when the run ends.
- **Respawn per test, inside that container** gives each test **clean tables** without paying for a new container.

(Somewhere in between: one container, but a new *database* per test inside it - e.g. PostgreSQL's
`CREATE DATABASE ... TEMPLATE` copies an existing, already-migrated database. Not covered here.)

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

    // Respawn reads the foreign keys from the schema and deletes child tables before their parents
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

- **SQLite** - deletes the tables in dependency order, children before parents. (Its generated `DeleteSql` also
includes `PRAGMA foreign_keys = OFF` / `ON`, but Respawn runs the delete inside a transaction, where SQLite ignores that
pragma - so it's the order that does the work. The `TablesToIgnore` limitation below proves it.)
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

## Limitations

Respawn is simple and fast, but it's a blunt tool. Everything below was checked while writing this chapter (by running
it, or in Respawn 7.0.0's source, as noted).

- **It empties whole tables - it can't delete "only the rows my tests created".** Never point it at a shared database
(staging, integration, a team "non-prod"): it deletes everyone's data in every table it covers. `TablesToIgnore`,
`TablesToInclude`, and the schema options work per table and per schema, never per row. Respawn also has no check of
its own on *which* database it's resetting, so a guard that refuses to run unless the connection is a known test
database (by name) is cheap insurance.
- **Generated IDs keep counting.** By default, identity columns and sequences aren't reset: on PostgreSQL, the first
product after a reset got ID `2`, not `1`. `WithReseed = true` restarts them (it got `1`). Better still, don't write
tests that depend on specific generated IDs.
- **The database and schema must already exist.** Respawn never creates or migrates anything, and it reads the schema
once, in `Respawner.CreateAsync` - after a schema change, create a new `Respawner`.
- **Tests sharing one database can't run in parallel.** Resetting before one test would wipe the rows another test is
in the middle of using. Run them sequentially, or give each parallel worker its own database (see
[Parallel Processing](./README_ParallelProcessing.md)).
- **It only resets tables.** Anything else that remembers state between tests - an in-memory or distributed cache (see
[Caching](./README_Caching.md)), files, message queues, external services - needs its own reset.

### Ignoring a table that points at a table being reset doesn't work

`TablesToIgnore` is safe for tables that *other* tables point at - reference and lookup data, like `D_` above. It
isn't safe for a table that points *at* something being reset, and it fails differently on each database. Here, Reviews
(which references Products) is ignored while Products is reset:

- **SQLite** - the reset fails with `FOREIGN KEY constraint failed`, because the kept reviews would point at deleted
products. That's also the proof that, on SQLite, Respawn's `PRAGMA foreign_keys = OFF` has no effect (see above).
- **PostgreSQL** - the reset *succeeds*, but `TRUNCATE "public"."Products" CASCADE` empties the "ignored" Reviews table
anyway. (Checked against PostgreSQL 17.)

<!-- snippet: RespawnTests_F_IgnoringAChildTable_BreaksTheReset -->
<a id='snippet-RespawnTests_F_IgnoringAChildTable_BreaksTheReset'></a>
```cs
public async Task F_IgnoringAChildTable_BreaksTheReset()
{
    // Arrange - ignore Reviews (the child), but reset Products (the parent it points at)
    await AddProductWithReviewAsync("Widget");

    Respawner keepReviews = await Respawner.CreateAsync(_connection, new RespawnerOptions
    {
        DbAdapter = DbAdapter.Sqlite,
        TablesToIgnore = [new Table("Reviews")],
    });

    // Act
    Func<Task> reset = () => keepReviews.ResetAsync(_connection);

    // Assert - the kept reviews would be left pointing at deleted products, so the reset fails
    await reset.Should().ThrowAsync<SqliteException>().WithMessage("*FOREIGN KEY constraint failed*");
}
```
<sup><a href='/UnitTestingCookbook.Tests/RespawnTests.cs#L213-L231' title='Snippet source file'>snippet source</a> | <a href='#snippet-RespawnTests_F_IgnoringAChildTable_BreaksTheReset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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
