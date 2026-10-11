using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.RemoveSubHabit;

public class RemoveSubHabitCommandHandler : IRequestHandler<RemoveSubHabitCommand, Unit>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public RemoveSubHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RemoveSubHabitCommand request, CancellationToken cancellationToken)
    {
        // The sub-habit's marks are not loaded: the database removes them with their sub-habit (cascade).
        var habit = await _habits.GetByIdWithSubHabitsAsync(_currentUser.UserId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        if (habit.SubHabitById(request.SubHabitId) is null)
            throw new NotFoundException(nameof(SubHabit), request.SubHabitId);

        habit.RemoveSubHabit(request.SubHabitId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
