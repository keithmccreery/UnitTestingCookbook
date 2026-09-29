using UnitTestingCookbook.MinimalApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AnimalStore>();

WebApplication app = builder.Build();

app.MapGet("/ping", () => Results.Ok("pong"));

app.MapGet("/animals/{id:int}", (int id, AnimalStore store) =>
    store.TryGet(id, out AnimalRecord? animal)
        ? Results.Ok(animal)
        : Results.NotFound());

app.MapPost("/animals", (AnimalRecord animal, AnimalStore store) =>
{
    AnimalRecord created = store.Add(animal);
    return Results.Created($"/animals/{created.Id}", created);
});

app.Run();

// Makes the auto-generated top-level-statements Program class visible to
// WebApplicationFactory<Program> in the test project (it's `internal` by default).
public partial class Program;
