using HannasHabits.Application.Habits.Recurrence;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommand : IRequest<CreateHabitDto>
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>The first day the habit applies, in the caller's time zone; <c>null</c> = today (server date).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// The rule that decides on which days the habit is due; <c>null</c> = see <see cref="Schedule"/>. Wins over
    /// <see cref="Schedule"/> when both are given.
    /// </summary>
    public RecurrenceInput? Recurrence { get; set; }

    /// <summary>
    /// Deprecated, use <see cref="Recurrence"/>: the planned days of the week, every week. With neither this nor a
    /// recurrence the habit is due every day.
    /// </summary>
    public List<DayOfWeek>? Schedule { get; set; }
}
