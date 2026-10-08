using FluentValidation;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommandValidator : AbstractValidator<UpdateHabitCommand>
{
    public UpdateHabitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(HabitTitle.MaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Habit.DescriptionMaxLength);

        RuleFor(x => x.Schedule).NotEmpty();
        RuleForEach(x => x.Schedule).IsInEnum();
    }
}
