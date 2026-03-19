using MediatR;

namespace HannasHabits.Application.Habits.Commands.CreateHabit;

public class CreateHabitCommand : IRequest<CreateHabitDto>
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
}