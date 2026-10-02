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
