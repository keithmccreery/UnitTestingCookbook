using System.Collections.Concurrent;
using System.Diagnostics;

using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// System.Diagnostics.ActivityListener https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-collection-walkthroughs
//
[Category("unit")]
[Category("traces")]
[TestFixture]
public class TracesTests
{
    //
    // Q: How do I capture the Activities (spans) my code creates?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: TracesTests_A_ActivityListener
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
    // end-snippet

    //
    // Q: How do I assert an Activity's tags and status?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: TracesTests_B_Tags_And_Status
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
    // end-snippet

    //
    // Q: How do I assert parent/child relationships between Activities?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: TracesTests_C_Parent_Child
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
    // end-snippet

    //
    // Q: How do I assert an Activity recorded an error?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: TracesTests_D_Error_Status_And_Exception_Event
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
    // end-snippet

    //
    // Q: Do Activities from one test leak into another test's listener?
    // NOTE: Yes - unlike MetricCollector (see MetricsTests.E_), an ActivityListener is process-wide
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: TracesTests_E_Isolation_By_TraceId
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
    // end-snippet

    //
    // Q: Does the code still work when nothing is listening?
    // NOTE: This is the production default until a tracing exporter (e.g. OpenTelemetry) is configured
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: TracesTests_F_No_Listener
    public async Task F_No_Listener()
    {
        // Arrange
        Order order = new Order { Lines = [new OrderLine { Sku = "SKU-1", Quantity = 1 }] };

        // Act - no listener, so every StartActivity returns null
        bool fulfilled = await OrderFulfillment.FulfillAsync(order);

        // Assert - no NullReferenceException from the Activity calls, and the normal result is returned
        fulfilled.Should().BeTrue();
    }
    // end-snippet

    // begin-snippet: TracesTests_ListenTo
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
    // end-snippet
}
