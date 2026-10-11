using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Services;

/// <summary>
/// Which days of a range a habit is due on, as a pure function of its schedule and start date - the sibling of
/// <see cref="HabitStreak"/> for the read side, which has no <c>Habit</c> aggregate. It puts the one rule "a day is due
/// if the schedule says so and the habit has started" in the Domain, so a client never has to re-derive it.
/// </summary>
public static class HabitDueDays
{
    /// <summary>Whether the habit is due on <paramref name="date"/>: the schedule says so and the habit has started.</summary>
    public static bool IsDue(HabitSchedule schedule, DateOnly startDate, DateOnly date)
        => date >= startDate && schedule.IsScheduledOn(date);

    /// <summary>
    /// Whether a sub-habit planned on <paramref name="weekdays"/> is due on <paramref name="date"/>: the habit is due
    /// that day and the weekday is one of the sub-habit's. The one rule the aggregate and the read side both use.
    /// </summary>
    public static bool IsDue(HabitSchedule schedule, DateOnly startDate, WeekdaySet weekdays, DateOnly date)
        => weekdays.Contains(date.DayOfWeek) && IsDue(schedule, startDate, date);

    /// <summary>
    /// The days of the range on which a sub-habit planned on <paramref name="weekdays"/> is due: the due days of the
    /// habit that fall on one of those weekdays, oldest first.
    /// </summary>
    public static IReadOnlyList<DateOnly> Between(
        HabitSchedule schedule, DateOnly startDate, WeekdaySet weekdays, DateOnly from, DateOnly to)
        => Between(schedule, startDate, from, to).Where(day => weekdays.Contains(day.DayOfWeek)).ToList();

    /// <summary>
    /// The days from <paramref name="from"/> to <paramref name="to"/> (both inclusive) on which the habit is due,
    /// oldest first. Days before <paramref name="startDate"/> are never due; an empty list if the range ends before it,
    /// or is turned round.
    /// </summary>
    public static IReadOnlyList<DateOnly> Between(HabitSchedule schedule, DateOnly startDate, DateOnly from, DateOnly to)
    {
        var due = new List<DateOnly>();

        // Counting day numbers instead of calling AddDays(1) cannot run past the end of the calendar.
        var first = Math.Max(from.DayNumber, startDate.DayNumber);

        for (var dayNumber = first; dayNumber <= to.DayNumber; dayNumber++)
        {
            var day = DateOnly.FromDayNumber(dayNumber);

            if (schedule.IsScheduledOn(day))
                due.Add(day);
        }

        return due;
    }
}
