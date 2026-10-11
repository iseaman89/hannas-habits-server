using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.SubHabits;

/// <summary>A note on a part of a habit on one day.</summary>
/// <param name="Text">Free text, trimmed, at most 500 characters; line breaks inside are kept.</param>
public record SubHabitNoteDto(DateOnly Date, string Text)
{
    public static SubHabitNoteDto From(SubHabitNote note) => new(note.Date, note.Text);
}

/// <summary>A note as the read side hands it over: which part, which day, what it says.</summary>
public record SubHabitNoteRow(Guid SubHabitId, DateOnly Date, string Text);
