using HannasHabits.Application.Habits.Recurrence;
using HannasHabits.Application.SubHabits;

namespace HannasHabits.Application.Habits.Queries.GetHabitById;

/// <param name="SubHabits">The parts of the habit in the order they were added; empty for a habit without any.</param>
public record HabitDetailsDto(
    Guid Id, string Title, string? Description, RecurrenceDto Recurrence, DateOnly StartDate, DateTime CreatedAt,
    IReadOnlyList<SubHabitDto> SubHabits);
