using HannasHabits.Application.Habits.Recurrence;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>
    /// The rule that decides on which days the habit is due. An update replaces the whole habit, so this or the
    /// deprecated <see cref="Schedule"/> is required; this one wins when both are given.
    /// </summary>
    public RecurrenceInput? Recurrence { get; set; }

    /// <summary>Deprecated, use <see cref="Recurrence"/>: the planned days of the week, every week.</summary>
    public List<DayOfWeek>? Schedule { get; set; }

    /// <summary>The first day the habit applies (the caller's local date); <c>null</c> keeps the current one.</summary>
    public DateOnly? StartDate { get; set; }
}
