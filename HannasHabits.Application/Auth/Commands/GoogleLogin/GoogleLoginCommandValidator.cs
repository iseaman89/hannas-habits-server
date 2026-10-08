using FluentValidation;

namespace HannasHabits.Application.Auth.Commands.GoogleLogin;

public class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .MaximumLength(AuthLimits.GoogleIdTokenMaxLength);
    }
}
