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
    /// The number of scheduled days in a row that were completed, counted back from <paramref name="asOf"/>.
    /// Unscheduled days neither count nor break the streak (a record on one is ignored). <paramref name="asOf"/>
    /// itself counts if completed, and is skipped - not a break - if it is still open. The first earlier scheduled day
    /// that was not completed ends the streak, as does <paramref name="startDate"/>: days before it do not exist for the
    /// habit. Records after <paramref name="asOf"/> are ignored.
    /// </summary>
    /// <param name="asOf">The caller's local "today"; the server's clock would be off by a day for some time zones.</param>
    public static int Current(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // Counting day numbers instead of calling AddDays(-1) cannot run off the start of the calendar.
        var daysBack = asOf.DayNumber - startDate.DayNumber;
        var streak = 0;

        for (var offset = 0; offset <= daysBack; offset++)
        {
            var day = DateOnly.FromDayNumber(asOf.DayNumber - offset);

            if (!schedule.IncludesDay(day.DayOfWeek))
                continue;

            if (completedDates.Contains(day))
                streak++;
            else if (offset > 0)
                break;
        }

        return streak;
    }
}
