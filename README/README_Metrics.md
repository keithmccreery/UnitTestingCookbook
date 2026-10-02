# Metrics

## NuGet Packages Referenced

- Microsoft.Extensions.Diagnostics.Testing https://github.com/dotnet/extensions (`MetricCollector<T>`) - the same
package as `FakeLogger<T>` in [Logging](./README_Logging.md)
- Microsoft.Extensions.Diagnostics (`services.AddMetrics()`, which registers `IMeterFactory`)

All examples are located in `UnitTestingCookbook.Tests` -> [`MetricsTests`](../UnitTestingCookbook.Tests/MetricsTests.cs)  

**NOTE:** `System.Diagnostics.Metrics` (`Meter`, `Counter<T>`, `Histogram<T>`, `IMeterFactory`, ...) is part of the
BCL - production code needs no package to emit metrics. Only the test-side `MetricCollector<T>` needs the package.  

**NOTE:** The `ServiceCollection`/DI wiring below follows the same basic pattern as
[Dependency Injection](./README_DependencyInjection.md) (the source of truth for that pattern) - repeated here
inline so this chapter stands on its own.  

---

## The code under test

[`OrderMetrics`](../UnitTestingCookbook.Support/Services/OrderMetrics.cs) emits three instruments: a counter, a
histogram, and an observable gauge. It creates its `Meter` from an injected `IMeterFactory` instead of `new Meter(...)`.
That's the recommended pattern for DI apps, and it's what makes the tests below isolated from each other (see `E_`).

```csharp
using System.Diagnostics.Metrics;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

public sealed class OrderMetrics
{
    public const string MeterName = "UnitTestingCookbook.Orders";

    private readonly Counter<long> ordersPlaced;
    private readonly Histogram<double> orderValue;
    private int pendingOrders;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        // IMeterFactory (not `new Meter(...)`) - the factory owns and disposes the Meter, and scopes it to this
        // DI container, so metrics from one container (or one test) never leak into another
        Meter meter = meterFactory.Create(MeterName);

        ordersPlaced = meter.CreateCounter<long>("orders.placed", unit: "{order}", description: "Number of orders placed.");
        orderValue = meter.CreateHistogram<double>("orders.value", unit: "USD", description: "Order total.");
        meter.CreateObservableGauge("orders.pending", () => pendingOrders, unit: "{order}", description: "Orders awaiting fulfillment.");
    }

    public void RecordOrderPlaced(Order order, string channel)
    {
        KeyValuePair<string, object?> channelTag = new KeyValuePair<string, object?>("channel", channel);

        ordersPlaced.Add(1, channelTag);
        orderValue.Record((double) order.Total, channelTag);
    }

    public void SetPendingOrders(int count)
    {
        pendingOrders = count;
    }
}
```

`MetricCollector<T>` listens to **one instrument**, identified by `(meterScope, meterName, instrumentName)`, and
records every measurement it emits from the moment it's created. Create it **before** the code under test runs, and
dispose it (`using`) when done. `T` must match the instrument's type (`Counter<long>` -> `MetricCollector<long>`).  

---

## How do I assert a Counter was incremented?

- `GetMeasurementSnapshot()` returns every individual measurement (one per `Add()` call).
- `.EvaluateAsCounter()` sums them, which is usually what a counter assertion actually cares about.
- `LastMeasurement` returns the most recent measurement.

```csharp
public void A_Counter()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddMetrics(); // registers IMeterFactory
    services.AddSingleton<OrderMetrics>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    IMeterFactory meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
    OrderMetrics orderMetrics = serviceProvider.GetRequiredService<OrderMetrics>();

    // meterScope = the IMeterFactory - only collects from Meters created by *this* factory
    using MetricCollector<long> collector = new MetricCollector<long>(meterFactory, OrderMetrics.MeterName, "orders.placed");

    // Act
    orderMetrics.RecordOrderPlaced(new Order(), "web");
    orderMetrics.RecordOrderPlaced(new Order(), "web");

    // Assert
    using (new AssertionScope())
    {
        collector.GetMeasurementSnapshot().Should().HaveCount(2); // two Add() calls
        collector.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(2); // their sum
        collector.LastMeasurement!.Value.Should().Be(1);
    }
}
```

