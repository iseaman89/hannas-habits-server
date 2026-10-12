using HannasHabits.Application.Habits.Commands.CreateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="StartDate">First day the habit applies (yyyy-MM-dd, the caller's local date); omitted = today.</param>
/// <param name="Recurrence">The rule that decides on which days the habit is due; omitted = every day.</param>
public record CreateHabitRequest(
    string Title, string? Description, DateOnly? StartDate, RecurrenceRequest? Recurrence = null)
{
    public CreateHabitCommand ToCommand() => new()
    {
        Title = Title,
        Description = Description,
        StartDate = StartDate,
        Recurrence = Recurrence?.ToInput()
    };
}
