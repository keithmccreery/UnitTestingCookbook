# TimeProvider

## NuGet Packages Referenced

- Microsoft.Extensions.TimeProvider.Testing https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing

All examples are located in `UnitTestingCookbook.Tests` -> [`TimeProviderTests`](../UnitTestingCookbook.Tests/TimeProviderTests.cs)

**NOTE:** `TimeProvider` itself (the abstract class) has been part of the BCL since .NET 8 - no package needed
to use it in production code. `FakeTimeProvider`, the test double used below, lives in the separate
`Microsoft.Extensions.TimeProvider.Testing` package (namespace `Microsoft.Extensions.Time.Testing`).

---

## How do I test code that depends on the current time?

Code that calls `DateTime.Now`/`DateTimeOffset.UtcNow` directly is untestable - you can't control what "now" is
from a test. `TimeProvider` is the modern (.NET 8+) fix: inject `TimeProvider` instead of calling the static
clock directly, and substitute `FakeTimeProvider` in tests to set "now" to whatever the test needs.

<!-- snippet: GreetingClock -->
<a id='snippet-GreetingClock'></a>
```cs
public class GreetingClock
{
    private readonly TimeProvider timeProvider;

    public GreetingClock(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public string GetGreeting()
    {
        int hour = timeProvider.GetLocalNow().Hour;

        return hour switch
        {
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening",
        };
    }
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/GreetingClock.cs#L3-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-GreetingClock' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: TimeProviderTests_A_GetGreeting_BeforeNoon_ReturnsGoodMorning -->
<a id='snippet-TimeProviderTests_A_GetGreeting_BeforeNoon_ReturnsGoodMorning'></a>
```cs
public void A_GetGreeting_BeforeNoon_ReturnsGoodMorning()
{
    // Arrange
    FakeTimeProvider timeProvider = new FakeTimeProvider();
    timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
    GreetingClock greetingClock = new GreetingClock(timeProvider);

    // Act
    string result = greetingClock.GetGreeting();

    // Assert
    result.Should().Be("Good morning");
}
```
<sup><a href='/UnitTestingCookbook.Tests/TimeProviderTests.cs#L13-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-TimeProviderTests_A_GetGreeting_BeforeNoon_ReturnsGoodMorning' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**NOTE:** `FakeTimeProvider.LocalTimeZone` defaults to UTC, so `GetLocalNow()` and `GetUtcNow()` agree in these
examples - no timezone conversion to reason about. Production code should still call `GetLocalNow()` where local
time is genuinely what's wanted; the test just doesn't need to care about the difference here.

---

## How do I test something that expires or times out, without actually waiting?

`FakeTimeProvider.Advance(TimeSpan)` moves the fake clock forward instantly - no `Task.Delay`, no real elapsed
wall-clock time, and no flakiness from a test that's timing-sensitive against the real clock.

<!-- snippet: ExpiringCache -->
<a id='snippet-ExpiringCache'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Support/Services/ExpiringCache.cs#L3-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-ExpiringCache' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: TimeProviderTests_D_ExpiringCache_AfterTimeToLiveElapses_ReturnsNothing -->
<a id='snippet-TimeProviderTests_D_ExpiringCache_AfterTimeToLiveElapses_ReturnsNothing'></a>
```cs
public void D_ExpiringCache_AfterTimeToLiveElapses_ReturnsNothing()
{
    // Arrange
    FakeTimeProvider timeProvider = new FakeTimeProvider();
    ExpiringCache<string> cache = new ExpiringCache<string>(timeProvider, TimeSpan.FromMinutes(5));
    cache.Set("cached value");

    // Act
    timeProvider.Advance(TimeSpan.FromMinutes(6));
    bool found = cache.TryGet(out string? result);

    // Assert
    found.Should().BeFalse();
    result.Should().BeNull();
}
```
<sup><a href='/UnitTestingCookbook.Tests/TimeProviderTests.cs#L65-L81' title='Snippet source file'>snippet source</a> | <a href='#snippet-TimeProviderTests_D_ExpiringCache_AfterTimeToLiveElapses_ReturnsNothing' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

See [Testing IHostedService / BackgroundService](./README_HostedService.md) for `TimeProvider` used to test a
periodic background worker the same way, without waiting on its actual period.

---

Back to [README](../README.md)
