using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// A whole number from <see cref="MinValue"/> to <see cref="MaxValue"/>, e.g. how energised or clear a day felt.
/// Immutable; two percentages with the same value are equal (record equality). An instance can only exist if it is valid.
/// </summary>
public sealed record Percentage
{
    public const int MinValue = 0;
    public const int MaxValue = 100;

    public int Value { get; }

    private Percentage(int value)
    {
        Value = value;
    }

    public static Percentage Create(int value)
    {
        if (value is < MinValue or > MaxValue)
            throw new DomainException($"A percentage must be between {MinValue} and {MaxValue}.");

        return new Percentage(value);
    }

    public override string ToString() => $"{Value}%";
}
