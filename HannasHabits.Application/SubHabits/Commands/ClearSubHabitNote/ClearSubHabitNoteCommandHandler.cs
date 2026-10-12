using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.ClearSubHabitNote;

public class ClearSubHabitNoteCommandHandler : IRequestHandler<ClearSubHabitNoteCommand, Unit>
{
    private readonly IHabitRepository _habits;
    private readonly ISubHabitNoteRepository _notes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ClearSubHabitNoteCommandHandler(
        IHabitRepository habits, ISubHabitNoteRepository notes, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _notes = notes;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ClearSubHabitNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var habit = await _habits.GetByIdWithSubHabitsAsync(userId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        if (habit.SubHabitById(request.SubHabitId) is null)
            throw new NotFoundException(nameof(SubHabit), request.SubHabitId);

        // Idempotent: no note is what was asked for.
        var note = await _notes.GetAsync(userId, request.SubHabitId, request.Date, cancellationToken);
        if (note is null)
            return Unit.Value;

        _notes.Remove(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
