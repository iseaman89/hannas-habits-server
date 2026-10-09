using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// The title of a habit: trimmed, not empty and at most <see cref="MaxLength"/> characters. Immutable; two titles with
/// the same text are equal (record equality). An instance can only exist if it is valid.
/// </summary>
public sealed record HabitTitle
{
    public const int MaxLength = 150;

    public string Value { get; }

    private HabitTitle(string value)
    {
        Value = value;
    }

    public static HabitTitle Create(string? value)
    {
        var title = value?.Trim();

        if (string.IsNullOrEmpty(title))
            throw new DomainException("A habit title must not be empty.");

        if (title.Length > MaxLength)
            throw new DomainException($"A habit title must not be longer than {MaxLength} characters.");

        return new HabitTitle(title);
    }

    public override string ToString() => Value;
}
