using MediatR;

namespace HannasHabits.Application.SubHabits.Commands.SetSubHabitNote;

/// <summary>Writes the note of a part on a day: creates it, or replaces what was there. Answers with the note.</summary>
public record SetSubHabitNoteCommand(Guid HabitId, Guid SubHabitId, DateOnly Date, string? Text)
    : IRequest<SubHabitNoteDto>;
