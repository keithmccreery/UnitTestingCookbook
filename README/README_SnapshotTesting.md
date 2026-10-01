# Snapshot Testing

## NuGet Packages Referenced

- Verify.NUnit https://github.com/VerifyTests/Verify - **pinned at 32.0.0** (see below)

All examples are located in `UnitTestingCookbook.Tests` -> [`SnapshotTestingTests`](../UnitTestingCookbook.Tests/SnapshotTestingTests.cs)  
Accepted snapshots are in [`UnitTestingCookbook.Tests/Snapshots`](../UnitTestingCookbook.Tests/Snapshots)  

**Why pinned at 32.0.0?** Verify is still MIT licensed, but it now participates in the
[Open Source Maintenance Fee](https://opensourcemaintenancefee.org): every version released **after 1 September 2026**
requires a paid sponsorship from organizations that generate revenue and from government agencies. Versions released on or before that date
don't. 32.0.0 (released 2026-08-26) is the last stable release before the cutoff. It also predates the build-time
"SponsorCheck" added in v33. `Directory.Packages.props` has a do-not-bump comment on the pin.  

**NUnit 5 caveat:** Verify.NUnit 32.0.0 was built against NUnit 4.6.1. This repo is on NUnit 5.0.0, and the first
Verify.NUnit built against NUnit 5 is 33.2.0, which falls under the fee. Everything in this chapter works on
32.0.0 + NUnit 5, including `[TestCase]` parameter naming (`F_` below). However, a future NUnit 5.x change that breaks Verify.NUnit
won't get a fix on the pinned version.  

**Alternatives considered:**
- [Snapshooter](https://github.com/SwissLife-OSS/snapshooter) (`Snapshooter.NUnit`, MIT, still maintained) - JSON
and plain-text snapshots only, with good JSON-path field matching (ignore / hash / type-check a field), but no
file-type snapshots and no add-on ecosystem (PDF, images, Excel, etc.).
- [Snapper](https://github.com/theramis/Snapper) - last stable release December 2023, effectively unmaintained.

---

## How does snapshot testing work?

Instead of writing a `.Should()` per property, the test hands the whole result to `Verify()`, which serializes it to
text and compares it against a previously approved file.

1. **First run:** there's no approved file yet, so the test **fails** and writes `<Class>.<Method>.received.txt`.
2. **Review and accept:** inspect the `.received` file. If it's correct, rename it to `.verified.txt` (or accept it in
your diff tool), and commit the `.verified` file alongside the test.
3. **Every later run:** the output is compared to the `.verified` file. If it matches, the test passes. Any difference fails
the test, writes a new `.received` file and (locally) opens a diff tool showing exactly what changed.

`.received` files are git-ignored (`*.received.*` in `.gitignore`); `.verified` files are source.  

### Repo setup

**Snapshot location.** By default, snapshots go next to the test's `.cs` file. A module initializer sends them to a
`Snapshots` folder instead:

```csharp
using System.Runtime.CompilerServices;

namespace UnitTestingCookbook.Tests;

public static class VerifyModuleInitializer
{
    //
    // Runs once, when the test assembly loads - before any test (or NUnit itself) touches Verify.
    // Puts every *.verified.* / *.received.* file under UnitTestingCookbook.Tests/Snapshots/ instead of
    // next to each test's .cs file, so snapshots don't clutter the project root.
    //
    [ModuleInitializer]
    public static void Initialize()
    {
        Verifier.UseProjectRelativeDirectory("Snapshots");
    }
}
```

**Line endings and encoding.** Verify writes snapshot files as UTF-8 with a BOM and compares them byte for byte, so two repo
settings keep editors and git from changing them:
- `.gitattributes` - `*.verified.{txt,csv,json,xml}` are `text eol=lf working-tree-encoding=UTF-8`, so a snapshot
accepted on Windows matches exactly on Linux CI.
- `.editorconfig` - a `[*.{received,verified}.{csv,json,txt,xml}]` section overrides the repo-wide `[*]` rules
(`charset = utf-8-bom`, `insert_final_newline = false`, `trim_trailing_whitespace = false`) so saving a snapshot in
an editor doesn't break it.

---

## How do I assert an entire object at once, without writing a .Should() per property?

```csharp
public Task A_Verify_Object()
{
    // Arrange
    Product product = new Product
    {
        Id = 42,
        Name = "Widget",
        Price = 9.99m,
    };

    // Act / Assert
    return Verify(product);
}
```

`Snapshots/SnapshotTestingTests.A_Verify_Object.verified.txt`:

```text
{
  Id: 42,
  Name: Widget,
  Price: 9.99
}
```

Verify's serializer is deliberately terse: no quotes around strings or property names, which keeps diffs readable.  

---

## How do I snapshot an object containing values that change every run (Guid, DateTime)?

This works with no configuration. Verify **scrubs** `Guid`s and `DateTime`s by default, replacing each distinct value
with a stable placeholder (`Guid_1`, `Guid_2`, `DateTime_1`, ...). The snapshot stays the same across runs, but
still shows whether two fields held the *same* value (both `Guid_1`) or *different* values (`Guid_1` vs. `Guid_2`).

```csharp
public Task B_Verify_AutoScrubbing()
{
    // Arrange
    Order order = new Order
    {
        Id = Guid.NewGuid(), // different every run
        CreatedUtc = DateTime.UtcNow, // different every run
        Customer = "John",
        Lines =
        [
            new OrderLine { Sku = "WIDGET", Quantity = 2, UnitPrice = 9.99m },
            new OrderLine { Sku = "GADGET", Quantity = 1, UnitPrice = 24.50m },
        ],
    };

    // Act / Assert
    return Verify(order);
}
```

`Snapshots/SnapshotTestingTests.B_Verify_AutoScrubbing.verified.txt`:

```text
{
  Id: Guid_1,
  CreatedUtc: DateTime_1,
  Customer: John,
  Lines: [
    {
      Sku: WIDGET,
      Quantity: 2,
      UnitPrice: 9.99
    },
    {
      Sku: GADGET,
      Quantity: 1,
      UnitPrice: 24.50
    }
  ],
  Total: 44.48
}
```

**NOTE:** `InternalNotes` is `null` here, and Verify leaves out `null` members by default. The computed `Total`
property **is** included. A snapshot covers everything the object exposes, which is the point: a new or changed property
fails the test until someone reviews it and accepts the new snapshot.  

---

## How do I leave a property out of the snapshot?

`.IgnoreMember<T>(x => x.Member)` removes it completely. (`.ScrubMember<T>(...)` keeps the property name but replaces
its value with `{Scrubbed}`, if the property's *presence* matters but its value doesn't.)

```csharp
public Task C_Verify_IgnoreMember()
{
    // Arrange
    Order order = new Order
    {
        Id = Guid.NewGuid(),
        CreatedUtc = DateTime.UtcNow,
        Customer = "John",
        InternalNotes = "changes constantly, not part of the contract",
        Lines = [new OrderLine { Sku = "WIDGET", Quantity = 1, UnitPrice = 9.99m }],
    };

    // Act / Assert
    return Verify(order)
        .IgnoreMember<Order>(x => x.InternalNotes);
}
```

`Snapshots/SnapshotTestingTests.C_Verify_IgnoreMember.verified.txt`:

```text
{
  Id: Guid_1,
  CreatedUtc: DateTime_1,
  Customer: John,
  Lines: [
    {
      Sku: WIDGET,
      Quantity: 1,
      UnitPrice: 9.99
    }
  ],
  Total: 9.99
}
```

---

## How do I snapshot text output (CSV, HTML, etc.) as its own file type?

Pass a `string` plus an `extension`. The snapshot is saved as-is, with no serialization, as a `.verified.csv`, so it
opens (and diffs) as a normal CSV file.

```csharp
public Task D_Verify_Text_Csv()
{
    // Arrange
    Order order = new Order
    {
        Customer = "John",
        Lines =
        [
            new OrderLine { Sku = "WIDGET", Quantity = 2, UnitPrice = 9.99m },
            new OrderLine { Sku = "GADGET", Quantity = 1, UnitPrice = 24.50m },
        ],
    };

    // Act
    string csv = OrderCsvExporter.ToCsv(order);

    // Assert
    return Verify(csv, extension: "csv");
}
```

`Snapshots/SnapshotTestingTests.D_Verify_Text_Csv.verified.csv`:

```text
Sku,Quantity,UnitPrice,LineTotal
WIDGET,2,9.99,19.98
GADGET,1,24.50,24.50
TOTAL,,,44.48
```

---

## How do I snapshot a JSON string?

`VerifyJson()` **parses** the JSON instead of comparing it as raw text, so the snapshot is formatted, and the
same Guid/DateTime scrubbing applies to values *inside* the JSON. Whitespace or formatting changes in the source
JSON don't fail the test; only content changes do.

```csharp
public Task E_VerifyJson()
{
    // Arrange
    const string json = """{"customer":"John","lines":[{"sku":"WIDGET","quantity":2}],"id":"5f0c7a8e-3b7a-4d4e-9c1e-2a6b8f9d0e11"}""";

    // Act / Assert
    return VerifyJson(json);
}
```

`Snapshots/SnapshotTestingTests.E_VerifyJson.verified.txt`:

```text
{
  customer: John,
  lines: [
    {
      sku: WIDGET,
      quantity: 2
    }
  ],
  id: Guid_1
}
```

---

## How do I snapshot a parameterized ([TestCase]) test - one snapshot file per case?

No extra code needed. Verify.NUnit reads the current test's arguments from NUnit and adds them to the file name,
so each `[TestCase]` gets its own snapshot:

```csharp
public Task F_Verify_Parameterized(string sku, int quantity)
{
    // Arrange
    Order order = new Order
    {
        Customer = "John",
        Lines = [new OrderLine { Sku = sku, Quantity = quantity, UnitPrice = 10m }],
    };

    // Act
    string csv = OrderCsvExporter.ToCsv(order);

    // Assert
    return Verify(csv, extension: "csv");
}
```

`Snapshots/SnapshotTestingTests.F_Verify_Parameterized_sku=WIDGET_quantity=1.verified.csv`:

```text
Sku,Quantity,UnitPrice,LineTotal
WIDGET,1,10.00,10.00
TOTAL,,,10.00
```

`Snapshots/SnapshotTestingTests.F_Verify_Parameterized_sku=GADGET_quantity=3.verified.csv`:

```text
Sku,Quantity,UnitPrice,LineTotal
GADGET,3,10.00,30.00
TOTAL,,,30.00
```

---

## How do I snapshot an HTTP response (status, content type, body) from a Minimal API?

**Watch out:** `Verify(response)` on an `HttpResponseMessage` serializes the response's *properties* (status,
headers, the request), but **not its body**. That snapshot would keep passing even if the API's response body
changed. The add-on package [Verify.Http](https://github.com/VerifyTests/Verify.Http) adds body support (not used here: any
Verify add-on would also need pinning to a release from on or before 2026-09-01). Without an add-on, pick out what matters
explicitly:

```csharp
public async Task G_Verify_HttpResponse()
{
    // Arrange
    using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>();
    using HttpClient client = factory.CreateClient();

    // Act
    using HttpResponseMessage response = await client.GetAsync("/animals/1");

    // Assert
    await Verify(new
    {
        response.StatusCode,
        ContentType = response.Content.Headers.ContentType?.ToString(),
        Body = await response.Content.ReadAsStringAsync(),
    });
}
```

`Snapshots/SnapshotTestingTests.G_Verify_HttpResponse.verified.txt`:

```text
{
  StatusCode: OK,
  ContentType: application/json; charset=utf-8,
  Body: {"id":1,"species":"Blue Whale"}
}
```

See [MinimalApi Integration Testing](./README_MinimalApi.md) (the source of truth for `WebApplicationFactory`).  

---

Back to [README](../README.md)
