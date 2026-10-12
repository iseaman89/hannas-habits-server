using FluentValidation;

namespace HannasHabits.Application.Tags.Commands.RenameTag;

public class RenameTagCommandValidator : AbstractValidator<RenameTagCommand>
{
    public RenameTagCommandValidator()
    {
        RuleFor(x => x.TagId).NotEmpty();
        RuleFor(x => x.Name).MustBeATagName();
    }
}
