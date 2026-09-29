using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

using UnitTestingCookbook.MinimalApi;

namespace UnitTestingCookbook.Test;

//
// Connection String Validation https://khalidabuhakmeh.com/validating-connection-strings-on-dotnet-startup
//
// Unlike AppSettings Validation (README_AppSettingsValidation.md), this one IS unit tested - the
// resource being validated is a real SQLite database rather than an unreachable production one, so
// both the success and failure paths are deterministic and fast to run here directly, without
// booting the whole app through a WebApplicationFactory.
//
[Category("unit")]
[Category("connectionstringvalidation")]
[TestFixture]
public class ConnectionStringValidationTest
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        DbProviderFactories.RegisterFactory(ConnectionStringValidator.ProviderName, SqliteFactory.Instance);
    }

    //
    // Q: How do I prove a "valid-looking" connection string can actually be opened?
    //
    [Test]
    [Category("_passes")]
    public void A_OpenableConnectionString_Succeeds()
    {
        // Arrange
        ConnectionStringValidator validator = new ConnectionStringValidator();
        ConnectionStringOptions options = new ConnectionStringOptions { Database = "Data Source=:memory:" };

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    //
    // Q: How do I catch a connection string that points at an inaccessible resource?
    //
    // NOTE: SQLite creates its database file on first connection if it doesn't exist yet - so an
    // ordinary missing file wouldn't fail here. Mode=ReadOnly against a file that doesn't exist
    // does, since SQLite can't create one when it's only allowed to read.
    //
    [Test]
    [Category("_passes")]
    public void B_UnreachableConnectionString_Fails()
    {
        // Arrange
        ConnectionStringValidator validator = new ConnectionStringValidator();
        ConnectionStringOptions options = new ConnectionStringOptions
        {
            Database = "Data Source=/this/path/does/not/exist/cookbook.db;Mode=ReadOnly",
        };

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        using (new AssertionScope())
        {
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("Unable to open a connection");
        }
    }

    //
    // Q: How do I catch a connection string that was never configured at all?
    //
    [Test]
    [Category("_passes")]
    public void C_MissingConnectionString_Fails()
    {
        // Arrange
        ConnectionStringValidator validator = new ConnectionStringValidator();
        ConnectionStringOptions options = new ConnectionStringOptions { Database = string.Empty };

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        using (new AssertionScope())
        {
            result.Failed.Should().BeTrue();
            result.FailureMessage.Should().Contain("is required");
        }
    }
}
