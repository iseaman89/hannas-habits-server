using HannasHabits.Application.SubHabits.Commands.SetSubHabitNote;

namespace HannasHabits.WebApi.Models;

/// <summary>The note of a part on a day.</summary>
/// <param name="Text">
/// Free text, not empty after trimming, at most 500 characters (line breaks inside are kept). To take a note away, call
/// the DELETE route instead of sending an empty text.
/// </param>
public record SubHabitNoteRequest(string? Text)
{
    public SetSubHabitNoteCommand ToCommand(Guid habitId, Guid subHabitId, DateOnly date)
        => new(habitId, subHabitId, date, Text);
}
