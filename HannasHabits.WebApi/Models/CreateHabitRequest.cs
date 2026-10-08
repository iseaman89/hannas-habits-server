using HannasHabits.Application.Habits.Commands.CreateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="Schedule">Planned days of the week (0 = Sunday ... 6 = Saturday); omitted = every day.</param>
/// <param name="StartDate">First day the habit applies (yyyy-MM-dd, the caller's local date); omitted = today.</param>
public record CreateHabitRequest(string Title, string? Description, List<DayOfWeek>? Schedule, DateOnly? StartDate)
{
    public CreateHabitCommand ToCommand() => new()
    {
        Title = Title, Description = Description, Schedule = Schedule, StartDate = StartDate
    };
}
