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
            : CurrentByDays(schedule, startDate, completedDates, asOf);

    private static int CurrentByDays(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // Counting day numbers instead of calling AddDays(-1) cannot run off the start of the calendar.
        var daysBack = asOf.DayNumber - startDate.DayNumber;
        var streak = 0;

        for (var offset = 0; offset <= daysBack; offset++)
        {
            var day = DateOnly.FromDayNumber(asOf.DayNumber - offset);

            if (!schedule.IsScheduledOn(day))
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
}
