# Traces

## NuGet Packages Referenced

- None. `ActivitySource`, `Activity`, and `ActivityListener` (`System.Diagnostics`) are all part of the BCL, for
production code **and** tests.

All examples are located in `UnitTestingCookbook.Tests` -> [`TracesTests`](../UnitTestingCookbook.Tests/TracesTests.cs)  

**NOTE:** This chapter completes the set with [Logging](./README_Logging.md) and [Metrics](./README_Metrics.md). In
.NET, an OpenTelemetry *span* is a `System.Diagnostics.Activity`, and a *tracer* is an `ActivitySource`. OpenTelemetry
only exports them. Creating and testing them needs nothing beyond the BCL.  

---

## The code under test

[`OrderFulfillment`](../UnitTestingCookbook.Support/Services/OrderFulfillment.cs) starts an `orders.fulfill` Activity,
plus one `orders.reserve_stock` child Activity per order line, and it records an error status (and an exception event)
when a reservation fails.

<!-- snippet: OrderFulfillment.cs -->
<a id='snippet-OrderFulfillment.cs'></a>
```cs
using System.Diagnostics;
using System.Globalization;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

public static class OrderFulfillment
{
    public const string ActivitySourceName = "UnitTestingCookbook.Orders";

    // static, unlike OrderMetrics' Meter - there's no IActivitySourceFactory, and one ActivitySource per process is
    // the intended pattern. That's why the tests have to isolate themselves by TraceId instead (see TracesTests.E_)
    private static readonly ActivitySource activitySource = new ActivitySource(ActivitySourceName);

    public static async Task<bool> FulfillAsync(Order order, CancellationToken cancellationToken = default)
    {
        // StartActivity returns null when nothing is listening, so every use below is null-conditional
        using Activity? activity = activitySource.StartActivity("orders.fulfill");
        activity?.SetTag("order.id", order.Id);
        activity?.SetTag("order.line_count", order.Lines.Count);

        if (order.Lines.Count == 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Order has no lines.");
            return false;
        }

        try
        {
            foreach (OrderLine line in order.Lines)
            {
                await ReserveStockAsync(line, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException ex)
        {
            activity?.AddException(ex); // an "exception" event, per the OpenTelemetry semantic conventions
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return true;
    }

    private static async Task ReserveStockAsync(OrderLine line, CancellationToken cancellationToken)
    {
        // Activity.Current flows across the await, so this becomes a child of "orders.fulfill" automatically
        using Activity? activity = activitySource.StartActivity("orders.reserve_stock");
        activity?.SetTag("order.line.sku", line.Sku);
        activity?.SetTag("order.line.quantity", line.Quantity);

        if (line.Quantity <= 0)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Cannot reserve {0} of {1}.", line.Quantity, line.Sku));
        }

        await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false); // stands in for a stock-service call
    }
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/OrderFulfillment.cs#L1-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-OrderFulfillment.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ActivitySource.StartActivity` returns **null** unless a listener is subscribed to that source **and** its `Sample`
callback asks for the Activity. So production code uses `activity?.` everywhere, and a test has to register an
`ActivityListener` before the code under test runs.  

---

## How do I capture the Activities (spans) my code creates?

Register an `ActivityListener` with `ActivitySource.AddActivityListener`, and dispose it (`using`) when done.
It needs three things:

- `ShouldListenTo` - which `ActivitySource`s to listen to (match by name).
- `Sample` - whether to create the Activity at all. **Leave this out and every `StartActivity` still returns null.**
  (Checked while writing this chapter: removing `Sample` from these tests leaves the listener with 0 Activities.)
- `ActivityStopped` - where to put finished Activities. Collect on *stop*, not *start*: tags and status are usually set
  after an Activity starts. Use a thread-safe collection, because the callback runs on whatever thread stops the Activity.

