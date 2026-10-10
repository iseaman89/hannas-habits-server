using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// The rule that decides on which days a habit is due, answered by <see cref="IsScheduledOn"/>: on chosen weekdays
/// (every week or every N weeks), every N days, on one weekday of the month ("the last Saturday") or on one date of the
/// month. Immutable; two schedules with the same rule are equal (record equality). Every instance is valid: it can only
/// be made through one of the factories.
/// </summary>
/// <remarks>
/// One sealed type with a <see cref="Kind"/> instead of a class per rule: a schedule is stored in the columns of its
/// habit (EF Core cannot map a polymorphic value object to flat columns), and four small rules do not need four types.
/// The rules that count from a day (<see cref="Anchor"/>) keep their own anchor - it is part of the rule, not the
/// habit's start date, so moving the start date never shifts the rhythm.
/// </remarks>
public sealed record HabitSchedule
{
    /// <summary>The longest "every N weeks".</summary>
    public const int MaxWeekInterval = 52;

    /// <summary>The shortest and longest "every N days" (every day is <see cref="Daily"/>).</summary>
    public const int MinDayInterval = 2;
    public const int MaxDayInterval = 365;

    public const int MaxDayOfMonth = 31;

    /// <summary>The same plausible range as a habit's start date: keeps absurd anchors out of the day arithmetic.</summary>
    public static readonly DateOnly MinAnchor = new(2000, 1, 1);
    public static readonly DateOnly MaxAnchor = new(2100, 12, 31);

    /// <summary>All seven weekdays as a bit set: one bit per <see cref="DayOfWeek"/> (Sunday = bit 0 ... Saturday = bit 6).</summary>
    public const int AllDaysMask = 0b111_1111;

    // The weekdays a due day can fall on, one bit each. Set for every kind (all seven where the weekday does not matter),
    // so a stored schedule always has a non-empty set. Private: the Domain exposes the days, persistence knows the bits.
    private int WeekdayMask { get; init; }

    /// <summary>The default: every day of the week.</summary>
    public static HabitSchedule Daily { get; } = new()
    {
        Kind = ScheduleKind.Weekly, WeekdayMask = AllDaysMask, Interval = 1
    };

    public ScheduleKind Kind { get; private init; }

    /// <summary>
    /// Every N weeks (<see cref="ScheduleKind.Weekly"/>, 1 = every week) or every N days (<see cref="ScheduleKind.EveryNDays"/>);
    /// 1 for the monthly kinds, where it does not apply.
    /// </summary>
    public int Interval { get; private init; }

    /// <summary>
    /// The day the rhythm counts from: set for "every N weeks" (N &gt; 1, counted in weeks that start on Monday) and
    /// "every N days"; <c>null</c> where no day is needed.
    /// </summary>
    public DateOnly? Anchor { get; private init; }

    /// <summary>Set for <see cref="ScheduleKind.MonthlyOnWeekday"/> only.</summary>
    public WeekdayOccurrence? Occurrence { get; private init; }

    /// <summary>
    /// 1..31, set for <see cref="ScheduleKind.MonthlyOnDate"/> only. A date a month does not have ("the 31st" in April)
    /// falls on that month's last day, so the habit is never silently skipped.
    /// </summary>
    public int? DayOfMonth { get; private init; }

    /// <summary>
    /// The weekdays a due day can fall on, in ascending <see cref="DayOfWeek"/> order (Sunday first), never empty: the
    /// chosen days of a weekly schedule, the one weekday of "the last Saturday", all seven for the rules that do not
    /// depend on the weekday. Not the rule itself - <see cref="IsScheduledOn"/> is.
    /// </summary>
    public IReadOnlyList<DayOfWeek> Days => Enum.GetValues<DayOfWeek>().Where(HasWeekday).ToArray();

    // For EF Core only; it sets every property after materialization.
    private HabitSchedule()
    {
    }

    /// <summary>
    /// On the given weekdays, every week - or, with <paramref name="everyWeeks"/> above 1, only in every N-th week
    /// (weeks start on Monday and are counted from the week of <paramref name="anchor"/>, which is then required).
    /// Duplicate days collapse; <paramref name="anchor"/> is ignored for "every week".
    /// </summary>
    public static HabitSchedule Weekly(IEnumerable<DayOfWeek> days, int everyWeeks = 1, DateOnly? anchor = null)
    {
        var mask = MaskOf(days);

        if (everyWeeks is < 1 or > MaxWeekInterval)
            throw new DomainException($"A habit can repeat every 1 to {MaxWeekInterval} weeks.");

        if (everyWeeks == 1)
            return new HabitSchedule { Kind = ScheduleKind.Weekly, WeekdayMask = mask, Interval = 1 };

        return new HabitSchedule
        {
            Kind = ScheduleKind.Weekly,
            WeekdayMask = mask,
            Interval = everyWeeks,
            Anchor = ValidateAnchor(anchor
                ?? throw new DomainException($"A habit that repeats every {everyWeeks} weeks needs the day its weeks are counted from."))
        };
    }

