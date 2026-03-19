using MediatR;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
}