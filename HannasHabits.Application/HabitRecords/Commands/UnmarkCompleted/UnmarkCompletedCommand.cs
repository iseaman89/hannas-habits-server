using MediatR;

namespace HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;

public record UnmarkCompletedCommand(Guid HabitId, DateOnly Date) : IRequest<Unit>;