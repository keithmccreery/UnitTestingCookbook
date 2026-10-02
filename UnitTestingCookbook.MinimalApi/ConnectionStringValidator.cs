using System.Data.Common;

using Microsoft.Extensions.Options;

namespace UnitTestingCookbook.MinimalApi;

// https://khalidabuhakmeh.com/validating-connection-strings-on-dotnet-startup
// A connection string can be syntactically valid and still point at nothing reachable - the only
// way to actually know is to open a real connection. DbProviderFactories keeps this provider-agnostic
// (the same code validates a SQL Server, PostgreSQL, or SQLite connection string) instead of hardcoding
// a single ADO.NET provider's connection type.
// begin-snippet: ConnectionStringValidator
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
// end-snippet
