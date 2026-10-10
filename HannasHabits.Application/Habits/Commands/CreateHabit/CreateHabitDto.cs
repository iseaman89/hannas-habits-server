using HannasHabits.Application.Habits.Recurrence;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

/// <param name="Schedule">Deprecated, use <paramref name="Recurrence"/>: the weekdays on which the habit can be due.</param>
public record CreateHabitDto(
    Guid Id, string Title, IReadOnlyList<DayOfWeek> Schedule, RecurrenceDto Recurrence, DateOnly StartDate);
