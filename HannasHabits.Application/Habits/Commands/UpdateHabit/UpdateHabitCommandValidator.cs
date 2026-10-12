using FluentValidation;
using HannasHabits.Application.Habits.Recurrence;
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

        RuleFor(x => x.StartDate)
            .InclusiveBetween(Habit.MinStartDate, Habit.MaxStartDate)
            .When(x => x.StartDate.HasValue);

        // An update replaces the whole habit: the rule has to be there.
        RuleFor(x => x.Recurrence).NotNull();

        // A missing recurrence is skipped by the child validator (the null-forgiving operator only quiets the nullability).
        RuleFor(x => x.Recurrence!).SetValidator(new RecurrenceInputValidator());
    }
}
