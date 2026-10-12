using HannasHabits.Application.Habits.Recurrence;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>
    /// The rule that decides on which days the habit is due. An update replaces the whole habit, so it is required
    /// (the validator says so; the handler does not invent one).
    /// </summary>
    public RecurrenceInput? Recurrence { get; set; }

    /// <summary>The first day the habit applies (the caller's local date); <c>null</c> keeps the current one.</summary>
    public DateOnly? StartDate { get; set; }
}
