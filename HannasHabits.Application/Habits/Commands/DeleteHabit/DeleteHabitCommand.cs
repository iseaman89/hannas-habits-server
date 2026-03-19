using MediatR;

namespace HannasHabits.Application.Habits.Commands.DeleteHabit;

public record DeleteHabitCommand(Guid Id) : IRequest<Unit>;