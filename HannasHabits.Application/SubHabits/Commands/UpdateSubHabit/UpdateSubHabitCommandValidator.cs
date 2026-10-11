using FluentValidation;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.SubHabits.Commands.UpdateSubHabit;

public class UpdateSubHabitCommandValidator : AbstractValidator<UpdateSubHabitCommand>
{
    public UpdateSubHabitCommandValidator()
    {
        RuleFor(x => x.HabitId).NotEmpty();
        RuleFor(x => x.SubHabitId).NotEmpty();
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(HabitTitle.MaxLength);

        RuleFor(x => x.Weekdays).NotEmpty();
        RuleForEach(x => x.Weekdays).IsInEnum();
    }
}
