using HannasHabits.Application.Habits.Recurrence;

namespace HannasHabits.Application.Habits.Queries.GetHabitById;

/// <param name="Schedule">Deprecated, use <paramref name="Recurrence"/>: the weekdays on which the habit can be due.</param>
public record HabitDetailsDto(
    Guid Id, string Title, string? Description, IReadOnlyList<DayOfWeek> Schedule, RecurrenceDto Recurrence,
    DateOnly StartDate, DateTime CreatedAt);
