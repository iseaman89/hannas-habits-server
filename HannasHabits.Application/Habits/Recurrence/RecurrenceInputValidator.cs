using FluentValidation;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Recurrence;

// First line of defence with per-field messages (`recurrence.interval`, `recurrence.weekdays[1]`, ...); the Domain's
// factories enforce the same rules again.
public class RecurrenceInputValidator : AbstractValidator<RecurrenceInput>
{
    public RecurrenceInputValidator()
    {
        RuleFor(x => x.Kind).NotNull().IsInEnum();

        RuleFor(x => x.Anchor)
            .InclusiveBetween(HabitSchedule.MinAnchor, HabitSchedule.MaxAnchor)
            .When(x => x.Anchor.HasValue);

        When(x => x.Kind == ScheduleKind.Weekly, () =>
        {
            RuleFor(x => x.Weekdays).NotEmpty();
            RuleForEach(x => x.Weekdays).IsInEnum();

            RuleFor(x => x.Interval)
                .InclusiveBetween(1, HabitSchedule.MaxWeekInterval)
                .When(x => x.Interval.HasValue);
        });

        When(x => x.Kind == ScheduleKind.EveryNDays, () =>
        {
            RuleFor(x => x.Interval)
                .NotNull()
                .InclusiveBetween(HabitSchedule.MinDayInterval, HabitSchedule.MaxDayInterval);
        });

        When(x => x.Kind == ScheduleKind.MonthlyOnWeekday, () =>
        {
            RuleFor(x => x.Occurrence).NotNull().IsInEnum();
            RuleFor(x => x.Weekday).NotNull().IsInEnum();
        });

        When(x => x.Kind == ScheduleKind.MonthlyOnDate, () =>
        {
            RuleFor(x => x.DayOfMonth)
                .NotNull()
                .InclusiveBetween(1, HabitSchedule.MaxDayOfMonth);
        });
    }
}
