using FluentValidation;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits.Commands.UnmarkSubHabitCompleted;

public class UnmarkSubHabitCompletedCommandValidator : AbstractValidator<UnmarkSubHabitCompletedCommand>
{
    public UnmarkSubHabitCompletedCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.SubHabitId).NotEmpty();
        RuleFor(x => x.Date).InclusiveBetween(Habit.MinStartDate, Habit.MaxStartDate);
    }
}
