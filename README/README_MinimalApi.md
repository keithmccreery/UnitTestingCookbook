# MinimalApi Integration Testing

## NuGet Packages Referenced

- Microsoft.AspNetCore.Mvc.Testing https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests

All examples are located in `UnitTestingCookbook.Test` -> [`MinimalApiTest`](../UnitTestingCookbook.Test/MinimalApiTest.cs)

The API under test is [`UnitTestingCookbook.MinimalApi`](../UnitTestingCookbook.MinimalApi) - a small,
separate ASP.NET Core project (not folded into `UnitTestingCookbook.Support`, since it needs the
`Microsoft.NET.Sdk.Web` SDK and a real app host, unlike the plain POCOs the rest of that project holds).

**NOTE:** This same project also hosts [AppSettings Validation](./README_AppSettingsValidation.md)'s
`AnimalApiOptions`/`NotificationOptions` and [Connection String Validation](./README_ConnectionStringValidation.md)'s
`ConnectionStringOptions` - those chapters' `.ValidateOnStart()` calls run every time a test here creates a
`WebApplicationFactory<Program>`, so `appsettings.json` has to stay valid or every test in *this* chapter starts
failing too.  

---

## What's different about this from every other HTTP-touching chapter?

[HttpClientFactory](./README_HttpClientFactory.md), [WireMockNet](./README_WireMockNet.md), and
[Docker](./README_Docker.md) all test code that **consumes** an HTTP API (real, mocked, or containerized).  
This chapter is the other direction: testing an ASP.NET Core app you wrote, end-to-end, including its own
routing, model binding, and dependency injection - without deploying it anywhere.

`WebApplicationFactory<TEntryPoint>` boots the app in-memory (via `TestServer`) and hands back a real
`HttpClient`. Calls through that client exercise the actual app pipeline; they just never leave the process or
open a real socket.

---

## How do I make my API's `Program` visible to the test project?

Minimal APIs (top-level statements) generate an `internal partial class Program` automatically - not visible
outside its own assembly, and `WebApplicationFactory<TEntryPoint>` needs a public `TEntryPoint` it can see from
the test project. Add one line at the end of `Program.cs`:

```csharp
// Makes the auto-generated top-level-statements Program class visible to
// WebApplicationFactory<Program> in the test project (it's `internal` by default).
public partial class Program;
```

---

## How do I test a Minimal API end-to-end without a real HTTP port?

Create the factory once (`[OneTimeSetUp]`) and reuse the `HttpClient` it hands back for every test in the fixture.

```csharp
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
```

**NOTE:** `Should().Be200Ok()` / `.BeAs()` below are from `AwesomeAssertions.Web` - see
[AwesomeAssertions Add-Ons](./README_AwesomeAssertionsAddOns.md) for the rest of what it offers.  

---

## How do I test a GET endpoint that returns a resource?

```csharp
public async Task B_GetAnimal_Found()
{
    // Arrange

    // Act
    HttpResponseMessage response = await _client.GetAsync("/animals/1");

    // Assert
    response.Should().Be200Ok()
        .And.BeAs(new { id = 1, species = "Blue Whale" });
}
```

**NOTE:** The anonymous object's property names are lower-`camelCase` (`id`, `species`) because that's what
Minimal APIs serialize JSON as by default - `.BeAs()` compares against the actual response body, so it has to
match the real casing, not the C# record's `PascalCase` property names.  

---

## How do I test a GET endpoint's not-found path?

```csharp
public async Task C_GetAnimal_NotFound()
{
    // Arrange

    // Act
    HttpResponseMessage response = await _client.GetAsync("/animals/999");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

---

## How do I test a POST endpoint that creates a resource?

```csharp
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
```

**NOTE:** The API's `AnimalStore` is registered `AddSingleton` (see `Program.cs`), so it - and its `nextId`
counter - persists across every test in the fixture, same as the real app would in a single running process.
Only this one test creates an animal, so it reliably gets Id `2` regardless of test execution order.  

---

Back to [README](../README.md)
