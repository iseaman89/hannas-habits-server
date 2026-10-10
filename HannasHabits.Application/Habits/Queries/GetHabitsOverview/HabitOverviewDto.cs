using HannasHabits.Application.Habits.Recurrence;

namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

/// <param name="Recurrence">The rule itself, for showing and editing it.</param>
/// <param name="ScheduledDates">
/// The days within the requested range on which the habit is due - the rule applied, and none before the start date -
/// oldest first. A day of the range that is not in the list is "not scheduled" (or, before the start date, "not applicable").
/// </param>
/// <param name="CompletedDates">Completed days within the requested range, oldest first.</param>
/// <param name="CurrentStreak">Scheduled days in a row, as of the requested day - independent of the range, it can span months.</param>
public record HabitOverviewDto(
    Guid Id, string Title, RecurrenceDto Recurrence, DateOnly StartDate,
    IReadOnlyList<DateOnly> ScheduledDates, IReadOnlyList<DateOnly> CompletedDates, int CurrentStreak);
