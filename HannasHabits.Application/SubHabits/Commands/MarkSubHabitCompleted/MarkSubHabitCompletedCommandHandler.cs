using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;

public class MarkSubHabitCompletedCommandHandler : IRequestHandler<MarkSubHabitCompletedCommand, HabitDayDto>
{
    private readonly IHabitRepository _habits;
    private readonly IHabitQueries _habitQueries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public MarkSubHabitCompletedCommandHandler(
        IHabitRepository habits, IHabitQueries habitQueries, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _habitQueries = habitQueries;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<HabitDayDto> Handle(MarkSubHabitCompletedCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var habit = await _habits.GetByIdWithRecordsAsync(userId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        var subHabit = habit.SubHabitById(request.SubHabitId)
                       ?? throw new NotFoundException(nameof(SubHabit), request.SubHabitId);

        // The Domain forbids ticking twice; making the API call idempotent is this use case's decision: the client
        // just gets the day as it is.
        if (subHabit.IsCompletedOn(request.Date))
            return HabitDayDto.Of(habit, request.Date);

        habit.MarkSubHabitCompleted(subHabit.Id, request.Date);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEntryException)
        {
            // A concurrent request ticked the same sub-habit on the same day between our load and our save and the
            // unique index rejected ours. The goal "it is done" is reached either way: answer with the day as it is now.
            var day = await _habitQueries.GetDayAsync(userId, request.HabitId, request.Date, cancellationToken);

            if (day is not null && day.CompletedSubHabitIds.Contains(request.SubHabitId))
                return day;

            throw; // the winner was taken back again, or the habit's own day collided: let the client retry (409)
        }

        return HabitDayDto.Of(habit, request.Date);
    }
}