---

## How do I assert the values recorded by a Histogram?

```csharp
public void B_Histogram()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddMetrics();
    services.AddSingleton<OrderMetrics>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    IMeterFactory meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
    OrderMetrics orderMetrics = serviceProvider.GetRequiredService<OrderMetrics>();

    using MetricCollector<double> collector = new MetricCollector<double>(meterFactory, OrderMetrics.MeterName, "orders.value");

    // Act
    orderMetrics.RecordOrderPlaced(new Order { Lines = [new OrderLine { Quantity = 2, UnitPrice = 9.99m }] }, "web");
    orderMetrics.RecordOrderPlaced(new Order { Lines = [new OrderLine { Quantity = 1, UnitPrice = 24.50m }] }, "web");

    // Assert
    collector.GetMeasurementSnapshot()
        .Select(x => x.Value)
        .Should().Equal(19.98, 24.50);
}
```

---

## How do I assert on a measurement's tags (dimensions)?

`ContainsTags("name")` filters to measurements that *have* a tag (any value). `MatchesTags(new KeyValuePair<...>(name, value))`
filters to an exact name **and** value. Both return the filtered measurements, so `.EvaluateAsCounter()` can be chained.

```csharp
public void C_Tags()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddMetrics();
    services.AddSingleton<OrderMetrics>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    IMeterFactory meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
    OrderMetrics orderMetrics = serviceProvider.GetRequiredService<OrderMetrics>();

    using MetricCollector<long> collector = new MetricCollector<long>(meterFactory, OrderMetrics.MeterName, "orders.placed");

    // Act
    orderMetrics.RecordOrderPlaced(new Order(), "web");
    orderMetrics.RecordOrderPlaced(new Order(), "mobile");
    orderMetrics.RecordOrderPlaced(new Order(), "web");

    // Assert
    IReadOnlyList<CollectedMeasurement<long>> measurements = collector.GetMeasurementSnapshot();

    using (new AssertionScope())
    {
        measurements.ContainsTags("channel").EvaluateAsCounter().Should().Be(3); // has the tag, any value
        measurements.MatchesTags(new KeyValuePair<string, object?>("channel", "web")).EvaluateAsCounter().Should().Be(2);
        measurements.MatchesTags(new KeyValuePair<string, object?>("channel", "mobile")).EvaluateAsCounter().Should().Be(1);
        measurements[1].Tags["channel"].Should().Be("mobile");
    }
}
```

---

## How do I assert an ObservableGauge (a value that's polled, not pushed)?

Observable instruments don't record anything when the value changes. Their callback runs only when a listener
**polls** them. In production that's the metrics exporter, on its own schedule. In a test,
`collector.RecordObservableInstruments()` polls them on demand, so the test controls exactly when each value is captured.

```csharp
public void D_ObservableGauge()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddMetrics();
    services.AddSingleton<OrderMetrics>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    IMeterFactory meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
    OrderMetrics orderMetrics = serviceProvider.GetRequiredService<OrderMetrics>();

    using MetricCollector<int> collector = new MetricCollector<int>(meterFactory, OrderMetrics.MeterName, "orders.pending");

    // Act
    orderMetrics.SetPendingOrders(7);
    collector.RecordObservableInstruments(); // nothing is recorded until something polls the gauge

    orderMetrics.SetPendingOrders(3);
    collector.RecordObservableInstruments();

    // Assert
    collector.GetMeasurementSnapshot()
        .Select(x => x.Value)
        .Should().Equal(7, 3);
}
```

---

## Do metrics from one test leak into another test's collector?

