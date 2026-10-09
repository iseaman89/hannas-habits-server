using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MediatR;

namespace HannasHabits.Application.Habits.Commands.UpdateHabit;

public class UpdateHabitCommandHandler : IRequestHandler<UpdateHabitCommand, Unit>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UpdateHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateHabitCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdAsync(_currentUser.UserId, request.Id, cancellationToken);

        if (habit is null)
            throw new NotFoundException(nameof(Habit), request.Id);

        habit.Update(HabitTitle.Create(request.Title), request.Description, HabitSchedule.Create(request.Schedule));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
