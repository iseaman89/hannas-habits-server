namespace HannasHabits.Domain.Enums;

/// <summary>
/// Which occurrence of a weekday within a month. There is no "fifth": not every month has one, so "the last" is the
/// way to say it.
/// </summary>
public enum WeekdayOccurrence
{
    First = 1,
    Second = 2,
    Third = 3,
    Fourth = 4,
    Last = 5
}
