using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;

public class UnmarkCompletedCommandHandler : IRequestHandler<UnmarkCompletedCommand, Unit>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UnmarkCompletedCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UnmarkCompletedCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdWithRecordsAsync(_currentUser.UserId, request.HabitId, cancellationToken);

        if (habit is null)
            throw new NotFoundException(nameof(Habit), request.HabitId);

        if (!habit.UnmarkCompleted(request.Date))
            throw new NotFoundException(nameof(HabitRecord), request.Date.ToString("O"));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
