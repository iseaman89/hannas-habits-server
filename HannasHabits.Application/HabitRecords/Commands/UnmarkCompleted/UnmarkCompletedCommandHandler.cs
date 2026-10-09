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

        // The Domain only allows taking back a completed day; for the API an unmarked day is a missing resource (404).
        if (habit.RecordOn(request.Date) is null)
            throw new NotFoundException(nameof(HabitRecord), request.Date.ToString("O"));

        habit.UnmarkCompleted(request.Date);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
