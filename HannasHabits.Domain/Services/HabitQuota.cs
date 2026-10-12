using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Services;

/// <summary>How far one calendar week or month of a quota habit has come: the period and the days completed in it.</summary>
/// <param name="Period">The whole week or month, also where it began before the habit's start date or ends after the range.</param>
/// <param name="Done">How many days of the period were completed (see <see cref="HabitQuota.DoneIn"/> for what counts).</param>
public sealed record QuotaProgress(CalendarPeriod Period, int Done);

/// <summary>
/// The quota half of a habit's rules as pure functions of its schedule, start date and completed days - the sibling of
/// <see cref="HabitDueDays"/> and <see cref="HabitStreak"/> for the read side, which loads dates, not aggregates. The
/// week is Monday to Sunday and the month is the calendar month, so "this week" means what a calendar on the wall says.
/// </summary>
public static class HabitQuota
{
    /// <summary>
    /// The days of <paramref name="period"/> that were completed and count: not before <paramref name="startDate"/>
    /// (those days do not exist for the habit, like for the streak) and not after <paramref name="asOf"/> (the caller's
    /// "today" is the horizon; a record beyond it is ignored, as in <see cref="HabitStreak"/>).
    /// </summary>
    public static int DoneIn(
        CalendarPeriod period, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // Counting day numbers instead of calling AddDays cannot run off the end of the calendar.
        var first = Math.Max(period.From.DayNumber, startDate.DayNumber);
        var last = Math.Min(period.To.DayNumber, asOf.DayNumber);
        var done = 0;

        for (var dayNumber = first; dayNumber <= last; dayNumber++)
        {
            if (completedDates.Contains(DateOnly.FromDayNumber(dayNumber)))
                done++;
        }

        return done;
    }

    /// <summary>
    /// The progress of every week or month that overlaps <paramref name="from"/> to <paramref name="to"/> (both
    /// inclusive), oldest first. A period that ended before <paramref name="startDate"/> is left out - it does not exist
    /// for the habit - and one that has not begun yet is listed with 0. Empty for a schedule that is not a quota, and
    /// for a turned-round range.
    /// </summary>
    public static IReadOnlyList<QuotaProgress> Between(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates,
        DateOnly from, DateOnly to, DateOnly asOf)
    {
        if (!schedule.IsQuota || from > to)
            return [];

        var progress = new List<QuotaProgress>();
        var period = schedule.PeriodOf(from);

        while (period.From <= to)
        {
            if (period.To >= startDate)
                progress.Add(new QuotaProgress(period, DoneIn(period, startDate, completedDates, asOf)));

            if (period.To.DayNumber == DateOnly.MaxValue.DayNumber)
                break;

            period = schedule.PeriodOf(DateOnly.FromDayNumber(period.To.DayNumber + 1));
        }

        return progress;
    }
}
