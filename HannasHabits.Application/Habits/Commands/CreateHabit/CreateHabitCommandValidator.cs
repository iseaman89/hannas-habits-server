using FluentValidation;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

// First line of defence with per-field messages; the Domain enforces the same rules again.
public class CreateHabitCommandValidator : AbstractValidator<CreateHabitCommand>
{
    public CreateHabitCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(HabitTitle.MaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Habit.DescriptionMaxLength);

        RuleFor(x => x.StartDate)
            .InclusiveBetween(Habit.MinStartDate, Habit.MaxStartDate)
            .When(x => x.StartDate.HasValue);

        // No schedule at all means "every day", but an explicitly empty one is a mistake.
        When(x => x.Schedule is not null, () =>
        {
            RuleFor(x => x.Schedule).NotEmpty();
            RuleForEach(x => x.Schedule).IsInEnum();
        });
    }
}
