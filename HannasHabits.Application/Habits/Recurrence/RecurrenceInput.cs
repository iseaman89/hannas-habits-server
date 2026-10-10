using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Recurrence;

/// <summary>
/// The rule as a command carries it - the same flat shape as <see cref="RecurrenceDto"/>, but every fact optional:
/// the validator says which ones its <see cref="Kind"/> needs, and a fact that does not belong to the kind is ignored.
/// </summary>
public class RecurrenceInput
{
    /// <summary>Nullable so that a missing kind is a validation error and not silently "weekly".</summary>
    public ScheduleKind? Kind { get; set; }

    /// <summary>Weekly: the chosen days of the week.</summary>
    public List<DayOfWeek>? Weekdays { get; set; }

    /// <summary>Weekly: every N weeks (omitted = every week). Every N days: N.</summary>
    public int? Interval { get; set; }

    /// <summary>
    /// The day the rhythm counts from (every N weeks, every N days). Omitted = the habit's start date as it is saved.
    /// Send the one the habit already has to keep its rhythm - editing a habit must not shift it.
    /// </summary>
    public DateOnly? Anchor { get; set; }

    /// <summary>Monthly on a weekday: which one.</summary>
    public WeekdayOccurrence? Occurrence { get; set; }

    /// <summary>Monthly on a weekday: the weekday.</summary>
    public DayOfWeek? Weekday { get; set; }

    /// <summary>Monthly on a date: 1..31.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>
    /// The schedule this input describes. The validator has already checked that the needed facts are there; the Domain
    /// factories enforce the rules once more.
    /// </summary>
    /// <param name="defaultAnchor">Used when the rule counts from a day and <see cref="Anchor"/> was left out.</param>
    public HabitSchedule ToSchedule(DateOnly defaultAnchor) => Kind switch
    {
        ScheduleKind.Weekly => HabitSchedule.Weekly(Weekdays!, Interval ?? 1, Anchor ?? defaultAnchor),
        ScheduleKind.EveryNDays => HabitSchedule.EveryNDays(Interval!.Value, Anchor ?? defaultAnchor),
        ScheduleKind.MonthlyOnWeekday => HabitSchedule.MonthlyOnWeekday(Occurrence!.Value, Weekday!.Value),
        ScheduleKind.MonthlyOnDate => HabitSchedule.MonthlyOnDate(DayOfMonth!.Value),
        _ => throw new InvalidOperationException($"Unknown schedule kind '{Kind}'.")
    };
}
