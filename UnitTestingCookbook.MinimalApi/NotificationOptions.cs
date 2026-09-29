namespace UnitTestingCookbook.MinimalApi;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public string FromEmail { get; set; } = default!;

    public int MaxRetries { get; set; }
}
