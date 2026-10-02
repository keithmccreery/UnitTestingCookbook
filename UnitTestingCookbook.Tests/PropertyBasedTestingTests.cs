using CsCheck;

using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// CsCheck https://github.com/AnthonyLloyd/CsCheck - see README_PropertyBasedTesting.md
// A property is an ordinary NUnit test: .Sample(...) runs it against 100 generated inputs (by default) and throws,
// with the simplest failing input it can shrink to, if any of them return false.
//
[Category("unit")]
[Category("propertybased")]
[TestFixture]
public class PropertyBasedTestingTests
{
    // Order totals in whole cents, from $0.00 to $1,000.00. Generating cents (not raw decimals) keeps values realistic
    // and lets shrinking walk toward simple values like 50.00.
    private static readonly Gen<decimal> GenOrderTotal = Gen.Int[0, 100_000].Select(cents => cents / 100m);

    //
    // Q: How do I state a rule that must hold for every input, instead of picking examples?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_A_Shipping_IsNeverNegative
    public void A_Shipping_IsNeverNegative()
    {
        Gen.Select(GenOrderTotal, Gen.Bool)
            .Sample((orderTotal, isExpress) => ShippingCalculator.Calculate(orderTotal, isExpress) >= 0m);
    }
    // end-snippet

    //
    // Q: How do I test a relationship between two calls, when I don't want to restate the exact expected value?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_B_Express_CostsExactlyTheSurchargeMore
    public void B_Express_CostsExactlyTheSurchargeMore()
    {
        GenOrderTotal.Sample(orderTotal =>
            ShippingCalculator.Calculate(orderTotal, isExpress: true) - ShippingCalculator.Calculate(orderTotal, isExpress: false)
                == ShippingCalculator.ExpressSurcharge);
    }
    // end-snippet

    //
    // Q: How do I test a rule like "free shipping if and only if the order is at least $50"?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_C_FreeShipping_IfAndOnlyIf_AtThreshold
    public void C_FreeShipping_IfAndOnlyIf_AtThreshold()
    {
        GenOrderTotal.Sample(orderTotal =>
            (ShippingCalculator.Calculate(orderTotal, isExpress: false) == 0m)
                == (orderTotal >= ShippingCalculator.FreeShippingThreshold));
    }
    // end-snippet

    //
    // Q: What does a failing property look like? (Fails by design - excluded from CI.)
    // NOTE: "Shipping always costs something" is a wrong assumption - orders of $50 or more ship free.
    //
    [Test]
    [Category("_fails")]
    // begin-snippet: PropertyBasedTestingTests_D_WrongAssumption_ShrinksToTheBoundary
    public void D_WrongAssumption_ShrinksToTheBoundary()
    {
        GenOrderTotal.Sample(orderTotal => ShippingCalculator.Calculate(orderTotal, isExpress: false) > 0m, iter: 10_000);
    }
    // end-snippet

    // begin-snippet: PropertyBasedTestingTests_GenOrder
    // Generators compose with LINQ. Gen.String produces any characters at all - including commas, quotes,
    // and newlines - which is exactly what a hand-written example tends to leave out.
    private static readonly Gen<OrderLine> GenOrderLine =
        from sku in Gen.String
        from quantity in Gen.Int[1, 10]
        from unitPrice in Gen.Int[1, 10_000].Select(cents => cents / 100m)
        select new OrderLine { Sku = sku, Quantity = quantity, UnitPrice = unitPrice };

    private static readonly Gen<Order> GenOrder =
        GenOrderLine.List[0, 5].Select(lines => new Order { Customer = "John", Lines = lines });

    // Order has no ToString(), so tell CsCheck how to print a failing example
    private static string Print(Order order) =>
        "Skus: [" + string.Join(", ", order.Lines.Select(line => System.Text.Json.JsonSerializer.Serialize(line.Sku))) + "]";
    // end-snippet

    //
    // Q: How do I generate whole objects, not just numbers?
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_E_Csv_TotalIsSumOfLines
    public void E_Csv_TotalIsSumOfLines()
    {
        GenOrder.Sample(
            order =>
            {
                List<string[]> rows = ParseCsv(OrderCsvExporter.ToCsv(order));
                decimal sumOfLineTotals = rows.Skip(1).SkipLast(1).Sum(row => decimal.Parse(row[3], System.Globalization.CultureInfo.InvariantCulture));
                decimal total = decimal.Parse(rows[^1][3], System.Globalization.CultureInfo.InvariantCulture);
                return total == sumOfLineTotals;
            },
            print: Print);
    }
    // end-snippet

    //
    // Q: How do I check that data survives a round trip (write it out, read it back)?
    // NOTE: This property found a real bug - OrderCsvExporter wrote SKUs without CSV escaping, so a SKU containing a
    //       comma, quote, or newline corrupted its row. See README_PropertyBasedTesting.md.
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: PropertyBasedTestingTests_F_Csv_SkusRoundTrip
    public void F_Csv_SkusRoundTrip()
    {
        GenOrder.Sample(
            order =>
            {
                List<string[]> rows = ParseCsv(OrderCsvExporter.ToCsv(order));
                IEnumerable<string> skusReadBack = rows.Skip(1).SkipLast(1).Select(row => row[0]);
                return rows.Count == order.Lines.Count + 2 && skusReadBack.SequenceEqual(order.Lines.Select(line => line.Sku));
            },
            print: Print);
    }
    // end-snippet

    // A minimal RFC 4180 reader (quoted fields may contain commas, doubled quotes, and line breaks) - just enough for
    // the round-trip property; production code would use a CSV library.
    private static List<string[]> ParseCsv(string csv)
    {
        List<string[]> rows = [];
        List<string> fields = [];
        System.Text.StringBuilder field = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    i++;
                }

                fields.Add(field.ToString());
                field.Clear();
                rows.Add(fields.ToArray());
                fields.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }

        return rows;
    }
}
