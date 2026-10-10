using HannasHabits.Application.Habits.Commands.CreateHabit;

namespace HannasHabits.WebApi.Models;

/// <param name="Schedule">Deprecated, use <c>recurrence</c>: planned days of the week (0 = Sunday ... 6 = Saturday), every week; ignored when <c>recurrence</c> is given.</param>
/// <param name="StartDate">First day the habit applies (yyyy-MM-dd, the caller's local date); omitted = today.</param>
/// <param name="Recurrence">The rule that decides on which days the habit is due; omitted (with no <c>schedule</c> either) = every day.</param>
public record CreateHabitRequest(
    string Title, string? Description, List<DayOfWeek>? Schedule, DateOnly? StartDate, RecurrenceRequest? Recurrence = null)
{
    public CreateHabitCommand ToCommand() => new()
    {
        Title = Title,
        Description = Description,
        Schedule = Schedule,
        StartDate = StartDate,
        Recurrence = Recurrence?.ToInput()
    };
}
