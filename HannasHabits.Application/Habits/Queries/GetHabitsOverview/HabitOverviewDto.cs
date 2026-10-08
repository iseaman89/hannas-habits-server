namespace HannasHabits.Application.Habits.Queries.GetHabitsOverview;

/// <param name="CompletedDates">Completed days within the requested range, oldest first.</param>
/// <param name="CurrentStreak">Scheduled days in a row, as of the requested day - independent of the range, it can span months.</param>
public record HabitOverviewDto(
    Guid Id, string Title, IReadOnlyList<DayOfWeek> Schedule, DateOnly StartDate,
    IReadOnlyList<DateOnly> CompletedDates, int CurrentStreak);
