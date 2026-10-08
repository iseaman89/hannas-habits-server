using FluentValidation;

namespace HannasHabits.Application.Auth.Commands.Login;

// No format checks on purpose: a login must not reveal which rule an existing account would have failed.
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(AuthLimits.EmailMaxLength);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(AuthLimits.PasswordMaxLength);
    }
}
