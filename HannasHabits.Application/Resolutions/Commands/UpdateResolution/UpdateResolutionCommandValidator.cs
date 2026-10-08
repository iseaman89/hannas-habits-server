using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Resolutions.Commands.UpdateResolution;

public class UpdateResolutionCommandValidator : AbstractValidator<UpdateResolutionCommand>
{
    public UpdateResolutionCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(Resolution.MinYear, Resolution.MaxYear);

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(Resolution.TitleMaxLength);

        // The update replaces the resolution: forgetting "kept" must not quietly re-open a kept one.
        RuleFor(x => x.Kept).NotNull();

        RuleFor(x => x.HabitId).NotEqual(Guid.Empty);
    }
}
