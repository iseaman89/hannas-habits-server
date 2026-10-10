namespace HannasHabits.Domain.ValueObjects;

/// <summary>
/// A calendar week (Monday to Sunday) or a calendar month, as the two days it spans (both included). The period a quota
/// rule ("3 times a week") is counted in. Immutable and valid by construction: only the factories make one, so
/// <see cref="From"/> is never after <see cref="To"/>.
/// </summary>
public sealed record CalendarPeriod
{
    public DateOnly From { get; private init; }
    public DateOnly To { get; private init; }

    // For the factories only.
    private CalendarPeriod()
    {
    }

    /// <summary>
    /// The week - Monday to Sunday, like every week in this app - that contains <paramref name="date"/>. Day number 0
    /// (0001-01-01) is a Monday, so the day number divided by 7 is the number of the week and no calendar lookup is needed.
    /// </summary>
    public static CalendarPeriod WeekOf(DateOnly date)
    {
        var monday = date.DayNumber - date.DayNumber % 7;

        // The clamp only matters in the last week of year 9999.
        return new CalendarPeriod
        {
            From = DateOnly.FromDayNumber(monday),
            To = DateOnly.FromDayNumber(Math.Min(monday + 6, DateOnly.MaxValue.DayNumber))
        };
    }

    /// <summary>The calendar month that contains <paramref name="date"/>.</summary>
    public static CalendarPeriod MonthOf(DateOnly date) => new()
    {
        From = new DateOnly(date.Year, date.Month, 1),
        To = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month))
    };

    /// <summary>The number of days, both ends included.</summary>
    public int Length => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= From && date <= To;

    public override string ToString() => $"{From:O}..{To:O}";
}
