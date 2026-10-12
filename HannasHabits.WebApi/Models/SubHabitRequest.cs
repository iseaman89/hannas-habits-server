using HannasHabits.Application.SubHabits.Commands.AddSubHabit;
using HannasHabits.Application.SubHabits.Commands.UpdateSubHabit;

namespace HannasHabits.WebApi.Models;

/// <summary>A part of a habit and the weekdays it is planned on.</summary>
/// <param name="Title">Not empty, at most 150 characters after trimming; unique among the habit's sub-habits, ignoring case.</param>
/// <param name="Weekdays">
/// The weekdays it is planned on (0 = Sunday ... 6 = Saturday), at least one. They must be days the habit itself is
/// planned on (a weekly rule: its chosen weekdays; "the last Saturday": that weekday; the other rules: any weekday).
/// A sub-habit is due where the habit is due and the weekday is one of these.
/// </param>
public record SubHabitRequest(string Title, List<DayOfWeek>? Weekdays)
{
    public AddSubHabitCommand ToAddCommand(Guid habitId) => new(habitId, Title, Weekdays);

    public UpdateSubHabitCommand ToUpdateCommand(Guid habitId, Guid subHabitId)
        => new(habitId, subHabitId, Title, Weekdays);
}
