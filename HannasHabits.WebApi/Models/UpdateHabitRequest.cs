using HannasHabits.Application.Habits.Commands.UpdateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="Schedule">Planned days of the week (0 = Sunday ... 6 = Saturday); required, an update replaces the habit.</param>
/// <param name="StartDate">First day the habit applies (yyyy-MM-dd, the caller's local date); omitted = unchanged.</param>
public record UpdateHabitRequest(string Title, string? Description, List<DayOfWeek> Schedule, DateOnly? StartDate)
{
    public UpdateHabitCommand ToCommand(Guid id) =>
        new() { Id = id, Title = Title, Description = Description, Schedule = Schedule, StartDate = StartDate };
}
