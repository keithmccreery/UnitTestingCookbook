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
