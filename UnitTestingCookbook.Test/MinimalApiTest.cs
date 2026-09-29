using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using UnitTestingCookbook.MinimalApi;

namespace UnitTestingCookbook.Test;

//
// Microsoft.AspNetCore.Mvc.Testing https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
//
[Category("unit")]
[Category("minimalapi")]
[TestFixture]
public class MinimalApiTest
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    //
    // Q: How do I test a Minimal API end-to-end without a real HTTP port?
    //
    // WebApplicationFactory<TEntryPoint> boots the app in-memory (via TestServer) - the HttpClient
    // it hands back makes real HttpClient calls, but they never leave the process.
    //
    [Test]
    [Category("_passes")]
    public async Task A_Ping()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await _client.GetAsync("/ping");

        // Assert
        using (new AssertionScope())
        {
            response.Should().Be200Ok();
            (await response.Content.ReadAsStringAsync()).Should().Be("\"pong\"");
        }
    }

    //
    // Q: How do I test a GET endpoint that returns a resource?
    //
    [Test]
    [Category("_passes")]
    public async Task B_GetAnimal_Found()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await _client.GetAsync("/animals/1");

        // Assert
        response.Should().Be200Ok()
            .And.BeAs(new { id = 1, species = "Blue Whale" });
    }

    //
    // Q: How do I test a GET endpoint's not-found path?
    //
    [Test]
    [Category("_passes")]
    public async Task C_GetAnimal_NotFound()
    {
        // Arrange

        // Act
        HttpResponseMessage response = await _client.GetAsync("/animals/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    //
    // Q: How do I test a POST endpoint that creates a resource?
    //
    [Test]
    [Category("_passes")]
    public async Task D_CreateAnimal()
    {
        // Arrange
        using StringContent content = new StringContent(
            JsonSerializer.Serialize(new { species = "Orca" }),
            Encoding.UTF8,
            "application/json");

        // Act
        HttpResponseMessage response = await _client.PostAsync("/animals", content);

        // Assert
        using (new AssertionScope())
        {
            response.Should().Be201Created();
            response.Headers.Location.Should().Be("/animals/2"); // seeded animal is Id 1
        }
    }
}
