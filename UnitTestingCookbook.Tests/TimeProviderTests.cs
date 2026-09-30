using Microsoft.Extensions.Time.Testing;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[TestFixture]
public class TimeProviderTests
{
    [Test]
    [Category("_passes")]
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

    [Test]
    [Category("_passes")]
    public void B_GetGreeting_Evening_ReturnsGoodEvening()
    {
        // Arrange
        FakeTimeProvider timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 20, 0, 0, TimeSpan.Zero));
        GreetingClock greetingClock = new GreetingClock(timeProvider);

        // Act
        string result = greetingClock.GetGreeting();

        // Assert
        result.Should().Be("Good evening");
    }

    [Test]
    [Category("_passes")]
    public void C_ExpiringCache_BeforeTimeToLiveElapses_ReturnsValue()
    {
        // Arrange
        FakeTimeProvider timeProvider = new FakeTimeProvider();
        ExpiringCache<string> cache = new ExpiringCache<string>(timeProvider, TimeSpan.FromMinutes(5));
        cache.Set("cached value");

        // Act
        timeProvider.Advance(TimeSpan.FromMinutes(4));
        bool found = cache.TryGet(out string? result);

        // Assert
        found.Should().BeTrue();
        result.Should().Be("cached value");
    }

    [Test]
    [Category("_passes")]
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
}
