using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.DeleteHabit;

public class DeleteHabitCommandHandler : IRequestHandler<DeleteHabitCommand, Unit>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DeleteHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteHabitCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdAsync(_currentUser.UserId, request.Id, cancellationToken);

        if (habit is null)
            throw new NotFoundException(nameof(Habit), request.Id);

        _habits.Remove(habit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
