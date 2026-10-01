using Microsoft.AspNetCore.Mvc.Testing;

using UnitTestingCookbook.Support.Models;
using UnitTestingCookbook.Support.Services;

namespace UnitTestingCookbook.Tests;

//
// Verify.NUnit https://github.com/VerifyTests/Verify - PINNED at 32.0.0, see README_SnapshotTesting.md
//
[Category("unit")]
[Category("snapshot")]
[TestFixture]
public class SnapshotTestingTests
{
    //
    // Q: How do I assert an entire object at once, without writing a .Should() per property?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I snapshot an object containing values that change every run (Guid, DateTime)?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I leave a property out of the snapshot?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I snapshot text output (CSV, HTML, etc.) as its own file type?
    //
    [Test]
    [Category("_passes")]
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

    //
    // Q: How do I snapshot a JSON string?
    //
    [Test]
    [Category("_passes")]
    public Task E_VerifyJson()
    {
        // Arrange
        const string json = """{"customer":"John","lines":[{"sku":"WIDGET","quantity":2}],"id":"5f0c7a8e-3b7a-4d4e-9c1e-2a6b8f9d0e11"}""";

        // Act / Assert
        return VerifyJson(json);
    }

    //
    // Q: How do I snapshot a parameterized ([TestCase]) test - one snapshot file per case?
    //
    [TestCase("WIDGET", 1)]
    [TestCase("GADGET", 3)]
    [Category("_passes")]
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

    //
    // Q: How do I snapshot an HTTP response (status, content type, body) from a Minimal API?
    // NOTE: See README_MinimalApi.md for WebApplicationFactory itself
    // NOTE: Verify(response) alone would NOT include the body - core Verify serializes HttpResponseMessage's
    //       properties (status, headers, request) but not its Content - so the parts that matter are picked out explicitly
    //
    [Test]
    [Category("_passes")]
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
}
