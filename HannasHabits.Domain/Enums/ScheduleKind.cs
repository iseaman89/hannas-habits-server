namespace HannasHabits.Domain.Enums;

/// <summary>The rule a <c>HabitSchedule</c> uses to decide which days a habit is due on.</summary>
public enum ScheduleKind
{
    /// <summary>On chosen weekdays, every week or every N weeks ("Monday and Thursday", "every 2nd week on Saturday").</summary>
    Weekly = 0,

    /// <summary>Every N days, counted from a fixed day ("every 3 days").</summary>
    EveryNDays = 1,

    /// <summary>One weekday of the month ("the first Monday", "the last Saturday").</summary>
    MonthlyOnWeekday = 2,

    /// <summary>One date of the month ("the 15th", "the last day").</summary>
    MonthlyOnDate = 3,

    /// <summary>
    /// A quota, no fixed day: N times in every calendar week, on any days ("3 times a week"). The streak counts weeks
    /// that reached it.
    /// </summary>
    TimesPerWeek = 4,

    /// <summary>A quota, no fixed day: N times in every calendar month, on any days. The streak counts months.</summary>
    TimesPerMonth = 5
}
