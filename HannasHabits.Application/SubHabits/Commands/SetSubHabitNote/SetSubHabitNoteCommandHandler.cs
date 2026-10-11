using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.Habits;
using HannasHabits.Domain.Entities;
using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.SetSubHabitNote;

public class SetSubHabitNoteCommandHandler : IRequestHandler<SetSubHabitNoteCommand, SubHabitNoteDto>
{
    private readonly IHabitRepository _habits;
    private readonly ISubHabitNoteRepository _notes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public SetSubHabitNoteCommandHandler(
        IHabitRepository habits, ISubHabitNoteRepository notes, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _habits = habits;
        _notes = notes;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<SubHabitNoteDto> Handle(SetSubHabitNoteCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        // Only the part is needed - the note does not depend on the habit's days or ticks - but that the part is the
        // user's is checked through the habit, where the ownership lives. No day rule: like a tick, a note may be
        // written on any day (the UI offers the part's own days).
        var habit = await _habits.GetByIdWithSubHabitsAsync(userId, request.HabitId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Habit), request.HabitId);

        if (habit.SubHabitById(request.SubHabitId) is null)
            throw new NotFoundException(nameof(SubHabit), request.SubHabitId);

        var note = await _notes.GetAsync(userId, request.SubHabitId, request.Date, cancellationToken);
        var isNew = note is null;

        if (note is null)
        {
            note = SubHabitNote.Create(request.SubHabitId, request.Date, request.Text);
            _notes.Add(note);
        }
        else
        {
            note.Rewrite(request.Text);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEntryException) when (isNew)
        {
            // A concurrent request wrote the day's first note between our lookup and our save and the unique index
            // rejected ours. "This is the note" is still what the person asked for: forget ours, put the text on the
            // winner's note and save that. (Two writers of one note: the last save wins - a note has no history.)
            _notes.Remove(note);

            var winner = await _notes.GetAsync(userId, request.SubHabitId, request.Date, cancellationToken);
            if (winner is null)
                throw; // the winner was taken away again in the meantime: let the client retry (409)

            winner.Rewrite(request.Text);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            note = winner;
        }

        return SubHabitNoteDto.From(note);
    }
}
