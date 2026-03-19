using FluentValidation;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommandValidator : AbstractValidator<CreateHabitCommand>
{
    public CreateHabitCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(150);
    }
}