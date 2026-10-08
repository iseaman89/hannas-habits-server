namespace HannasHabits.Application.Habits.Queries.GetHabitById;

public record HabitDetailsDto(Guid Id, string Title, string? Description, DateTime CreatedAt);