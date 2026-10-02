# Connection String Validation

## NuGet Packages Referenced

- Microsoft.Data.Sqlite https://github.com/dotnet/efcore

## Credits

- [Validating Connection Strings on .NET Startup](https://khalidabuhakmeh.com/validating-connection-strings-on-dotnet-startup), the source of this technique

All examples are located in `UnitTestingCookbook.Tests` -> [`ConnectionStringValidationTests`](../UnitTestingCookbook.Tests/ConnectionStringValidationTests.cs).
The validator under test lives in [`UnitTestingCookbook.MinimalApi`](../UnitTestingCookbook.MinimalApi)
(`ConnectionStringOptions.cs`, `ConnectionStringValidator.cs`), alongside
[AppSettings Validation](./README_AppSettingsValidation.md)'s options classes.

**Unlike AppSettings Validation, this chapter *does* have NUnit tests.** That one had nothing to unit test -
its validation rules are self-contained (a number is or isn't in range). This one's subject is whether a
*resource* is reachable, and a resource can be faked convincingly enough to make both the success and failure
paths real and deterministic - see below.

---

## Why isn't checking the connection string's shape enough?

A connection string can be syntactically perfect and still be useless: wrong password, database renamed, network
ACL doesn't allow the app server to reach it, ... None of that shows up from inspecting the string. The only way
to actually know is to open a connection for real:

```csharp
DbProviderFactory factory = DbProviderFactories.GetFactory(ProviderName);
using DbConnection connection = factory.CreateConnection()!;
connection.ConnectionString = options.Database;
connection.Open();
```

`DbProviderFactories` keeps this provider-agnostic - the same code validates a SQL Server, PostgreSQL, or SQLite
connection string, instead of hardcoding one ADO.NET provider's connection type.

---

## How do I mimic "the database is unreachable" without a real database?

Use SQLite. `Microsoft.Data.Sqlite` needs no server, no Docker, no network - `connection.Open()` either succeeds
or fails for real, against a resource (a file, or `:memory:`) that's entirely under the test's control. That
makes both outcomes below deterministic, unlike trying to reliably fail a connection to an actual SQL Server or
PostgreSQL instance without one running somewhere.

<!-- snippet: ConnectionStringValidator -->
<a id='snippet-ConnectionStringValidator'></a>
```cs
public class ConnectionStringValidator : IValidateOptions<ConnectionStringOptions>
{
    public const string ProviderName = "Microsoft.Data.Sqlite";

    public ValidateOptionsResult Validate(string? name, ConnectionStringOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Database))
        {
            return ValidateOptionsResult.Fail($"{ConnectionStringOptions.SectionName}:{nameof(options.Database)} is required.");
        }

        try
        {
            DbProviderFactory factory = DbProviderFactories.GetFactory(ProviderName);
            using DbConnection connection = factory.CreateConnection()
                ?? throw new InvalidOperationException($"'{ProviderName}' did not return a connection.");
            connection.ConnectionString = options.Database;
            connection.Open();

            return ValidateOptionsResult.Success;
        }
        catch (Exception ex)
        {
            // the value may be set, but point at a non-existent or inaccessible resource - only
            // actually opening the connection reveals that.
            return ValidateOptionsResult.Fail($"Unable to open a connection using '{options.Database}': {ex.Message}");
        }
    }
}
```
<sup><a href='/UnitTestingCookbook.MinimalApi/ConnectionStringValidator.cs#L12-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConnectionStringValidator' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`DbProviderFactories` needs the SQLite factory registered once, at startup (`Program.cs` - it's not automatic
the way it would be via `machine.config` in .NET Framework):

<!-- snippet: ConnectionStringValidationTests_RegisterFactory -->
<a id='snippet-ConnectionStringValidationTests_RegisterFactory'></a>
```cs
DbProviderFactories.RegisterFactory(ConnectionStringValidator.ProviderName, SqliteFactory.Instance);
```
<sup><a href='/UnitTestingCookbook.Tests/ConnectionStringValidationTests.cs#L26-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConnectionStringValidationTests_RegisterFactory' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I prove a "valid-looking" connection string can actually be opened?

<!-- snippet: ConnectionStringValidationTests_A_OpenableConnectionString_Succeeds -->
<a id='snippet-ConnectionStringValidationTests_A_OpenableConnectionString_Succeeds'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/ConnectionStringValidationTests.cs#L36-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConnectionStringValidationTests_A_OpenableConnectionString_Succeeds' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I catch a connection string that points at an inaccessible resource?

**NOTE:** SQLite creates its database file on first connection if it doesn't exist yet - so an ordinary missing
file path wouldn't fail here (it would just get created). `Mode=ReadOnly` against a file that doesn't exist does
fail, since SQLite can't create one when it's only allowed to read.  

<!-- snippet: ConnectionStringValidationTests_B_UnreachableConnectionString_Fails -->
<a id='snippet-ConnectionStringValidationTests_B_UnreachableConnectionString_Fails'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/ConnectionStringValidationTests.cs#L60-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConnectionStringValidationTests_B_UnreachableConnectionString_Fails' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The actual failure message:

```text
Unable to open a connection using 'Data Source=/this/path/does/not/exist/cookbook.db;Mode=ReadOnly': SQLite Error 14: 'unable to open database file'.
```

---

## How do I catch a connection string that was never configured at all?

A missing/empty value is a different failure mode than an unreachable one - worth its own check and its own
message, rather than letting it fall through to a confusing "unable to open a connection using ''" error.

<!-- snippet: ConnectionStringValidationTests_C_MissingConnectionString_Fails -->
<a id='snippet-ConnectionStringValidationTests_C_MissingConnectionString_Fails'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/ConnectionStringValidationTests.cs#L87-L104' title='Snippet source file'>snippet source</a> | <a href='#snippet-ConnectionStringValidationTests_C_MissingConnectionString_Fails' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

Back to [README](../README.md)
