using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Application.Tags;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

/// <param name="Recurrence">The rule itself, for showing and editing it.</param>
/// <param name="ScheduledDates">
/// The days within the requested range on which the habit is due - the rule applied, and none before the start date -
/// oldest first. A day of the range that is not in the list is "not scheduled" (or, before the start date, "not applicable").
/// A quota rule (<c>timesPerWeek</c>, <c>timesPerMonth</c>) has no due day: every day from the start date is listed, as
/// "can be ticked"; whether it is *needed* is for <paramref name="QuotaPeriods"/> to say.
/// </param>
/// <param name="CompletedDates">Completed days within the requested range, oldest first.</param>
/// <param name="TaggedDays">
/// The days within the requested range that carry tags (of any kind of day: done or not), oldest first, each with its
/// tags by name. A day of the range that is not in the list has no tags.
/// </param>
/// <param name="QuotaPeriods">
/// For a quota rule: every week or month that overlaps the requested range (and has not ended before the start date),
/// oldest first, with the days completed in it - counted over the whole period, also where it reaches outside the
/// range, so the week that spans two months shows its true number. Empty for every other kind of rule.
/// </param>
/// <param name="CurrentStreak">
/// Scheduled days in a row, as of the requested day - independent of the range, it can span months. For a quota rule
/// it is the number of weeks (<c>timesPerWeek</c>) or months (<c>timesPerMonth</c>) in a row that reached their count.
/// </param>
/// <param name="SubHabits">
/// The parts of the habit in the order they were added, each with its due and its done days of the range. A habit's
/// day is done when all of that day's due sub-habits are; the streak counts the habit's own days only.
/// </param>
public record HabitOverviewDto(
    Guid Id, string Title, RecurrenceDto Recurrence, DateOnly StartDate,
    IReadOnlyList<DateOnly> ScheduledDates, IReadOnlyList<DateOnly> CompletedDates,
    IReadOnlyList<TaggedDayDto> TaggedDays, IReadOnlyList<QuotaPeriodDto> QuotaPeriods, int CurrentStreak,
    IReadOnlyList<SubHabitOverviewDto> SubHabits);

/// <summary>A part of a habit within the requested range.</summary>
/// <param name="Weekdays">The days of the week it is planned on (0 = Sunday ... 6 = Saturday), never empty.</param>
/// <param name="ScheduledDates">
/// The days of the range on which it is due: the habit is due (from its start date on) and the weekday is one of
/// <paramref name="Weekdays"/>, oldest first.
/// </param>
/// <param name="CompletedDates">The days of the range it was done on - also one it was not due on - oldest first.</param>
public record SubHabitOverviewDto(
    Guid Id, string Title, IReadOnlyList<DayOfWeek> Weekdays,
    IReadOnlyList<DateOnly> ScheduledDates, IReadOnlyList<DateOnly> CompletedDates);

/// <summary>One calendar week (Monday to Sunday) or month of a quota habit and how often it was done in it.</summary>
/// <param name="From">First day of the period (yyyy-MM-dd), also if that is before the range or the start date.</param>
/// <param name="To">Last day of the period (yyyy-MM-dd).</param>
/// <param name="Done">
/// Completed days of the period that count: from the start date on, and not after the requested <c>asOf</c> day.
/// The aim is the rule's <c>times</c>.
/// </param>
public record QuotaPeriodDto(DateOnly From, DateOnly To, int Done);
