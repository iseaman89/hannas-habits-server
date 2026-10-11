using HannasHabits.Domain.Common;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.Entities;

/// <summary>
/// A free-text note on one part of a habit on one day ("Legs, 5 Oct: 3 x 10, left knee felt odd"). Independent of the
/// tick - a missed day can carry a note, that is the point - and at most one per part and day.
/// It is an aggregate of its own and not a part of <see cref="Habit"/>: no rule ties a note to the habit's ticks or
/// days, so writing one needs neither the habit's history loaded nor its day kept in step. The part is referenced by id
/// only; its notes go with it (the database cascades).
/// </summary>
public class SubHabitNote : EntityBase
{
    public const int TextMaxLength = 500;

    // The same plausible range as the habit's start date: keeps absurd dates such as 0001-01-01 out of the store.
    public static readonly DateOnly MinDate = Habit.MinStartDate;
    public static readonly DateOnly MaxDate = Habit.MaxStartDate;

    public Guid SubHabitId { get; private set; }
    public DateOnly Date { get; private set; }
    public string Text { get; private set; }

    // Parameterless constructor for EF Core only; it overwrites the values after materialization.
    private SubHabitNote()
    {
        Text = null!;
    }

    private SubHabitNote(Guid subHabitId, DateOnly date, string text)
    {
        SubHabitId = subHabitId;
        Date = date;
        Text = text;
    }

    /// <param name="text">Not empty after trimming, at most <see cref="TextMaxLength"/> characters.</param>
    public static SubHabitNote Create(Guid subHabitId, DateOnly date, string? text)
    {
        if (date < MinDate || date > MaxDate)
            throw new DomainException($"The day must be between {MinDate:O} and {MaxDate:O}.");

        return new SubHabitNote(subHabitId, date, Normalize(text));
    }

    /// <summary>Replaces the text. A note that is not wanted any more is removed, not emptied.</summary>
    public void Rewrite(string? text)
        => Text = Normalize(text);

    // Only the ends are trimmed: line breaks inside are what the person wrote.
    private static string Normalize(string? text)
    {
        var normalized = text?.Trim();

        if (string.IsNullOrEmpty(normalized))
            throw new DomainException("A note must not be empty; take it away instead.");

        if (normalized.Length > TextMaxLength)
            throw new DomainException($"A note must not be longer than {TextMaxLength} characters.");

        return normalized;
    }
}
