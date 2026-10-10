using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Infrastructure.Persistence.Configurations;

/// <summary>
/// The columns of a habit's schedule and the check constraints that keep a row that bypasses the application valid.
/// Shared by the entity configuration and the tests, so the SQL and the Domain limits cannot drift apart unnoticed.
/// </summary>
public static class HabitScheduleColumns
{
    public const string Kind = "ScheduleKind";
    public const string Weekdays = "ScheduleWeekdays";
    public const string Interval = "ScheduleInterval";
    public const string Anchor = "ScheduleAnchor";
    public const string Occurrence = "ScheduleOccurrence";
    public const string DayOfMonth = "ScheduleDayOfMonth";

    /// <summary>At least one day, all seven at most.</summary>
    public static string WeekdaysCheck => $"\"{Weekdays}\" BETWEEN 1 AND {HabitSchedule.AllDaysMask}";

    public static string AnchorCheck
        => $"\"{Anchor}\" IS NULL OR \"{Anchor}\" BETWEEN DATE '{HabitSchedule.MinAnchor:yyyy-MM-dd}' AND DATE '{HabitSchedule.MaxAnchor:yyyy-MM-dd}'";

    /// <summary>
    /// Which facts a kind of schedule has and which it must not have, mirroring the Domain's factories. A check passes
    /// when it evaluates to NULL, so a fact that is required says <c>IS NOT NULL</c> explicitly:
    /// <list type="bullet">
    /// <item>weekly: every week without an anchor, or every 2..N weeks with one</item>
    /// <item>every N days: all weekdays, N days, an anchor</item>
    /// <item>monthly on a weekday: exactly one weekday and an occurrence</item>
    /// <item>monthly on a date: all weekdays and a day of the month</item>
    /// </list>
    /// </summary>
    public static string RuleCheck
    {
        get
        {
            var kind = $"\"{Kind}\"";
            var weekdays = $"\"{Weekdays}\"";
            var interval = $"\"{Interval}\"";
            var anchor = $"\"{Anchor}\"";
            var occurrence = $"\"{Occurrence}\"";
            var dayOfMonth = $"\"{DayOfMonth}\"";

            return
                $"({kind} = {(int)ScheduleKind.Weekly} AND {interval} BETWEEN 1 AND {HabitSchedule.MaxWeekInterval}" +
                $" AND (({interval} = 1) = ({anchor} IS NULL)) AND {occurrence} IS NULL AND {dayOfMonth} IS NULL)" +
                $" OR ({kind} = {(int)ScheduleKind.EveryNDays} AND {weekdays} = {HabitSchedule.AllDaysMask}" +
                $" AND {interval} BETWEEN {HabitSchedule.MinDayInterval} AND {HabitSchedule.MaxDayInterval}" +
                $" AND {anchor} IS NOT NULL AND {occurrence} IS NULL AND {dayOfMonth} IS NULL)" +
                $" OR ({kind} = {(int)ScheduleKind.MonthlyOnWeekday} AND ({weekdays} & ({weekdays} - 1)) = 0" +
                $" AND {interval} = 1 AND {anchor} IS NULL AND {occurrence} IS NOT NULL AND {occurrence} BETWEEN 1 AND 5" +
                $" AND {dayOfMonth} IS NULL)" +
                $" OR ({kind} = {(int)ScheduleKind.MonthlyOnDate} AND {weekdays} = {HabitSchedule.AllDaysMask}" +
                $" AND {interval} = 1 AND {anchor} IS NULL AND {occurrence} IS NULL" +
                $" AND {dayOfMonth} IS NOT NULL AND {dayOfMonth} BETWEEN 1 AND {HabitSchedule.MaxDayOfMonth})";
        }
    }
}
