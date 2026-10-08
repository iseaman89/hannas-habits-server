using FluentValidation;

namespace HannasHabits.Application.Auth.Commands.Revoke;

public class RevokeCommandValidator : AbstractValidator<RevokeCommand>
{
    public RevokeCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(AuthLimits.RefreshTokenMaxLength);
    }
}
