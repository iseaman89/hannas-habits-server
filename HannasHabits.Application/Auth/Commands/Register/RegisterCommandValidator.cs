using FluentValidation;

namespace HannasHabits.Application.Auth.Commands.Register;

// Only the shape is checked here. The password policy (length, character classes) belongs to Identity, which
// reports violations under the same "password" key.
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(AuthLimits.EmailMaxLength)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(AuthLimits.PasswordMaxLength);

        // Both optional; a blank first name is fine (the account then shows the email's local part). Trimming happens in
        // the Identity layer.
        RuleFor(x => x.FirstName)
            .MaximumLength(AuthLimits.NameMaxLength);

        RuleFor(x => x.LastName)
            .MaximumLength(AuthLimits.NameMaxLength);
    }
}
