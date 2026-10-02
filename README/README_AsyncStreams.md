# Async Streams and Channels

## NuGet Packages Referenced

- None. `IAsyncEnumerable<T>`, `System.Threading.Channels`, and the async LINQ operators used below
(`ToListAsync()`, from `System.Linq.AsyncEnumerable`) are all part of .NET 10's base class library.

All examples are located in `UnitTestingCookbook.Tests` -> [`AsyncStreamsTests`](../UnitTestingCookbook.Tests/AsyncStreamsTests.cs)  

**See also:** [Testing IHostedService / BackgroundService](./README_HostedService.md) for long-running background
work driven by a timer, and the async assertion examples in [AwesomeAssertions](./README_AwesomeAssertions.md).  

---

## The code under test

[`OrderFeed`](../UnitTestingCookbook.Support/Services/OrderFeed.cs) is an **async stream** (`IAsyncEnumerable<Order>`).
It stands in for paging through an API or reading a database cursor: orders are produced only as the consumer asks
for them. Two properties let tests look inside: `Produced` (how many orders were actually pulled) and `CleanedUp`
(whether the stream's `finally` block ran).

<!-- snippet: OrderFeed.cs -->
<a id='snippet-OrderFeed.cs'></a>
```cs
using System.Globalization;
using System.Runtime.CompilerServices;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

// An async stream of orders - stands in for paging through an API or a database cursor. Orders are produced only as
// the consumer asks for them, so a consumer that stops early (or cancels) never causes the rest to be fetched.
public sealed class OrderFeed
{
    private readonly IReadOnlyList<Order> orders;
    private readonly int? failAfter;

    public OrderFeed(IReadOnlyList<Order> orders, int? failAfter = null)
    {
        this.orders = orders;
        this.failAfter = failAfter;
    }

    // How many orders the consumer actually pulled - lets a test prove the stream is lazy.
    public int Produced { get; private set; }

    // True once the stream's cleanup (its finally block) has run - lets a test prove an abandoned or cancelled
    // stream still releases its resources.
    public bool CleanedUp { get; private set; }

    public async IAsyncEnumerable<Order> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (Order order in orders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Produced == failAfter)
                {
                    throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Feed failed after {failAfter} orders."));
                }

                await Task.Yield(); // a real feed would await I/O here
                Produced++;
                yield return order;
            }
        }
        finally
        {
            CleanedUp = true;
        }
    }
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/OrderFeed.cs#L1-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-OrderFeed.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

[`OrderTotalsPipeline`](../UnitTestingCookbook.Support/Services/OrderTotalsPipeline.cs) connects a producer and a
consumer with a **`Channel<Order>`**. The producer copies a feed into the channel; the consumer reads until the channel
is completed, totaling the orders. The two sides run independently, which is what a channel is for.

<!-- snippet: OrderTotalsPipeline.cs -->
<a id='snippet-OrderTotalsPipeline.cs'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Support/Services/OrderTotalsPipeline.cs#L1-L43' title='Snippet source file'>snippet source</a> | <a href='#snippet-OrderTotalsPipeline.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Every test has a timeout

Bugs in async code often show up as a **hang**, not a failure: a consumer waiting for an item that never comes, or a
producer waiting for space that never frees up. A hung test blocks the whole suite. So every test here creates a
`CancellationTokenSource` with a 5-second timeout and passes its token to everything it awaits, which turns a hang
into an ordinary failing test (`OperationCanceledException`). The `G_` example below shows exactly that happening.

---

## How do I assert on everything an IAsyncEnumerable<T> produces?

Collect it into a list with `ToListAsync()`, then assert on the list as usual:

<!-- snippet: AsyncStreamsTests_A_ConsumeWholeStream -->
<a id='snippet-AsyncStreamsTests_A_ConsumeWholeStream'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L32-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_A_ConsumeWholeStream' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I prove a stream is lazy - that stopping early never produces the rest?

`break`ing out of `await foreach` **disposes** the enumerator. That stops the stream and runs its `finally` block. The
`Produced` counter proves only 2 of the 100 orders were ever produced:

<!-- snippet: AsyncStreamsTests_B_StoppingEarly_ProducesNoMore_AndStillCleansUp -->
<a id='snippet-AsyncStreamsTests_B_StoppingEarly_ProducesNoMore_AndStillCleansUp'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L56-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_B_StoppingEarly_ProducesNoMore_AndStillCleansUp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I test cancelling a stream part-way through?

`OrderFeed` marks its token parameter `[EnumeratorCancellation]` and checks it before producing each order. The test
cancels after the first order, and asserts the consumer gets an `OperationCanceledException`, that no more orders were
produced, and that cleanup still ran:

<!-- snippet: AsyncStreamsTests_C_CancelPartWay -->
<a id='snippet-AsyncStreamsTests_C_CancelPartWay'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L87-L109' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_C_CancelPartWay' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I test a stream that fails part-way through - keeping the items it produced before the failure?

The test collects items into a list *inside* the `await foreach`, so even though the loop ends with an exception, the
items that arrived first can still be asserted on:

<!-- snippet: AsyncStreamsTests_D_FailurePartWay -->
<a id='snippet-AsyncStreamsTests_D_FailurePartWay'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L116-L138' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_D_FailurePartWay' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I test a producer and a consumer connected by a Channel<T>, running at the same time?

Start the consumer **first** (without awaiting it), so it's genuinely waiting on the channel while the producer
writes. Then await the producer, then the consumer's result:

<!-- snippet: AsyncStreamsTests_E_Channel_ProducerAndConsumer -->
<a id='snippet-AsyncStreamsTests_E_Channel_ProducerAndConsumer'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L145-L161' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_E_Channel_ProducerAndConsumer' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I test backpressure - that a full bounded channel makes the producer wait?

A bounded channel with `FullMode = Wait` makes `WriteAsync` wait while the channel is full. That's deterministic to
test: the third write's `ValueTask` is still incomplete when the test checks it, and completes once a read makes room.
No timing or sleeps are involved.

<!-- snippet: AsyncStreamsTests_F_BoundedChannel_Backpressure -->
<a id='snippet-AsyncStreamsTests_F_BoundedChannel_Backpressure'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L168-L192' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_F_BoundedChannel_Backpressure' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## When the producer fails, does the consumer find out - or wait forever?

If the producer fails and only *stops*, the consumer waits forever for an item or a completion signal that never
comes. `OrderTotalsPipeline.ProduceAsync` completes the channel **with the exception** (`writer.Complete(exception)`),
so the consumer's `ReadAllAsync` rethrows it:

<!-- snippet: AsyncStreamsTests_G_ProducerFailure_ReachesTheConsumer -->
<a id='snippet-AsyncStreamsTests_G_ProducerFailure_ReachesTheConsumer'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/AsyncStreamsTests.cs#L199-L217' title='Snippet source file'>snippet source</a> | <a href='#snippet-AsyncStreamsTests_G_ProducerFailure_ReachesTheConsumer' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**Both `G_` and `C_` were checked by deliberately breaking the code they protect:**
- Temporarily removing `writer.Complete(exception)` made the consumer hang. The 5-second timeout fired, and `G_`
**failed** ("Expected a System.InvalidOperationException to be thrown, but found System.OperationCanceledException")
instead of freezing the test run. That's the timeout guard doing its job.
- Temporarily removing `OrderFeed`'s `cancellationToken.ThrowIfCancellationRequested()` made `C_` **fail** ("Expected a
System.OperationCanceledException to be thrown, but no exception was thrown").

All 7 tests also passed 20 runs in a row, so none of them depend on lucky thread timing.  

---

Back to [README](../README.md)
