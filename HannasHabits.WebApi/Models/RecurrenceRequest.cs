using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Domain.Enums;

namespace HannasHabits.WebApi.Models;

/// <summary>
/// The rule that decides on which days a habit is due. <c>kind</c> says which of the other fields are needed; a field
/// that does not belong to the kind is ignored.
/// </summary>
/// <param name="Kind">
/// <c>weekly</c> (needs <c>weekdays</c>; <c>interval</c> = every N weeks, omitted = every week) · <c>everyNDays</c>
/// (needs <c>interval</c> 2..365) · <c>monthlyOnWeekday</c> (needs <c>occurrence</c> and <c>weekday</c>) ·
/// <c>monthlyOnDate</c> (needs <c>dayOfMonth</c>) · <c>timesPerWeek</c> (needs <c>times</c> 1..7) ·
/// <c>timesPerMonth</c> (needs <c>times</c> 1..28). The last two have no due day: any day counts towards the N, and the
/// streak counts the weeks or months that reached it.
/// </param>
/// <param name="Weekdays">Weekly: the chosen days (0 = Sunday ... 6 = Saturday).</param>
/// <param name="Interval">Weekly: every N weeks, 1..52. Every N days: N, 2..365.</param>
/// <param name="Anchor">
/// The day the rhythm counts from (yyyy-MM-dd; every N weeks with N &gt; 1, every N days). Omitted = the habit's start
/// date as it is saved. Send the anchor the habit already has to keep its rhythm.
/// </param>
/// <param name="Occurrence">Monthly on a weekday: <c>first</c>, <c>second</c>, <c>third</c>, <c>fourth</c> or <c>last</c>.</param>
/// <param name="Weekday">Monthly on a weekday: the weekday (0 = Sunday ... 6 = Saturday).</param>
/// <param name="DayOfMonth">Monthly on a date: 1..31; a month without that date uses its last day.</param>
/// <param name="Times">Times a week (1..7, weeks run Monday to Sunday) or times a month (1..28).</param>
public record RecurrenceRequest(
    ScheduleKind? Kind,
    List<DayOfWeek>? Weekdays,
    int? Interval,
    DateOnly? Anchor,
    WeekdayOccurrence? Occurrence,
    DayOfWeek? Weekday,
    int? DayOfMonth,
    int? Times)
{
    public RecurrenceInput ToInput() => new()
    {
        Kind = Kind,
        Weekdays = Weekdays,
        Interval = Interval,
        Anchor = Anchor,
        Occurrence = Occurrence,
        Weekday = Weekday,
        DayOfMonth = DayOfMonth,
        Times = Times
    };
}
