using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;

public class MarkSubHabitCompletedCommandValidator : AbstractValidator<MarkSubHabitCompletedCommand>
{
    public MarkSubHabitCompletedCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.SubHabitId).NotEmpty();

        // The same plausible range as a habit's start date: keeps absurd days such as 0001-01-01 out of the store.
        RuleFor(x => x.Date).InclusiveBetween(Habit.MinStartDate, Habit.MaxStartDate);
    }
}
