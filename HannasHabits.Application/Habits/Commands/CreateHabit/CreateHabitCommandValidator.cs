using FluentValidation;
using HannasHabits.Application.Habits.Recurrence;
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

        // A missing recurrence is skipped by the child validator (the null-forgiving operator only quiets the nullability).
        RuleFor(x => x.Recurrence!).SetValidator(new RecurrenceInputValidator());

        // No rule at all means "every day", but an explicitly empty list of days is a mistake. A recurrence wins over
        // the deprecated list, which is then not looked at.
        When(x => x.Recurrence is null && x.Schedule is not null, () =>
        {
            RuleFor(x => x.Schedule).NotEmpty();
            RuleForEach(x => x.Schedule).IsInEnum();
        });
    }
}
