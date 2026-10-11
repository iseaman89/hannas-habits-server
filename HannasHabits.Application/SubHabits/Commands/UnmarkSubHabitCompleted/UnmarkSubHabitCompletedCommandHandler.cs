using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.UnmarkSubHabitCompleted;

public class UnmarkSubHabitCompletedCommandHandler : IRequestHandler<UnmarkSubHabitCompletedCommand, HabitDayDto>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UnmarkSubHabitCompletedCommandHandler(
        IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<HabitDayDto> Handle(UnmarkSubHabitCompletedCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdWithRecordsAsync(_currentUser.UserId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        var subHabit = habit.SubHabitById(request.SubHabitId)
                       ?? throw new NotFoundException(nameof(SubHabit), request.SubHabitId);

        // The Domain only takes back a tick that is there; for the API un-ticking is idempotent (as for the tags of a
        // day): a sub-habit that is not done is no error, the client just gets the day as it is.
        if (!subHabit.IsCompletedOn(request.Date))
            return HabitDayDto.Of(habit, request.Date);

        habit.UnmarkSubHabitCompleted(subHabit.Id, request.Date);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return HabitDayDto.Of(habit, request.Date);
    }
}
