namespace UnitTestingCookbook.Support.Services;

// begin-snippet: ExpiringCache
public class ExpiringCache<TValue>
{
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan timeToLive;

    private TValue? value;
    private DateTimeOffset expiresAt;

    public ExpiringCache(TimeProvider timeProvider, TimeSpan timeToLive)
    {
        this.timeProvider = timeProvider;
        this.timeToLive = timeToLive;
    }

    public void Set(TValue value)
    {
        this.value = value;
        expiresAt = timeProvider.GetUtcNow() + timeToLive;
    }

    public bool TryGet(out TValue? value)
    {
        if (timeProvider.GetUtcNow() >= expiresAt)
        {
            value = default;
            return false;
        }

        value = this.value;
        return true;
    }
}
// end-snippet
