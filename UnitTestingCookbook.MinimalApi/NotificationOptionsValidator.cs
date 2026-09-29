using FluentValidation;

namespace UnitTestingCookbook.MinimalApi;

public class NotificationOptionsValidator : AbstractValidator<NotificationOptions>
{
    public NotificationOptionsValidator()
    {
        RuleFor(x => x.FromEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.MaxRetries).InclusiveBetween(0, 10);
    }
}
