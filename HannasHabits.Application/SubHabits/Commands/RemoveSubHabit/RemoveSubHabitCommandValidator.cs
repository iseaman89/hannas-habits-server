using FluentValidation;

namespace HannasHabits.Application.SubHabits.Commands.RemoveSubHabit;

public class RemoveSubHabitCommandValidator : AbstractValidator<RemoveSubHabitCommand>
{
    public RemoveSubHabitCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.SubHabitId).NotEmpty();
    }
}
