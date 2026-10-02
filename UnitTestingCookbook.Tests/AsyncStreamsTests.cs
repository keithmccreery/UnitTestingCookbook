using System.Threading.Channels;

using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// IAsyncEnumerable<T> and System.Threading.Channels - both part of the BCL, no packages needed.
// See README_AsyncStreams.md.
//
[Category("unit")]
[Category("asyncstreams")]
[TestFixture]
public class AsyncStreamsTests
{
    // Every test that awaits a stream or a channel gets a timeout: a bug in async code tends to show up as a hang,
    // and a hung test blocks the whole suite. With this, a hang becomes a failing test instead.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Orders whose totals are 10, 20, 30, ... - easy to sum in your head
    private static List<Order> CreateOrders(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Order { Customer = $"Customer {i}", Lines = [new OrderLine { Sku = "WIDGET", Quantity = i, UnitPrice = 10m }] })
            .ToList();

    //
    // Q: How do I assert on everything an IAsyncEnumerable<T> produces?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_A_ConsumeWholeStream
    public async Task A_ConsumeWholeStream()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        OrderFeed feed = new OrderFeed(CreateOrders(3));

        // Act
        List<Order> received = await feed.ReadAllAsync(timeout.Token).ToListAsync(timeout.Token); // System.Linq.AsyncEnumerable, built into .NET 10

        // Assert
        using (new AssertionScope())
        {
            received.Select(order => order.Total).Should().Equal(10m, 20m, 30m);
            feed.CleanedUp.Should().BeTrue();
        }
    }
    // end-snippet

    //
    // Q: How do I prove a stream is lazy - that stopping early never produces the rest?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_B_StoppingEarly_ProducesNoMore_AndStillCleansUp
    public async Task B_StoppingEarly_ProducesNoMore_AndStillCleansUp()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        OrderFeed feed = new OrderFeed(CreateOrders(100));
        int consumed = 0;

        // Act
        await foreach (Order order in feed.ReadAllAsync(timeout.Token))
        {
            if (++consumed == 2)
            {
                break; // leaving the loop disposes the enumerator, which runs the stream's finally block
            }
        }

        // Assert
        using (new AssertionScope())
        {
            feed.Produced.Should().Be(2); // not 100 - the other 98 were never produced
            feed.CleanedUp.Should().BeTrue();
        }
    }
    // end-snippet

    //
    // Q: How do I test cancelling a stream part-way through?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_C_CancelPartWay
    public async Task C_CancelPartWay()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        OrderFeed feed = new OrderFeed(CreateOrders(100));

        // Act
        Func<Task> act = async () =>
        {
            await foreach (Order order in feed.ReadAllAsync(cancellation.Token))
            {
                await cancellation.CancelAsync(); // e.g. the user navigated away after the first result
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        feed.Produced.Should().Be(1);
        feed.CleanedUp.Should().BeTrue();
    }
    // end-snippet

    //
    // Q: How do I test a stream that fails part-way through - keeping the items it produced before the failure?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_D_FailurePartWay
    public async Task D_FailurePartWay()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        OrderFeed feed = new OrderFeed(CreateOrders(5), failAfter: 2);
        List<Order> received = [];

        // Act
        Func<Task> act = async () =>
        {
            await foreach (Order order in feed.ReadAllAsync(timeout.Token))
            {
                received.Add(order);
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Feed failed after 2 orders.");
        received.Should().HaveCount(2); // what arrived before the failure is still observable
        feed.CleanedUp.Should().BeTrue();
    }
    // end-snippet

    //
    // Q: How do I test a producer and a consumer connected by a Channel<T>, running at the same time?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_E_Channel_ProducerAndConsumer
    public async Task E_Channel_ProducerAndConsumer()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        Channel<Order> channel = Channel.CreateUnbounded<Order>();
        OrderFeed feed = new OrderFeed(CreateOrders(4));

        // Act - start the consumer first, so it is genuinely waiting on the channel while the producer writes
        Task<decimal> consumer = OrderTotalsPipeline.SumTotalsAsync(channel.Reader, timeout.Token);
        await OrderTotalsPipeline.ProduceAsync(feed.ReadAllAsync(timeout.Token), channel.Writer, timeout.Token);
        decimal sum = await consumer;

        // Assert
        sum.Should().Be(10m + 20m + 30m + 40m);
    }
    // end-snippet

    //
    // Q: How do I test backpressure - that a full bounded channel makes the producer wait?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_F_BoundedChannel_Backpressure
    public async Task F_BoundedChannel_Backpressure()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        Channel<int> channel = Channel.CreateBounded<int>(new BoundedChannelOptions(capacity: 2) { FullMode = BoundedChannelFullMode.Wait });

        // Act
        await channel.Writer.WriteAsync(1, timeout.Token);
        await channel.Writer.WriteAsync(2, timeout.Token);
        ValueTask third = channel.Writer.WriteAsync(3, timeout.Token); // channel is full - this one has to wait

        bool thirdCompletedWhileFull = third.IsCompleted;
        int firstRead = await channel.Reader.ReadAsync(timeout.Token); // makes room
        await third; // ...so the waiting write can finish

        // Assert
        using (new AssertionScope())
        {
            thirdCompletedWhileFull.Should().BeFalse();
            firstRead.Should().Be(1);
            channel.Reader.Count.Should().Be(2); // 2 and 3
        }
    }
    // end-snippet

    //
    // Q: When the producer fails, does the consumer find out - or wait forever?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: AsyncStreamsTests_G_ProducerFailure_ReachesTheConsumer
    public async Task G_ProducerFailure_ReachesTheConsumer()
    {
        // Arrange
        using CancellationTokenSource timeout = new CancellationTokenSource(Timeout);
        Channel<Order> channel = Channel.CreateUnbounded<Order>();
        OrderFeed feed = new OrderFeed(CreateOrders(5), failAfter: 1);

        // Act
        Task<decimal> consumer = OrderTotalsPipeline.SumTotalsAsync(channel.Reader, timeout.Token);
        Func<Task> produce = () => OrderTotalsPipeline.ProduceAsync(feed.ReadAllAsync(timeout.Token), channel.Writer, timeout.Token);
        Func<Task> consume = () => consumer;

        // Assert - the producer completes the channel *with* its exception, so the consumer rethrows it. Without that,
        // the consumer would wait forever, and only the timeout would end this test (as an OperationCanceledException).
        await produce.Should().ThrowAsync<InvalidOperationException>();
        await consume.Should().ThrowAsync<InvalidOperationException>().WithMessage("Feed failed after 1 orders.");
    }
    // end-snippet
}
