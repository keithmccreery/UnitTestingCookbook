using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.Options;

namespace UnitTestingCookbook.MinimalApi;

// https://www.milanjovanovic.tech/blog/options-pattern-validation-in-aspnetcore-with-fluentvalidation
// FluentValidation has no first-party integration with IOptions<T> validation (unlike Data
// Annotations' built-in .ValidateDataAnnotations()) - this adapter is the standard way to bridge
// an AbstractValidator<T> into the Options pattern's .ValidateOnStart() pipeline.
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
