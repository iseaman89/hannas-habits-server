using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;
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

        // A rule that counts from a day, sent without one, counts from the start date the habit has after this update.
        var startDate = request.StartDate ?? habit.StartDate;

        // The validator has already asked for one of the two; the Domain error is for a caller that skipped it.
        var schedule = request.Recurrence?.ToSchedule(startDate)
                       ?? (request.Schedule is null
                           ? throw new DomainException("A habit needs a recurrence rule.")
                           : HabitSchedule.Weekly(request.Schedule));

        habit.Update(HabitTitle.Create(request.Title), request.Description, schedule, request.StartDate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
