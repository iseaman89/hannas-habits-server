using HannasHabits.Application.Habits.Commands.CreateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="Schedule">Planned days of the week (0 = Sunday ... 6 = Saturday); omitted = every day.</param>
public record CreateHabitRequest(string Title, string? Description, List<DayOfWeek>? Schedule)
{
    public CreateHabitCommand ToCommand() => new() { Title = Title, Description = Description, Schedule = Schedule };
}