    /// <summary>Every <paramref name="days"/> days, counted from <paramref name="anchor"/> (which is due itself).</summary>
    public static HabitSchedule EveryNDays(int days, DateOnly anchor)
    {
        if (days is < MinDayInterval or > MaxDayInterval)
            throw new DomainException($"A habit can repeat every {MinDayInterval} to {MaxDayInterval} days (every day is the daily schedule).");

        return new HabitSchedule
        {
            Kind = ScheduleKind.EveryNDays,
            WeekdayMask = AllDaysMask,
            Interval = days,
            Anchor = ValidateAnchor(anchor)
        };
    }

    /// <summary>On one weekday of every month, e.g. the first Monday or the last Saturday.</summary>
    public static HabitSchedule MonthlyOnWeekday(WeekdayOccurrence occurrence, DayOfWeek day)
    {
        if (!Enum.IsDefined(occurrence))
            throw new DomainException($"'{(int)occurrence}' is not an occurrence of a weekday in a month.");

        return new HabitSchedule
        {
            Kind = ScheduleKind.MonthlyOnWeekday,
            WeekdayMask = MaskOf([day]),
            Interval = 1,
            Occurrence = occurrence
        };
    }

    /// <summary>On one date of every month; 29 to 31 fall on the last day of a month that is shorter.</summary>
    public static HabitSchedule MonthlyOnDate(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > MaxDayOfMonth)
            throw new DomainException($"The day of the month must be between 1 and {MaxDayOfMonth}.");

        return new HabitSchedule
        {
            Kind = ScheduleKind.MonthlyOnDate,
            WeekdayMask = AllDaysMask,
            Interval = 1,
            DayOfMonth = dayOfMonth
        };
    }

    /// <summary>
    /// Whether the habit is due on <paramref name="date"/> by this rule alone - the habit's start date is not the
    /// schedule's business. Defined for every date, also before an anchor.
    /// </summary>
    public bool IsScheduledOn(DateOnly date) => Kind switch
    {
        ScheduleKind.Weekly => HasWeekday(date.DayOfWeek) && (Interval == 1 || IsInAnchoredWeek(date)),
        ScheduleKind.EveryNDays => FloorMod(date.DayNumber - Anchor!.Value.DayNumber, Interval) == 0,
        ScheduleKind.MonthlyOnWeekday => HasWeekday(date.DayOfWeek) && IsOccurrenceInMonth(date),
        ScheduleKind.MonthlyOnDate => date.Day == Math.Min(DayOfMonth!.Value, DateTime.DaysInMonth(date.Year, date.Month)),
        _ => throw new InvalidOperationException($"Unknown schedule kind '{Kind}'.")
    };

    // 0001-01-01 - day number 0 - is a Monday, so the day number divided by 7 counts Monday-based weeks without any
    // calendar lookup (and a Sunday belongs to the week that began on the Monday before it).
    private bool IsInAnchoredWeek(DateOnly date)
        => FloorMod(date.DayNumber / 7 - Anchor!.Value.DayNumber / 7, Interval) == 0;

    private bool IsOccurrenceInMonth(DateOnly date) => Occurrence switch
    {
        WeekdayOccurrence.Last => date.Day + 7 > DateTime.DaysInMonth(date.Year, date.Month),
        { } occurrence => (date.Day - 1) / 7 + 1 == (int)occurrence,
        null => throw new InvalidOperationException("A monthly-on-weekday schedule without an occurrence.")
    };

    private bool HasWeekday(DayOfWeek day) => (WeekdayMask & (1 << (int)day)) != 0;

    // Unlike %, the result is never negative: dates before the anchor follow the same rhythm.
    private static int FloorMod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    private static int MaskOf(IEnumerable<DayOfWeek> days)
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

        return mask;
    }

    private static DateOnly ValidateAnchor(DateOnly anchor)
        => anchor < MinAnchor || anchor > MaxAnchor
            ? throw new DomainException($"The day a schedule is counted from must be between {MinAnchor:O} and {MaxAnchor:O}.")
            : anchor;

    public override string ToString() => Kind switch
    {
        ScheduleKind.Weekly when Interval == 1 => string.Join(", ", Days),
        ScheduleKind.Weekly => $"Every {Interval} weeks on {string.Join(", ", Days)} (weeks counted from {Anchor:O})",
        ScheduleKind.EveryNDays => $"Every {Interval} days (counted from {Anchor:O})",
        ScheduleKind.MonthlyOnWeekday => $"The {Occurrence!.Value.ToString().ToLowerInvariant()} {Days.Single()} of the month",
        ScheduleKind.MonthlyOnDate => $"Day {DayOfMonth} of the month",
        _ => Kind.ToString()
    };
}
