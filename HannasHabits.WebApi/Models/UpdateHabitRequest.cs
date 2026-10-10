using HannasHabits.Application.Habits.Commands.UpdateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="Schedule">Deprecated, use <c>recurrence</c>: planned days of the week (0 = Sunday ... 6 = Saturday), every week; ignored when <c>recurrence</c> is given.</param>
/// <param name="StartDate">First day the habit applies (yyyy-MM-dd, the caller's local date); omitted = unchanged.</param>
/// <param name="Recurrence">The rule that decides on which days the habit is due. An update replaces the habit, so this or <c>schedule</c> is required.</param>
public record UpdateHabitRequest(
    string Title, string? Description, List<DayOfWeek>? Schedule, DateOnly? StartDate, RecurrenceRequest? Recurrence = null)
{
    public UpdateHabitCommand ToCommand(Guid id) => new()
    {
        Id = id,
        Title = Title,
        Description = Description,
        Schedule = Schedule,
        StartDate = StartDate,
        Recurrence = Recurrence?.ToInput()
    };
}
