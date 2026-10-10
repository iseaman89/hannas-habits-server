using HannasHabits.Application.Habits.Recurrence;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public record CreateHabitDto(Guid Id, string Title, RecurrenceDto Recurrence, DateOnly StartDate);
