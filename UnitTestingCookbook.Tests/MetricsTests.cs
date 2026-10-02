using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;

using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// Microsoft.Extensions.Diagnostics.Testing https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#test-custom-metrics
//
[Category("unit")]
[Category("metrics")]
[TestFixture]
public class MetricsTests
{
    //
    // Q: How do I assert a Counter was incremented?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert the values recorded by a Histogram?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert on a measurement's tags (dimensions)?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I assert an ObservableGauge (a value that's polled, not pushed)?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: Do metrics from one test leak into another test's collector?
    // NOTE: This is why OrderMetrics uses IMeterFactory instead of a static `new Meter(...)`
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I make measurement timestamps deterministic?
    // NOTE: See README_TimeProvider.md for FakeTimeProvider itself
    //
    [Test]
    [Category("_passes")]
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
}
