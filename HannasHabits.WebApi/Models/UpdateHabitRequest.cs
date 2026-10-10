using HannasHabits.Application.Habits.Commands.UpdateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="StartDate">First day the habit applies (yyyy-MM-dd, the caller's local date); omitted = unchanged.</param>
/// <param name="Recurrence">The rule that decides on which days the habit is due. An update replaces the habit, so it is required.</param>
public record UpdateHabitRequest(
    string Title, string? Description, DateOnly? StartDate, RecurrenceRequest? Recurrence = null)
{
    public UpdateHabitCommand ToCommand(Guid id) => new()
    {
        Id = id,
        Title = Title,
        Description = Description,
        StartDate = StartDate,
        Recurrence = Recurrence?.ToInput()
    };
}
