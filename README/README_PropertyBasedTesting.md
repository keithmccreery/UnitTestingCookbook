# Property-Based Testing

## NuGet Packages Referenced

- CsCheck https://github.com/AnthonyLloyd/CsCheck (Apache-2.0)

All examples are located in `UnitTestingCookbook.Tests` -> [`PropertyBasedTestingTests`](../UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs)  

**Why CsCheck and not FsCheck?** [FsCheck](https://github.com/fscheck/FsCheck) is the longer-established .NET
property-based testing library, but its NUnit integration (`FsCheck.NUnit` 3.4.0) declares support for NUnit
`[4.0.0, 5.0.0)`, and this repo is on NUnit 5. CsCheck is written in C#, has no dependencies, and doesn't need a test
framework integration at all: a property is a normal `[Test]` that calls `.Sample(...)`.  

---

## What is property-based testing?

An example-based test checks **one input you picked**: "an order of $10 costs $5.99 to ship." A property-based test
states a **rule that must hold for every input**, "shipping is never negative", and the library checks it against
many generated inputs (100 by default), including ones you'd never think to write by hand.

When a rule fails, the library **shrinks** the failing input: it keeps trying simpler inputs that still fail, and
reports the simplest one it finds. You don't get a random 900-character string; you get the smallest version of the
problem.

Property-based tests complement example-based ones rather than replacing them. Examples document specific cases;
properties find the cases you didn't know to write. Compare [Data Driven](./README_DataDriven.md) tests, where *you*
choose the inputs, and [Bogus](./README_Bogus.md), which generates realistic-looking data but doesn't search for
failures or shrink them.

---

## How do I state a rule that must hold for every input, instead of picking examples?

`GenOrderTotal` generates order totals from $0.00 to $1,000.00 in whole cents (`Gen.Int[0, 100_000]`, then divided
by 100). `Gen.Select(...)` combines generators, and `.Sample(...)` runs the property, which fails by returning `false`
(or throwing):

<!-- snippet: PropertyBasedTestingTests_A_Shipping_IsNeverNegative -->
<a id='snippet-PropertyBasedTestingTests_A_Shipping_IsNeverNegative'></a>
```cs
public void A_Shipping_IsNeverNegative()
{
    Gen.Select(GenOrderTotal, Gen.Bool)
        .Sample((orderTotal, isExpress) => ShippingCalculator.Calculate(orderTotal, isExpress) >= 0m);
}
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L27-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_A_Shipping_IsNeverNegative' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I test a relationship between two calls, when I don't want to restate the exact expected value?

Instead of re-deriving what `Calculate()` *should* return (which just copies the implementation into the test), test a
relationship that must hold whatever the total is: express shipping always costs exactly the surcharge more.

<!-- snippet: PropertyBasedTestingTests_B_Express_CostsExactlyTheSurchargeMore -->
<a id='snippet-PropertyBasedTestingTests_B_Express_CostsExactlyTheSurchargeMore'></a>
```cs
public void B_Express_CostsExactlyTheSurchargeMore()
{
    GenOrderTotal.Sample(orderTotal =>
        ShippingCalculator.Calculate(orderTotal, isExpress: true) - ShippingCalculator.Calculate(orderTotal, isExpress: false)
            == ShippingCalculator.ExpressSurcharge);
}
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L40-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_B_Express_CostsExactlyTheSurchargeMore' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I test a rule like "free shipping if and only if the order is at least $50"?

An "if and only if" rule is two booleans that must always agree:

<!-- snippet: PropertyBasedTestingTests_C_FreeShipping_IfAndOnlyIf_AtThreshold -->
<a id='snippet-PropertyBasedTestingTests_C_FreeShipping_IfAndOnlyIf_AtThreshold'></a>
```cs
public void C_FreeShipping_IfAndOnlyIf_AtThreshold()
{
    GenOrderTotal.Sample(orderTotal =>
        (ShippingCalculator.Calculate(orderTotal, isExpress: false) == 0m)
            == (orderTotal >= ShippingCalculator.FreeShippingThreshold));
}
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L54-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_C_FreeShipping_IfAndOnlyIf_AtThreshold' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## What does a failing property look like?

This one states a wrong assumption, "shipping always costs something", forgetting that orders of $50 or more ship
free. It's tagged `_fails`: this repo tags every test with its *intended* outcome, and `_fails` tests fail **by
design** to demonstrate failure output, so CI excludes them.

<!-- snippet: PropertyBasedTestingTests_D_WrongAssumption_ShrinksToTheBoundary -->
<a id='snippet-PropertyBasedTestingTests_D_WrongAssumption_ShrinksToTheBoundary'></a>
```cs
public void D_WrongAssumption_ShrinksToTheBoundary()
{
    GenOrderTotal.Sample(orderTotal => ShippingCalculator.Calculate(orderTotal, isExpress: false) > 0m, iter: 10_000);
}
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L69-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_D_WrongAssumption_ShrinksToTheBoundary' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Real output from one run:

```
CsCheck.CsCheckException : Set seed: "3XsyaQoxHlq6" or -e CsCheck_Seed=3XsyaQoxHlq6 to reproduce (7 shrinks, 9,454 skipped, 10,000 total).
50.04
```

The failing input it reports, **50.04**, sits right next to the real rule's boundary ($50.00). The first failure it
happened to generate could have been any total above $50; shrinking walked it down toward the edge. Across runs this
lands on or near the boundary (50, 50.01, 50.04, 50.15, ...), which is exactly the hint you need to spot the
forgotten rule. `iter: 10_000` gives the shrinker more attempts than the default 100.

### How do I replay a failure?

Paste the seed into the test (`.Sample(..., seed: "3XsyaQoxHlq6")`) or set it as an environment variable for one run:

```
CsCheck_Seed=3XsyaQoxHlq6 dotnet test --filter "FullyQualifiedName~D_WrongAssumption_ShrinksToTheBoundary"
```

The seed reproduces that failing input immediately. Replaying it twice here, the first replay reported 50.04 again
with 0 shrinks. CsCheck uses the seed for the **first** iteration and then keeps exploring, though, so a replay can
go on to find an even simpler failure (the second replay went on to report 50.01). `CsCheck_Iter=10000` similarly
raises the iteration count for a single run without changing the code.

---

## How do I generate whole objects, not just numbers?

Generators compose with LINQ query syntax. `Gen.String` generates **any** characters, including commas, quotes,
and line breaks:

<!-- snippet: PropertyBasedTestingTests_GenOrder -->
<a id='snippet-PropertyBasedTestingTests_GenOrder'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L76-L91' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_GenOrder' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`print:` tells CsCheck how to describe a failing input. Without it, a failure prints just the type name, since
`Order` has no `ToString()`.

<!-- snippet: PropertyBasedTestingTests_E_Csv_TotalIsSumOfLines -->
<a id='snippet-PropertyBasedTestingTests_E_Csv_TotalIsSumOfLines'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L98-L111' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_E_Csv_TotalIsSumOfLines' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

---

## How do I check that data survives a round trip (write it out, read it back)?

A round trip is one of the most useful properties: whatever goes in must come back out. This one writes an order to
CSV with [`OrderCsvExporter`](../UnitTestingCookbook.Support/Services/OrderCsvExporter.cs), reads it back (with a
small RFC 4180 reader in the test class), and checks every SKU survived unchanged:

<!-- snippet: PropertyBasedTestingTests_F_Csv_SkusRoundTrip -->
<a id='snippet-PropertyBasedTestingTests_F_Csv_SkusRoundTrip'></a>
```cs
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
```
<sup><a href='/UnitTestingCookbook.Tests/PropertyBasedTestingTests.cs#L120-L132' title='Snippet source file'>snippet source</a> | <a href='#snippet-PropertyBasedTestingTests_F_Csv_SkusRoundTrip' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### This property found a real bug

When these properties were first written, `E_` and `F_` **failed**. `OrderCsvExporter` wrote each SKU straight into
the CSV, so a SKU containing a comma, a double quote, or a line break corrupted its row. CsCheck shrank the failures to
SKUs like these (real output, from the runs before the fix):

```
Skus: ["G#,S6sjP^"]
Skus: ["M#F9Fy~=90_A!4JCytzA"i(NP"]
```

The example-based tests in [Snapshot Testing](./README_SnapshotTesting.md) never caught it, because their SKUs are
`WIDGET` and `GADGET`. The fix follows [RFC 4180](https://www.rfc-editor.org/rfc/rfc4180): a field containing a comma,
quote, or line break is wrapped in double quotes, with any inner quote doubled.

<!-- snippet: OrderCsvExporter.cs -->
<a id='snippet-OrderCsvExporter.cs'></a>
```cs
using System.Globalization;
using System.Text;

using UnitTestingCookbook.Support.Models;

namespace UnitTestingCookbook.Support.Services;

public static class OrderCsvExporter
{
    public static string ToCsv(Order order)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Sku,Quantity,UnitPrice,LineTotal");

        foreach (OrderLine line in order.Lines)
        {
            csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{Escape(line.Sku)},{line.Quantity},{line.UnitPrice:0.00},{line.Quantity * line.UnitPrice:0.00}"));
        }

        csv.AppendLine(string.Create(CultureInfo.InvariantCulture, $"TOTAL,,,{order.Total:0.00}"));

        return csv.ToString();
    }

    // RFC 4180: a field containing a comma, double quote, or line break must be wrapped in double quotes, with any
    // double quote inside it doubled. (Found by PropertyBasedTestingTests.F_Csv_SkusRoundTrip.)
    private static string Escape(string field) =>
        field.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : field;
}
```
<sup><a href='/UnitTestingCookbook.Support/Services/OrderCsvExporter.cs#L1-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-OrderCsvExporter.cs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

After the fix, all five passing properties held for 3 runs × 10,000 generated inputs each, and the existing CSV
snapshots were unchanged.  

---

## When are properties worth it?

Properties shine when there's a rule that's easy to state but hard to cover by example:
- **Invariants** - "never negative", "the total equals the sum of the lines".
- **Relationships** - "express costs exactly the surcharge more", "sorting twice equals sorting once".
- **Round trips** - serialize/deserialize, export/import, encode/decode.
- **"If and only if" rules** - "free shipping exactly when the total is at least $50".

If the only way to state the expected result is to copy the implementation's formula into the test, a property
doesn't add much. Use a few well-chosen examples there instead.

---

Back to [README](../README.md)
