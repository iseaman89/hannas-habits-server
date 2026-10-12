using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.ValueObjects;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.AddSubHabit;

public class AddSubHabitCommandHandler : IRequestHandler<AddSubHabitCommand, SubHabitDto>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AddSubHabitCommandHandler(IHabitRepository habits, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<SubHabitDto> Handle(AddSubHabitCommand request, CancellationToken cancellationToken)
    {
        var habit = await _habits.GetByIdWithSubHabitsAsync(_currentUser.UserId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        // The validator has asked for the weekdays; an empty set is a Domain error for a caller that skipped it.
        var subHabit = habit.AddSubHabit(HabitTitle.Create(request.Title), WeekdaySet.Of(request.Weekdays ?? []));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SubHabitDto.From(subHabit);
    }
}
