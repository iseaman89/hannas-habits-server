using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Domain.Enums;

namespace HannasHabits.Application.Habits.Queries.GetHabitsStatistics;

/// <summary>One habit's statistic for the requested range.</summary>
/// <param name="Recurrence">The rule itself, so the screen can say it in words.</param>
/// <param name="Unit">
/// What <paramref name="Planned"/> and <paramref name="Done"/> count: <c>days</c> for a rule with due days, <c>weeks</c>
/// or <c>months</c> for a quota ("3 times a week").
/// </param>
/// <param name="Planned">
/// What was to be done in the range and is judged by now: the due days up to the requested day (that day itself only
/// once it is done), or the weeks / months that ended in the range (the running one only once it reached its number).
/// </param>
/// <param name="Done">How many of <paramref name="Planned"/> were met; never more.</param>
/// <param name="LastDoneOn">
/// The latest day the habit was done on or before the requested day - over the whole history, not just the range;
/// <c>null</c> if it never was.
/// </param>
/// <param name="SubHabits">The parts of the habit in the order they were added, each counted in days.</param>
public record HabitStatisticsDto(
    Guid Id, string Title, RecurrenceDto Recurrence, DateOnly StartDate, CountingUnit Unit,
    int Planned, int Done, DateOnly? LastDoneOn, IReadOnlyList<SubHabitStatisticsDto> SubHabits);

/// <summary>One part's statistic: its due days of the range (the habit is due and the weekday is the part's) in days.</summary>
/// <param name="Weekdays">The days of the week it is planned on (0 = Sunday ... 6 = Saturday).</param>
/// <param name="LastDoneOn">The latest day the part was ticked on or before the requested day, over the whole history.</param>
public record SubHabitStatisticsDto(
    Guid Id, string Title, IReadOnlyList<DayOfWeek> Weekdays, int Planned, int Done, DateOnly? LastDoneOn);
