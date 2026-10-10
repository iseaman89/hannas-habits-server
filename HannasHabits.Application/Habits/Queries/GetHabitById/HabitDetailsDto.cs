using HannasHabits.Application.Habits.Recurrence;

namespace HannasHabits.Application.Habits.Queries.GetHabitById;

public record HabitDetailsDto(
    Guid Id, string Title, string? Description, RecurrenceDto Recurrence, DateOnly StartDate, DateTime CreatedAt);
