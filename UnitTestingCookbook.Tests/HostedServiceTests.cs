using Microsoft.Extensions.Time.Testing;

using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[TestFixture]
public class HostedServiceTests
{
    // ExecuteAsync runs on a background thread, so there's a real race between this test advancing the
    // fake clock and the worker's loop actually reaching its next Task.Delay(period, timeProvider, ...) call.
    // Advancing repeatedly in small steps (rather than once, by the full period) tolerates that race: an
    // Advance() call before the timer is registered is simply a no-op, and once the timer registers, the
    // remaining small steps accumulate up to its due time and fire it exactly once.
    // begin-snippet: HostedServiceTests_AdvanceUntilNextHeartbeat
    private static async Task AdvanceUntilNextHeartbeat(FakeTimeProvider timeProvider, HeartbeatWorker worker, TimeSpan step, TimeSpan timeout)
    {
        int initialCount = worker.HeartbeatCount;
        DateTime deadline = DateTime.UtcNow + timeout;
        while (worker.HeartbeatCount == initialCount && DateTime.UtcNow < deadline)
        {
            timeProvider.Advance(step);
            await Task.Delay(1);
        }
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    // begin-snippet: HostedServiceTests_A_HeartbeatWorker_AdvancingTimeProvider_IncrementsHeartbeatCount
    public async Task A_HeartbeatWorker_AdvancingTimeProvider_IncrementsHeartbeatCount()
    {
        // Arrange
        FakeTimeProvider timeProvider = new FakeTimeProvider();
        HeartbeatWorker worker = new HeartbeatWorker(timeProvider, TimeSpan.FromMinutes(1));
        await worker.StartAsync(CancellationToken.None);

        // Act
        await AdvanceUntilNextHeartbeat(timeProvider, worker, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        // Assert
        worker.HeartbeatCount.Should().Be(1);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
    }
    // end-snippet

    [Test]
    [Category("_passes")]
    public async Task B_HeartbeatWorker_MultipleAdvances_IncrementsHeartbeatCountEachTime()
    {
        // Arrange
        FakeTimeProvider timeProvider = new FakeTimeProvider();
        HeartbeatWorker worker = new HeartbeatWorker(timeProvider, TimeSpan.FromMinutes(1));
        await worker.StartAsync(CancellationToken.None);

        // Act
        for (int i = 0; i < 3; i++)
            await AdvanceUntilNextHeartbeat(timeProvider, worker, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        // Assert
        worker.HeartbeatCount.Should().Be(3);

        // Cleanup
        await worker.StopAsync(CancellationToken.None);
    }

    [Test]
    [Category("_passes")]
    // begin-snippet: HostedServiceTests_C_HeartbeatWorker_StopAsync_StopsGracefully
    public async Task C_HeartbeatWorker_StopAsync_StopsGracefully()
    {
        // Arrange
        FakeTimeProvider timeProvider = new FakeTimeProvider();
        HeartbeatWorker worker = new HeartbeatWorker(timeProvider, TimeSpan.FromMinutes(1));
        await worker.StartAsync(CancellationToken.None);

        // Act
        Func<Task> action = () => worker.StopAsync(CancellationToken.None);

        // Assert
        await action.Should().NotThrowAsync();
    }
    // end-snippet
}
