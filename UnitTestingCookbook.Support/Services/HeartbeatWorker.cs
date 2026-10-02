using Microsoft.Extensions.Hosting;

namespace UnitTestingCookbook.Support.Services;

// begin-snippet: HeartbeatWorker
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
// end-snippet
