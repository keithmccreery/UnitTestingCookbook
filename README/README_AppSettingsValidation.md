# AppSettings Validation

## NuGet Packages Referenced

- FluentValidation https://github.com/FluentValidation/FluentValidation

## Credits

- [Options Pattern Validation in ASP.NET Core With FluentValidation](https://www.milanjovanovic.tech/blog/options-pattern-validation-in-aspnetcore-with-fluentvalidation) for the `FluentValidateOptions<T>` adapter pattern
- [ASP.NET Core Options Validation](https://code-maze.com/aspnet-configuration-options-validation/), the original inspiration for this chapter

The code is in [`UnitTestingCookbook.MinimalApi`](../UnitTestingCookbook.MinimalApi) (`AnimalApiOptions.cs`,
`NotificationOptions.cs`, `NotificationOptionsValidator.cs`, `FluentValidateOptions.cs`, and the wiring in
`Program.cs`) - **there are no NUnit tests for this chapter.** The validation runs at *host startup*
(`.ValidateOnStart()`), not in response to a method call, so there's nothing to unit test in the usual sense; what
there is to demonstrate is what happens when the app boots with bad configuration, which is what this README
shows (captured by actually running the app with bad config - see below). `MinimalApiTest.cs`'s existing tests do
still exercise this indirectly: every `WebApplicationFactory<Program>` it creates boots the app for real, which
means `appsettings.json`'s values have to stay valid or every test in that chapter starts failing at
factory-creation time.

---

## Why validate configuration at startup?

The alternative - finding out `appsettings.json` has a bad value when the code that reads it finally runs, maybe
hours after deploy, maybe only under a specific request - is worse than the app refusing to start at all.
`.ValidateOnStart()` moves that failure to the earliest possible point: `IHost.StartAsync()`, before the app ever
accepts a request.

---

## How do I validate options with Data Annotations?

The framework has this built in - decorate the options class, then chain `.ValidateDataAnnotations()`.

```csharp
public class AnimalApiOptions
{
    public const string SectionName = "AnimalApi";

    [Range(1, 1000)]
    public int MaxAnimals { get; set; }

    [Required]
    public string DefaultSpecies { get; set; } = default!;
}
```

```csharp
builder.Services.AddOptions<AnimalApiOptions>()
    .Bind(builder.Configuration.GetSection(AnimalApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Running the app with `AnimalApi:MaxAnimals` set to `0` and `AnimalApi:DefaultSpecies` unset...

```text
Microsoft.Extensions.Options.OptionsValidationException: DataAnnotation validation failed for 'AnimalApiOptions' members: 'MaxAnimals' with the error: 'The field MaxAnimals must be between 1 and 1000.'.; DataAnnotation validation failed for 'AnimalApiOptions' members: 'DefaultSpecies' with the error: 'The DefaultSpecies field is required.'.
```

...and the app never finishes starting.

---

## How do I validate options with FluentValidation?

FluentValidation has no first-party Options pattern integration (unlike Data Annotations' built-in
`.ValidateDataAnnotations()`), so it goes through a small adapter - `IValidateOptions<T>` that resolves a
FluentValidation `IValidator<T>` and translates the result.

```csharp
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public string FromEmail { get; set; } = default!;

    public int MaxRetries { get; set; }
}

public class NotificationOptionsValidator : AbstractValidator<NotificationOptions>
{
    public NotificationOptionsValidator()
    {
        RuleFor(x => x.FromEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.MaxRetries).InclusiveBetween(0, 10);
    }
}
```

```csharp
// FluentValidateOptions.cs
public class FluentValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IServiceProvider serviceProvider;
    private readonly string? name;

    public FluentValidateOptions(IServiceProvider serviceProvider, string? name)
    {
        this.serviceProvider = serviceProvider;
        this.name = name;
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (this.name is not null && this.name != name)
        {
            return ValidateOptionsResult.Skip; // this instance validates a different named options instance
        }

        ArgumentNullException.ThrowIfNull(options);

        using IServiceScope scope = serviceProvider.CreateScope();
        IValidator<TOptions> validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();
        ValidationResult result = validator.Validate(options);

        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> errors = result.Errors
            .Select(failure => $"Validation failed for {typeof(TOptions).Name}.{failure.PropertyName} with the error: {failure.ErrorMessage}")
            .ToList();

        return ValidateOptionsResult.Fail(errors);
    }
}

public static class OptionsBuilderFluentValidationExtensions
{
    public static OptionsBuilder<TOptions> ValidateFluentValidation<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(
            serviceProvider => new FluentValidateOptions<TOptions>(serviceProvider, builder.Name));

        return builder;
    }
}
```

```csharp
builder.Services.AddScoped<IValidator<NotificationOptions>, NotificationOptionsValidator>();
builder.Services.AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
    .ValidateFluentValidation()
    .ValidateOnStart();
```

Running the app with `Notifications:FromEmail` set to `not-an-email` and `Notifications:MaxRetries` set to `99`...

```text
Microsoft.Extensions.Options.OptionsValidationException: Validation failed for NotificationOptions.FromEmail with the error: 'From Email' is not a valid email address.; Validation failed for NotificationOptions.MaxRetries with the error: 'Max Retries' must be between 0 and 10. You entered 99.
```

---

## Which one should I use?

Data Annotations for simple per-property constraints (`[Required]`, `[Range]`, ...) - it's built in, no adapter
needed. Reach for FluentValidation when a rule needs to compare multiple properties, needs a dependency injected
(a validator is a resolved service, same as anything else in DI), or when Data Annotations' attribute-only model
gets awkward to express what you actually mean.

---

Back to [README](../README.md)
