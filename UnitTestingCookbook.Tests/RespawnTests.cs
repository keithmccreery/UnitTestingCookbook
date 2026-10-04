using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using Respawn;
using Respawn.Graph;

using Testcontainers.PostgreSql;

using UnitTestingCookbook.Support;
using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Tests;

//
// Respawn https://github.com/jbogard/Respawn - see README_Respawn.md
//
[Category("unit")]
[Category("respawn")]
[TestFixture]
public class RespawnTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<CatalogDbContext> _options = null!;
    private Respawner _respawner = null!;

    // begin-snippet: RespawnTests_Setup
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
    // end-snippet

    private async Task AddProductWithReviewAsync(string name)
    {
        await using CatalogDbContext dbContext = new CatalogDbContext(_options);
        Product product = new Product { Name = name, Price = 9.99m };
        await dbContext.Products.AddAsync(product);
        await dbContext.SaveChangesAsync();
        await dbContext.Reviews.AddAsync(new ProductReview { ProductId = product.Id, Rating = 5, Comment = "Great" });
        await dbContext.SaveChangesAsync();
    }

    //
    // Q: How do I give every test an empty database, without recreating it?
    // NOTE: A_ and B_ insert the same product name, and Name has a unique index - so whichever runs second only passes
    //       because [SetUp] reset the database in between.
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: RespawnTests_A_EachTest_StartsEmpty
    public async Task A_EachTest_StartsEmpty()
    {
        // Act
        await AddProductWithReviewAsync("Widget");

        // Assert
        await using CatalogDbContext dbContext = new CatalogDbContext(_options);
        (await dbContext.Products.CountAsync()).Should().Be(1);
        (await dbContext.Reviews.CountAsync()).Should().Be(1);
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: RespawnTests_B_EachTest_StartsEmpty_Again
    public async Task B_EachTest_StartsEmpty_Again()
    {
        // Act - the same unique product name as A_
        await AddProductWithReviewAsync("Widget");

        // Assert
        await using CatalogDbContext dbContext = new CatalogDbContext(_options);
        (await dbContext.Products.CountAsync()).Should().Be(1);
    }
    // end-snippet

    //
    // Q: Why not just DELETE FROM every table?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: RespawnTests_C_ForeignKeys_MakeTheOrderMatter
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
    // end-snippet

    //
    // Q: How do I keep reference data (lookup tables, seed data) between tests?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: RespawnTests_D_TablesToIgnore_KeepsReferenceData
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
    // end-snippet

    //
    // Q: How does this look against a real database server?
    // NOTE: Requires Docker - disabled by default, like DockerTests.A_Container
    //
    [Test]
    [Category("_passes")]
    [Ignore("Requires Docker - disabled by default to keep full test-suite runs fast during regular development")]
    // begin-snippet: RespawnTests_E_PostgreSql
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
    // end-snippet
}
