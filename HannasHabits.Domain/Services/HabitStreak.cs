using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Services;

/// <summary>
/// The streak rule of a habit as a pure function of three facts (schedule, start date, completed days), so it also works
/// on the read side, where no <c>Habit</c> aggregate is loaded - only the dates. A stateless domain service: the rule
/// belongs to the Domain, but not to a single entity instance.
/// </summary>
public static class HabitStreak
{
    /// <summary>
    /// The current streak as of <paramref name="asOf"/>. For a rule with due days it counts <b>days</b>: the scheduled
    /// days in a row that were completed, counted back from <paramref name="asOf"/>. Unscheduled days neither count nor
    /// break the streak (a record on one is ignored). <paramref name="asOf"/> itself counts if completed, and is
    /// skipped - not a break - if it is still open. The first earlier scheduled day that was not completed ends the
    /// streak, as does <paramref name="startDate"/>: days before it do not exist for the habit. Records after
    /// <paramref name="asOf"/> are ignored.
    /// <para>
    /// For a quota ("3 times a week") it counts <b>weeks or months</b> instead, by the same principle: the periods in a
    /// row that reached their N, counted back from the one that contains <paramref name="asOf"/>. That one counts if it
    /// has reached its N and is skipped - not a break - while it is still running. The first earlier period that fell
    /// short ends the streak, as does the period the habit started in (it counts only if it reached its N).
    /// </para>
    /// </summary>
    /// <param name="asOf">The caller's local "today"; the server's clock would be off by a day for some time zones.</param>
    public static int Current(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
        => schedule.IsQuota
            ? CurrentByPeriods(schedule, startDate, completedDates, asOf)
            : CurrentByDays(day => HabitDueDays.IsDue(schedule, startDate, day), startDate, completedDates, asOf);

    /// <summary>
    /// The current streak of a sub-habit planned on <paramref name="weekdays"/>, by the same principle as for the
    /// habit: its due days (the habit is due and the weekday is the part's, see
    /// <see cref="HabitDueDays.IsDue(HabitSchedule, DateOnly, WeekdaySet, DateOnly)"/>) in a row that were ticked, counted
    /// back from <paramref name="asOf"/>. It is always counted in <b>days</b> - also under a quota habit, where every
    /// day of the week is a due day, as in <see cref="HabitStatistics"/>.
    /// </summary>
    public static int Current(
        HabitSchedule schedule, DateOnly startDate, WeekdaySet weekdays, IReadOnlySet<DateOnly> completedDates,
        DateOnly asOf)
        => CurrentByDays(
            day => HabitDueDays.IsDue(schedule, startDate, weekdays, day), startDate, completedDates, asOf);

    /// <summary>
    /// The longest streak the habit ever had up to <paramref name="asOf"/>: the same run as <see cref="Current"/> -
    /// due days (or, for a quota, weeks or months) in a row that were completed, a record on a day that was not due is
    /// ignored, days before <paramref name="startDate"/> and records after <paramref name="asOf"/> do not exist - but the
    /// best run of the <b>whole history</b> instead of the one that ends now. So it is never shorter than
    /// <see cref="Current"/>. A day that is still open <paramref name="asOf"/> ends nothing: no run ends there yet.
    /// </summary>
    public static int Longest(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
        => schedule.IsQuota
            ? LongestByPeriods(schedule, startDate, completedDates, asOf)
            : LongestByDays(day => HabitDueDays.IsDue(schedule, startDate, day), startDate, completedDates, asOf);

    /// <summary>The longest streak a sub-habit planned on <paramref name="weekdays"/> ever had, counted in days.</summary>
    public static int Longest(
        HabitSchedule schedule, DateOnly startDate, WeekdaySet weekdays, IReadOnlySet<DateOnly> completedDates,
        DateOnly asOf)
        => LongestByDays(
            day => HabitDueDays.IsDue(schedule, startDate, weekdays, day), startDate, completedDates, asOf);

    private static int CurrentByDays(
        Func<DateOnly, bool> isDue, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // Counting day numbers instead of calling AddDays(-1) cannot run off the start of the calendar.
        var daysBack = asOf.DayNumber - startDate.DayNumber;
        var streak = 0;

        for (var offset = 0; offset <= daysBack; offset++)
        {
            var day = DateOnly.FromDayNumber(asOf.DayNumber - offset);

            if (!isDue(day))
                continue;

            if (completedDates.Contains(day))
                streak++;
            else if (offset > 0)
                break;
        }

        return streak;
    }

    private static int CurrentByPeriods(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // A start date after "today": nothing has begun.
        if (asOf < startDate)
            return 0;

        var times = schedule.Times!.Value;
        var running = schedule.PeriodOf(asOf);
        var period = running;
        var streak = 0;

        while (true)
        {
            if (HabitQuota.DoneIn(period, startDate, completedDates, asOf) >= times)
                streak++;
            else if (period != running)
                break;

            // The period of the start date is the oldest one the habit has.
            if (period.From <= startDate)
                break;

            period = schedule.PeriodOf(DateOnly.FromDayNumber(period.From.DayNumber - 1));
        }

        return streak;
    }

    private static int LongestByDays(
        Func<DateOnly, bool> isDue, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // A run can only begin with a completed day: nothing before the first one has to be walked.
        var first = completedDates
            .Where(day => day >= startDate && day <= asOf)
            .Select(day => day.DayNumber)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        // Counting day numbers instead of calling AddDays(1) cannot run past the end of the calendar.
        var longest = 0;
        var run = 0;

        for (var dayNumber = first; dayNumber <= asOf.DayNumber; dayNumber++)
        {
            var day = DateOnly.FromDayNumber(dayNumber);

            if (!isDue(day))
                continue;

            if (completedDates.Contains(day))
                longest = Math.Max(longest, ++run);
            else
                run = 0;
        }

        return longest;
    }

    private static int LongestByPeriods(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // A start date after "today": nothing has begun.
        if (asOf < startDate)
            return 0;

        var times = schedule.Times!.Value;
        var running = schedule.PeriodOf(asOf);
        var period = schedule.PeriodOf(startDate); // the oldest period the habit has
        var longest = 0;
        var run = 0;

        while (true)
        {
            if (HabitQuota.DoneIn(period, startDate, completedDates, asOf) >= times)
                longest = Math.Max(longest, ++run);
            else
                run = 0;

            if (period == running)
                break;

            period = schedule.PeriodOf(DateOnly.FromDayNumber(period.To.DayNumber + 1));
        }

        return longest;
    }
}
