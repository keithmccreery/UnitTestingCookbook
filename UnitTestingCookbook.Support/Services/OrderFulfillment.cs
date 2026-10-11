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
