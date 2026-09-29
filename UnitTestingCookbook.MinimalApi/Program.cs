using FluentValidation;

using Microsoft.Extensions.Options;

using UnitTestingCookbook.MinimalApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AnimalStore>();

// Data Annotations: validated via the framework's own .ValidateDataAnnotations().
builder.Services.AddOptions<AnimalApiOptions>()
    .Bind(builder.Configuration.GetSection(AnimalApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// FluentValidation: no built-in Options-pattern integration, so it goes through the
// FluentValidateOptions<T> adapter (see FluentValidateOptions.cs) instead.
builder.Services.AddScoped<IValidator<NotificationOptions>, NotificationOptionsValidator>();
builder.Services.AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
    .ValidateFluentValidation()
    .ValidateOnStart();

WebApplication app = builder.Build();

app.MapGet("/ping", () => Results.Ok("pong"));

app.MapGet("/config", (IOptions<AnimalApiOptions> animalApiOptions, IOptions<NotificationOptions> notificationOptions) =>
    Results.Ok(new
    {
        animalApiOptions.Value.MaxAnimals,
        animalApiOptions.Value.DefaultSpecies,
        notificationOptions.Value.FromEmail,
        notificationOptions.Value.MaxRetries,
    }));

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
