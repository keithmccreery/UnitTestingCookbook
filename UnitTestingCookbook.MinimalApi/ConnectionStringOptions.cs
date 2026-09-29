namespace UnitTestingCookbook.MinimalApi;

public class ConnectionStringOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Database { get; set; } = default!;
}
