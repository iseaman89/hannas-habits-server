using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Resolutions.Commands.AddResolution;

// First line of defence with per-field messages; the Domain enforces the same rules again.
public class AddResolutionCommandValidator : AbstractValidator<AddResolutionCommand>
{
    public AddResolutionCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(Resolution.MinYear, Resolution.MaxYear);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(Resolution.TitleMaxLength);

        // No link at all is fine; an all-zero id can never belong to a habit.
        RuleFor(x => x.HabitId).NotEqual(Guid.Empty);
    }
}