Not if the `Meter` comes from `IMeterFactory`, and the collector is created with that factory as its `meterScope`.
A factory-created `Meter`'s `Scope` is the factory itself, and a collector only matches a `Meter` whose scope
is **equal** to the `meterScope` it was given. So each DI container's measurements stay separate, even with
identical meter and instrument names:

```csharp
public void E_Isolation_Per_MeterFactory()
{
    // Arrange - two independent containers, as two tests (possibly running in parallel) would have
    ServiceCollection services = new ServiceCollection();
    services.AddMetrics();
    services.AddSingleton<OrderMetrics>();
    using ServiceProvider serviceProvider1 = services.BuildServiceProvider(true);
    using ServiceProvider serviceProvider2 = services.BuildServiceProvider(true);

    IMeterFactory meterFactory1 = serviceProvider1.GetRequiredService<IMeterFactory>();
    IMeterFactory meterFactory2 = serviceProvider2.GetRequiredService<IMeterFactory>();
    OrderMetrics orderMetrics1 = serviceProvider1.GetRequiredService<OrderMetrics>();
    OrderMetrics orderMetrics2 = serviceProvider2.GetRequiredService<OrderMetrics>();

    using MetricCollector<long> collector1 = new MetricCollector<long>(meterFactory1, OrderMetrics.MeterName, "orders.placed");
    using MetricCollector<long> collector2 = new MetricCollector<long>(meterFactory2, OrderMetrics.MeterName, "orders.placed");

    // Act - same Meter name, same instrument name, different factories
    orderMetrics1.RecordOrderPlaced(new Order(), "web");
    orderMetrics2.RecordOrderPlaced(new Order(), "web");
    orderMetrics2.RecordOrderPlaced(new Order(), "web");

    // Assert
    using (new AssertionScope())
    {
        collector1.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(1); // only container 1's measurement
        collector2.GetMeasurementSnapshot().EvaluateAsCounter().Should().Be(2); // only container 2's measurements
    }
}
```

**Why this matters:** with a `static readonly Meter meter = new Meter("UnitTestingCookbook.Orders")` (very common, and
fine in production), the meter has **no** scope, so there's only one per process. A collector listening to it
(`meterScope: null`) receives measurements from **every** test that touches that meter, in this test or any other
test running at the same time. That's the metrics version of [Data Hangover](./README_DataHangover.md), and it makes a
test unsafe under [Parallel Processing](./README_ParallelProcessing.md).  

**NOTE:** Scope matching is exact. A collector created with `meterScope: null` does **not** see factory-created meters at all
(checked while writing this chapter: changing `E_`'s `collector1` to a `null` scope makes it collect 0 measurements,
not 3).  

---

## How do I make measurement timestamps deterministic?

`MetricCollector<T>` takes an optional `TimeProvider` and uses it to timestamp each measurement. With a
`FakeTimeProvider`, the timestamps are exact and assertable:

```csharp
public void F_Timestamps_With_FakeTimeProvider()
{
    // Arrange
    ServiceCollection services = new ServiceCollection();
    services.AddMetrics();
    services.AddSingleton<OrderMetrics>();
    using ServiceProvider serviceProvider = services.BuildServiceProvider(true);

    IMeterFactory meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
    OrderMetrics orderMetrics = serviceProvider.GetRequiredService<OrderMetrics>();

    DateTimeOffset now = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    FakeTimeProvider timeProvider = new FakeTimeProvider(now);

    using MetricCollector<long> collector = new MetricCollector<long>(meterFactory, OrderMetrics.MeterName, "orders.placed", timeProvider);

    // Act
    orderMetrics.RecordOrderPlaced(new Order(), "web");
    timeProvider.Advance(TimeSpan.FromMinutes(5));
    orderMetrics.RecordOrderPlaced(new Order(), "web");

    // Assert
    collector.GetMeasurementSnapshot()
        .Select(x => x.Timestamp)
        .Should().Equal(now, now.AddMinutes(5));
}
```

See [TimeProvider](./README_TimeProvider.md) (the source of truth for `FakeTimeProvider`).  

---

Back to [README](../README.md)
