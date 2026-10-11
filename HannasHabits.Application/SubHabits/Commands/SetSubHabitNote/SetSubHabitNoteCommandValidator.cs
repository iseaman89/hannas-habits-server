using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits.Commands.SetSubHabitNote;

public class SetSubHabitNoteCommandValidator : AbstractValidator<SetSubHabitNoteCommand>
{
    public SetSubHabitNoteCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.SubHabitId).NotEmpty();

        // The same plausible range as a habit's start date: keeps absurd days such as 0001-01-01 out of the store.
        RuleFor(x => x.Date).InclusiveBetween(SubHabitNote.MinDate, SubHabitNote.MaxDate);

        // Blank is not "no note" here: taking a note away is the DELETE route, so a PUT that says nothing is a mistake.
        RuleFor(x => x.Text)
            .NotEmpty()
            .Must(text => text is null || text.Trim().Length <= SubHabitNote.TextMaxLength)
            .WithMessage($"A note must not be longer than {SubHabitNote.TextMaxLength} characters.");
    }
}
