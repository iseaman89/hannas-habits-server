using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.ClearSubHabitNote;

/// <summary>Takes the note of a part on a day away. Idempotent: a day without a note is no error.</summary>
public record ClearSubHabitNoteCommand(Guid HabitId, Guid SubHabitId, DateOnly Date) : IRequest<Unit>;
