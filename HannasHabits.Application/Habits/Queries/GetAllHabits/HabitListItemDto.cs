using HannasHabits.Application.Habits.Recurrence;

namespace HannasHabits.Application.Habits.Queries.GetAllHabits;

public record HabitListItemDto(Guid Id, string Title, string? Description, RecurrenceDto Recurrence);
