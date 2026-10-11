using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits.Commands.ClearSubHabitNote;

public class ClearSubHabitNoteCommandValidator : AbstractValidator<ClearSubHabitNoteCommand>
{
    public ClearSubHabitNoteCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.SubHabitId).NotEmpty();
        RuleFor(x => x.Date).InclusiveBetween(SubHabitNote.MinDate, SubHabitNote.MaxDate);
    }
}
