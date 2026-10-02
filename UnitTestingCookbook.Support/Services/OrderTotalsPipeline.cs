using System.Threading.Channels;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

// A producer/consumer pipeline connected by a Channel<Order>: one side writes orders in, the other reads them out and
// totals them. The two sides run independently, which is the point of a channel - and what makes it worth testing.
public static class OrderTotalsPipeline
{
    // Producer: copies every order from the feed into the channel, then completes the channel - with the failure, if
    // the feed failed, so the consumer sees it instead of waiting forever.
    public static async Task ProduceAsync(IAsyncEnumerable<Order> feed, ChannelWriter<Order> writer, CancellationToken cancellationToken = default)
    {
        try
        {
            await foreach (Order order in feed.WithCancellation(cancellationToken))
            {
                await writer.WriteAsync(order, cancellationToken);
            }

            writer.Complete();
        }
        catch (Exception exception)
        {
            writer.Complete(exception);
            throw;
        }
    }

    // Consumer: reads until the producer completes the channel, returning the sum of the order totals.
    public static async Task<decimal> SumTotalsAsync(ChannelReader<Order> reader, CancellationToken cancellationToken = default)
    {
        decimal sum = 0m;

        await foreach (Order order in reader.ReadAllAsync(cancellationToken))
        {
            sum += order.Total;
        }

        return sum;
    }
}
