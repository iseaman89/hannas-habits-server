using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// The days of the week on which a habit is planned: a non-empty set of <see cref="DayOfWeek"/>. Immutable; two
/// schedules with the same days are equal (record equality over the private bit set). Duplicates in the input collapse.
/// </summary>
public sealed record HabitSchedule
{
    private const int AllDaysMask = 0b111_1111;

    // One bit per DayOfWeek (Sunday = bit 0 ... Saturday = bit 6): a set without allocating a collection to compare.
    private readonly int _mask;

    /// <summary>The default: every day of the week.</summary>
    public static HabitSchedule Daily { get; } = new(AllDaysMask);

    /// <summary>The scheduled days in ascending <see cref="DayOfWeek"/> order (Sunday first), never empty.</summary>
    public IReadOnlyList<DayOfWeek> Days => Enum.GetValues<DayOfWeek>().Where(IncludesDay).ToArray();

    private HabitSchedule(int mask)
    {
        _mask = mask;
    }

    public static HabitSchedule Create(IEnumerable<DayOfWeek> days)
    {
        var mask = 0;

        foreach (var day in days)
        {
            if (!Enum.IsDefined(day))
                throw new DomainException($"'{(int)day}' is not a day of the week.");

            mask |= 1 << (int)day;
        }

        if (mask == 0)
            throw new DomainException("A habit must be scheduled on at least one day of the week.");

        return new HabitSchedule(mask);
    }

    public bool IncludesDay(DayOfWeek day) => (_mask & (1 << (int)day)) != 0;

    public override string ToString() => string.Join(", ", Days);
}
