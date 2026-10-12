using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// The name of a tag: trimmed, not empty and at most <see cref="MaxLength"/> characters. Immutable; an instance can only
/// exist if it is valid. Two names that differ only in case ("krank", "Krank") name the same tag: they have the same
/// <see cref="Key"/>. Record equality stays exact (on <see cref="Value"/>) on purpose - a rename that only changes the
/// case must still count as a change.
/// </summary>
public sealed record TagName
{
    public const int MaxLength = 30;

    /// <summary>The name as it is shown, with the spelling the person chose.</summary>
    public string Value { get; }

    /// <summary>
    /// What names are compared by: the case-folded <see cref="Value"/>, the same on every machine (invariant culture,
    /// ordinal) - which is why it is computed here and not left to the database's collation.
    /// </summary>
    public string Key => Value.ToUpperInvariant();

    private TagName(string value)
    {
        Value = value;
    }

    public static TagName Create(string? value)
    {
        var name = value?.Trim();

        if (string.IsNullOrEmpty(name))
            throw new DomainException("A tag name must not be empty.");

        if (name.Length > MaxLength)
            throw new DomainException($"A tag name must not be longer than {MaxLength} characters.");

        return new TagName(name);
    }

    public override string ToString() => Value;
}
