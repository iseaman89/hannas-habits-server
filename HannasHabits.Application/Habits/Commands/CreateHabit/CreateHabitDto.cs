namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public record CreateHabitDto(Guid Id, string Title, IReadOnlyList<DayOfWeek> Schedule, DateOnly StartDate);
