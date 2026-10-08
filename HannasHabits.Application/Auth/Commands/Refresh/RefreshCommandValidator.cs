using FluentValidation;

namespace HannasHabits.Application.Auth.Commands.Refresh;

public class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(AuthLimits.RefreshTokenMaxLength);
    }
}
