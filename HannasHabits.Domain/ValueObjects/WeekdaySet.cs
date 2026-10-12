using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// A set of weekdays that is never empty ("Monday and Wednesday"). Immutable; two sets with the same days are equal
/// (record equality). Stored as a bit set: one bit per <see cref="DayOfWeek"/> (Sunday = bit 0 ... Saturday = bit 6).
/// </summary>
/// <remarks>
/// <c>HabitSchedule</c> keeps its own private copy of the same bit trick; it predates this type and has no reason to
/// change for it.
/// </remarks>
public sealed record WeekdaySet
{
    /// <summary>All seven weekdays.</summary>
    public const int AllDaysMask = 0b111_1111;

    /// <summary>The bits, for persistence. Always between 1 and <see cref="AllDaysMask"/>.</summary>
    public int Mask { get; }

    /// <summary>The days in ascending <see cref="DayOfWeek"/> order (Sunday first), never empty.</summary>
    public IReadOnlyList<DayOfWeek> Days => Enum.GetValues<DayOfWeek>().Where(Contains).ToArray();

    private WeekdaySet(int mask)
    {
        Mask = mask;
    }

    /// <summary>The given days; duplicates collapse. At least one day is required.</summary>
    public static WeekdaySet Of(IEnumerable<DayOfWeek> days)
    {
        var mask = 0;

        foreach (var day in days)
        {
            if (!Enum.IsDefined(day))
                throw new DomainException($"'{(int)day}' is not a day of the week.");

            mask |= 1 << (int)day;
        }

        return FromMask(mask);
    }

    /// <summary>Rebuilds a set from its <see cref="Mask"/> (what the database holds).</summary>
    public static WeekdaySet FromMask(int mask)
    {
        if (mask is < 1 or > AllDaysMask)
            throw new DomainException("A set of weekdays must contain at least one day of the week.");

        return new WeekdaySet(mask);
    }

    public bool Contains(DayOfWeek day) => (Mask & (1 << (int)day)) != 0;

    /// <summary>Whether every day of this set is also in <paramref name="other"/>.</summary>
    public bool IsSubsetOf(WeekdaySet other) => (Mask & ~other.Mask) == 0;

    public override string ToString() => string.Join(", ", Days);
}
