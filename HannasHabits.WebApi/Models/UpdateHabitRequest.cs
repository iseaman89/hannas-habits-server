using HannasHabits.Application.Habits.Commands.UpdateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="Schedule">Planned days of the week (0 = Sunday ... 6 = Saturday); required, an update replaces the habit.</param>
public record UpdateHabitRequest(string Title, string? Description, List<DayOfWeek> Schedule)
{
    public UpdateHabitCommand ToCommand(Guid id) =>
        new() { Id = id, Title = Title, Description = Description, Schedule = Schedule };
}