<!-- snippet: TracesTests_A_ActivityListener -->
<a id='snippet-TracesTests_A_ActivityListener'></a>
```cs
public async Task A_ActivityListener()
{
    // Arrange
    ConcurrentQueue<Activity> stopped = new ConcurrentQueue<Activity>(); // callbacks can run on any thread

    using ActivityListener listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == OrderFulfillment.ActivitySourceName,
        // without Sample, StartActivity still returns null - "listening" alone isn't enough
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        // Stopped, not Started - tags and status are usually set after the Activity starts
        ActivityStopped = stopped.Enqueue,
    };
    ActivitySource.AddActivityListener(listener);

    Order order = new Order { Lines = [new OrderLine { Sku = "SKU-1", Quantity = 1, UnitPrice = 9.99m }] };

    // Act
    bool fulfilled = await OrderFulfillment.FulfillAsync(order);

    // Assert
    using (new AssertionScope())
    {
        fulfilled.Should().BeTrue();
        stopped.Select(x => x.OperationName).Should().Equal("orders.reserve_stock", "orders.fulfill"); // children stop first
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L22-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_A_ActivityListener' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Children stop before their parent, so `orders.reserve_stock` is collected before `orders.fulfill`.

The remaining tests use the same listener, moved into a small helper:

<!-- snippet: TracesTests_ListenTo -->
<a id='snippet-TracesTests_ListenTo'></a>
```cs
private static ActivityListener ListenTo(string sourceName, ConcurrentQueue<Activity> stopped)
{
    ActivityListener listener = new ActivityListener
    {
        ShouldListenTo = source => source.Name == sourceName,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStopped = stopped.Enqueue,
    };
    ActivitySource.AddActivityListener(listener);

    return listener;
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L216-L229' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_ListenTo' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert an Activity's tags and status?

`GetTagItem(name)` returns the tag's original object (here a `Guid` and an `int`, not their string forms).
`Status` is `Unset` unless the code sets it.

<!-- snippet: TracesTests_B_Tags_And_Status -->
<a id='snippet-TracesTests_B_Tags_And_Status'></a>
```cs
public async Task B_Tags_And_Status()
{
    // Arrange
    ConcurrentQueue<Activity> stopped = new ConcurrentQueue<Activity>();
    using ActivityListener listener = ListenTo(OrderFulfillment.ActivitySourceName, stopped);

    Order order = new Order
    {
        Id = Guid.NewGuid(),
        Lines = [new OrderLine { Sku = "SKU-1", Quantity = 2 }, new OrderLine { Sku = "SKU-2", Quantity = 1 }],
    };

    // Act
    await OrderFulfillment.FulfillAsync(order);

    // Assert
    Activity fulfill = stopped.Single(x => x.OperationName == "orders.fulfill");

    using (new AssertionScope())
    {
        fulfill.Kind.Should().Be(ActivityKind.Internal);
        fulfill.Status.Should().Be(ActivityStatusCode.Ok);
        fulfill.GetTagItem("order.id").Should().Be(order.Id); // the original object, not a string
        fulfill.GetTagItem("order.line_count").Should().Be(2);
        fulfill.Duration.Should().BePositive(); // real wall-clock time - only assert that it was measured
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L57-L85' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_B_Tags_And_Status' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

**NOTE:** `Activity` timestamps come from the system clock. Unlike `MetricCollector<T>` (see [Metrics](./README_Metrics.md)),
there's no way to pass in a `FakeTimeProvider`. So assert that a duration was *measured*, never what it *was*.  

---

## How do I assert parent/child relationships between Activities?

`Activity.Current` is an `AsyncLocal`, so it flows across `await`. Any Activity started while another one is current
becomes its child automatically. A child's `ParentSpanId` is its parent's `SpanId`, and every Activity in the tree
shares one `TraceId`.

<!-- snippet: TracesTests_C_Parent_Child -->
<a id='snippet-TracesTests_C_Parent_Child'></a>
```cs
public async Task C_Parent_Child()
{
    // Arrange
    ConcurrentQueue<Activity> stopped = new ConcurrentQueue<Activity>();
    using ActivityListener listener = ListenTo(OrderFulfillment.ActivitySourceName, stopped);

    Order order = new Order { Lines = [new OrderLine { Sku = "SKU-1", Quantity = 1 }, new OrderLine { Sku = "SKU-2", Quantity = 3 }] };

    // Act
    await OrderFulfillment.FulfillAsync(order);

    // Assert
    Activity fulfill = stopped.Single(x => x.OperationName == "orders.fulfill");
    List<Activity> reservations = stopped.Where(x => x.OperationName == "orders.reserve_stock").ToList();

    using (new AssertionScope())
    {
        fulfill.Parent.Should().BeNull(); // the root - nothing was running when the test called FulfillAsync
        reservations.Should().HaveCount(2);
        reservations.Should().AllSatisfy(x =>
        {
            x.ParentSpanId.Should().Be(fulfill.SpanId);
            x.TraceId.Should().Be(fulfill.TraceId); // one trace, end to end
        });
        reservations.Select(x => x.GetTagItem("order.line.sku")).Should().Equal("SKU-1", "SKU-2");
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L92-L120' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_C_Parent_Child' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I assert an Activity recorded an error?

`SetStatus(ActivityStatusCode.Error, description)` sets `Status` and `StatusDescription`. `AddException(ex)` (.NET 9+)
adds an `exception` event with the OpenTelemetry semantic-convention tags (`exception.type`, `exception.message`,
`exception.stacktrace`).

<!-- snippet: TracesTests_D_Error_Status_And_Exception_Event -->
<a id='snippet-TracesTests_D_Error_Status_And_Exception_Event'></a>
```cs
public async Task D_Error_Status_And_Exception_Event()
{
    // Arrange
    ConcurrentQueue<Activity> stopped = new ConcurrentQueue<Activity>();
    using ActivityListener listener = ListenTo(OrderFulfillment.ActivitySourceName, stopped);

    Order order = new Order { Lines = [new OrderLine { Sku = "SKU-1", Quantity = 0 }] };

    // Act
    Func<Task> act = () => OrderFulfillment.FulfillAsync(order);

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>();

    Activity fulfill = stopped.Single(x => x.OperationName == "orders.fulfill");
    ActivityEvent exceptionEvent = fulfill.Events.Should().ContainSingle().Subject;

    using (new AssertionScope())
    {
        fulfill.Status.Should().Be(ActivityStatusCode.Error);
        fulfill.StatusDescription.Should().Be("Cannot reserve 0 of SKU-1.");

        exceptionEvent.Name.Should().Be("exception");
        exceptionEvent.Tags.Should().Contain(new KeyValuePair<string, object?>("exception.type", typeof(InvalidOperationException).FullName));
        exceptionEvent.Tags.Should().Contain(new KeyValuePair<string, object?>("exception.message", "Cannot reserve 0 of SKU-1."));
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L127-L155' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_D_Error_Status_And_Exception_Event' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Do Activities from one test leak into another test's listener?

**Yes.** This is the big difference from [Metrics](./README_Metrics.md). A `MetricCollector<T>` can be scoped to one
test's `IMeterFactory` (see `MetricsTests.E_`). There's no `IActivitySourceFactory`, though: `ActivitySource` is
designed to be one static instance per process, and an `ActivityListener` receives Activities from **every** thread.
A test running at the same time as another one (or a background task left over from an earlier test) shows up in
your listener.

The fix: the test starts its own root Activity, from its own `ActivitySource`, around the code under test. Everything
the code under test starts becomes part of that trace, so filtering by the root's `TraceId` keeps only this test's
Activities:

<!-- snippet: TracesTests_E_Isolation_By_TraceId -->
<a id='snippet-TracesTests_E_Isolation_By_TraceId'></a>
```cs
public async Task E_Isolation_By_TraceId()
{
    // Arrange
    using ActivitySource testSource = new ActivitySource("UnitTestingCookbook.Tests.Traces");

    ConcurrentQueue<Activity> stopped = new ConcurrentQueue<Activity>();
    using ActivityListener listener = ListenTo(OrderFulfillment.ActivitySourceName, stopped);
    using ActivityListener testListener = ListenTo(testSource.Name, new ConcurrentQueue<Activity>()); // so testSource creates Activities too

    // stands in for another test running at the same time - its Activities reach this listener too
    await OrderFulfillment.FulfillAsync(new Order { Lines = [new OrderLine { Sku = "OTHER", Quantity = 1 }] });

    // Act - under a root Activity the test owns, so everything this test causes shares one TraceId
    ActivityTraceId traceId;
    using (Activity root = testSource.StartActivity("test")!)
    {
        traceId = root.TraceId;
        await OrderFulfillment.FulfillAsync(new Order { Lines = [new OrderLine { Sku = "MINE", Quantity = 1 }] });
    }

    // Assert
    List<Activity> mine = stopped.Where(x => x.TraceId == traceId).ToList();

    using (new AssertionScope())
    {
        stopped.Count(x => x.OperationName == "orders.fulfill").Should().Be(2); // the listener saw both
        mine.Count(x => x.OperationName == "orders.fulfill").Should().Be(1); // the filter keeps only this test's
        mine.Single(x => x.OperationName == "orders.reserve_stock").GetTagItem("order.line.sku").Should().Be("MINE");
    }
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L163-L194' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_E_Isolation_By_TraceId' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Without the filter, this is the tracing version of [Data Hangover](./README_DataHangover.md), and it makes a test unsafe
under [Parallel Processing](./README_ParallelProcessing.md).  

**NOTE:** The test's own `ActivitySource` needs a listener too (`testListener`). Otherwise `testSource.StartActivity`
returns null, just like the code under test's would.  

---

## Does the code still work when nothing is listening?

That's the production default until a tracing exporter (for example OpenTelemetry) is configured, and it's the case a
missing `?.` would break. It's worth a test of its own, because none of the tests above exercise it.

<!-- snippet: TracesTests_F_No_Listener -->
<a id='snippet-TracesTests_F_No_Listener'></a>
```cs
public async Task F_No_Listener()
{
    // Arrange
    Order order = new Order { Lines = [new OrderLine { Sku = "SKU-1", Quantity = 1 }] };

    // Act - no listener, so every StartActivity returns null
    bool fulfilled = await OrderFulfillment.FulfillAsync(order);

    // Assert - no NullReferenceException from the Activity calls, and the normal result is returned
    fulfilled.Should().BeTrue();
}
```
<sup><a href='/UnitTestingCookbook.Tests/TracesTests.cs#L202-L214' title='Snippet source file'>snippet source</a> | <a href='#snippet-TracesTests_F_No_Listener' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## Alternative: OpenTelemetry's in-memory exporter

If the app already configures OpenTelemetry tracing, `OpenTelemetry.Exporter.InMemory` (`.AddInMemoryExporter(list)`)
collects into a `List<Activity>` through the real OpenTelemetry pipeline, so it also exercises the app's samplers and
processors. It is **not** used here because the plain `ActivityListener` above needs no package and tests exactly what
the code under test emits. The same isolation caveat applies either way.  

---

Back to [README](../README.md)
