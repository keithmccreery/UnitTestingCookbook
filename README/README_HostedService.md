# Testing IHostedService / BackgroundService

## NuGet Packages Referenced

- Microsoft.Extensions.Hosting.Abstractions https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions
- Microsoft.Extensions.TimeProvider.Testing https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing

All examples are located in `UnitTestingCookbook.Tests` -> [`HostedServiceTests`](../UnitTestingCookbook.Tests/HostedServiceTests.cs)

**NOTE:** This chapter builds directly on [TimeProvider](./README_TimeProvider.md) - a periodic background worker
is exactly the kind of "waits on the real clock" code that abstraction exists for.

---

## How do I unit test a periodic BackgroundService without actually waiting on its period?

Inject `TimeProvider` and use it for the delay between iterations (`Task.Delay(period, timeProvider,
stoppingToken)` - .NET 8+'s `TimeProvider`-aware overload) instead of a bare `Task.Delay(period)`. In tests,
substitute `FakeTimeProvider` and advance it instead of waiting in real time.

```csharp
public class HeartbeatWorker : BackgroundService
{
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan period;

    public int HeartbeatCount { get; private set; }

    public HeartbeatWorker(TimeProvider timeProvider, TimeSpan period)
    {
        this.timeProvider = timeProvider;
        this.period = period;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(period, timeProvider, stoppingToken);
            HeartbeatCount++;
        }
    }
}
```

**NOTE:** `BackgroundService.ExecuteAsync` is `protected`, so a test can't call it directly - drive the worker
through the public `StartAsync`/`StopAsync` (both inherited from `IHostedService`) instead, exactly as the host
would.

**NOTE - the race worth knowing about:** `ExecuteAsync` runs on a background thread, not synchronously inside
`StartAsync`. That means there's a real gap between "the test calls `StartAsync`" and "the worker's loop has
actually reached its next `Task.Delay(...)` call and registered a timer with the `FakeTimeProvider`." Advancing
the fake clock once, immediately after `StartAsync` returns, is a race - it can easily land *before* the timer
is registered, in which case the advance is silently a no-op and the tick never happens. The fix is to advance
in small repeated steps instead of one big jump: an `Advance()` call before the timer exists is harmless, and
once the timer *does* register, the remaining small steps simply accumulate up to its due time and fire it
exactly once.

```csharp
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
```

```csharp
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
```

## How do I test that stopping the worker doesn't throw?

```csharp
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
```

`BackgroundService.StopAsync` cancels the token passed to `ExecuteAsync`, which makes the pending
`Task.Delay(..., stoppingToken)` throw `OperationCanceledException` internally - `BackgroundService` catches
that itself as part of normal, graceful shutdown, so `StopAsync` completing without throwing is exactly the
expected, correct behavior here.

---

Back to [README](../README.md)
