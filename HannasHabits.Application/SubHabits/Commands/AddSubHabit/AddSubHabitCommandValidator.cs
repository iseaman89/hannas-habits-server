using FluentValidation;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.SubHabits.Commands.AddSubHabit;

public class AddSubHabitCommandValidator : AbstractValidator<AddSubHabitCommand>
{
    public AddSubHabitCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(HabitTitle.MaxLength);

        RuleFor(x => x.Weekdays).NotEmpty();
        RuleForEach(x => x.Weekdays).IsInEnum();
    }
}
