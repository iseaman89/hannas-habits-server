using MediatR;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>The planned days of the week. Required: an update replaces the whole habit.</summary>
    public List<DayOfWeek> Schedule { get; set; } = default!;

    /// <summary>The first day the habit applies (the caller's local date); <c>null</c> keeps the current one.</summary>
    public DateOnly? StartDate { get; set; }
}
