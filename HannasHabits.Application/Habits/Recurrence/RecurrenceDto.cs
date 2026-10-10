using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Habits.Recurrence;

/// <summary>
/// The rule that decides on which days a habit is due, as the API shows it. Flat on purpose: <c>kind</c> says which of
/// the other fields apply, the rest is empty (<c>weekdays</c> = <c>[]</c>, the others <c>null</c>, <c>interval</c> = 1).
/// </summary>
/// <param name="Kind">Which rule this is.</param>
/// <param name="Weekdays">
/// <see cref="ScheduleKind.Weekly"/>: the chosen days (0 = Sunday ... 6 = Saturday), never empty. Empty for the other kinds.
/// </param>
/// <param name="Interval">
/// <see cref="ScheduleKind.Weekly"/>: every N weeks (1 = every week). <see cref="ScheduleKind.EveryNDays"/>: every N days.
/// 1 otherwise.
/// </param>
/// <param name="Anchor">
/// The day the rhythm counts from (yyyy-MM-dd): for "every N weeks" (N &gt; 1) the week that contains it - weeks start on
/// Monday - and for "every N days" the first due day. Not the start date: moving the start date does not shift the rhythm.
/// <c>null</c> otherwise.
/// </param>
/// <param name="Occurrence"><see cref="ScheduleKind.MonthlyOnWeekday"/>: which one of the month; <c>null</c> otherwise.</param>
/// <param name="Weekday"><see cref="ScheduleKind.MonthlyOnWeekday"/>: the weekday (0 = Sunday); <c>null</c> otherwise.</param>
/// <param name="DayOfMonth">
/// <see cref="ScheduleKind.MonthlyOnDate"/>: 1..31; a month without that date uses its last day. <c>null</c> otherwise.
/// </param>
public record RecurrenceDto(
    ScheduleKind Kind,
    IReadOnlyList<DayOfWeek> Weekdays,
    int Interval,
    DateOnly? Anchor,
    WeekdayOccurrence? Occurrence,
    DayOfWeek? Weekday,
    int? DayOfMonth)
{
    public static RecurrenceDto From(HabitSchedule schedule) => schedule.Kind switch
    {
        ScheduleKind.Weekly => new RecurrenceDto(
            schedule.Kind, schedule.Days, schedule.Interval, schedule.Anchor, null, null, null),

        ScheduleKind.EveryNDays => new RecurrenceDto(
            schedule.Kind, [], schedule.Interval, schedule.Anchor, null, null, null),

        ScheduleKind.MonthlyOnWeekday => new RecurrenceDto(
            schedule.Kind, [], schedule.Interval, null, schedule.Occurrence, schedule.Days.Single(), null),

        ScheduleKind.MonthlyOnDate => new RecurrenceDto(
            schedule.Kind, [], schedule.Interval, null, null, null, schedule.DayOfMonth),

        _ => throw new InvalidOperationException($"Unknown schedule kind '{schedule.Kind}'.")
    };
}
