using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.UpdateSubHabit;

public class UpdateSubHabitCommandHandler : IRequestHandler<UpdateSubHabitCommand, SubHabitDto>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UpdateSubHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<SubHabitDto> Handle(UpdateSubHabitCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdWithSubHabitsAsync(_currentUser.UserId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        // For the API an unknown sub-habit is a missing resource (404); the Domain has no such notion.
        var subHabit = habit.SubHabitById(request.SubHabitId)
                       ?? throw new NotFoundException(nameof(SubHabit), request.SubHabitId);

        habit.UpdateSubHabit(subHabit.Id, HabitTitle.Create(request.Title), WeekdaySet.Of(request.Weekdays ?? []));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SubHabitDto.From(subHabit);
    }
}
