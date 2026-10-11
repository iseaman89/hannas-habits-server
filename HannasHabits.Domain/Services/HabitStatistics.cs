using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Services;

/// <summary>How much of a range was planned and how much of that was done, in <paramref name="Unit"/>s.</summary>
/// <param name="Unit">What is counted: days, or for a quota weeks or months. A sub-habit always counts days.</param>
/// <param name="Planned">What was to be done in the range and is already judged.</param>
/// <param name="Done">How many of the <paramref name="Planned"/> were met; never more than <paramref name="Planned"/>.</param>
public sealed record StatisticsTally(CountingUnit Unit, int Planned, int Done);

/// <summary>
/// The statistic of a habit or of one of its sub-habits as pure functions of schedule, start date and completed days -
/// the sibling of <see cref="HabitStreak"/>, <see cref="HabitDueDays"/> and <see cref="HabitQuota"/> for the read side,
/// which loads dates, not aggregates. The same principles as the streak: records on days that are not due are ignored,
/// days before the start date do not exist, records after "today" are ignored, and a day that is still open today is
/// not yet a miss.
/// </summary>
public static class HabitStatistics
{
    /// <summary>What a habit with this rule is counted in.</summary>
    public static CountingUnit UnitOf(HabitSchedule schedule) => schedule.Kind switch
    {
        ScheduleKind.TimesPerWeek => CountingUnit.Weeks,
        ScheduleKind.TimesPerMonth => CountingUnit.Months,
        _ => CountingUnit.Days
    };

    /// <summary>
    /// How much of the habit was planned and done from <paramref name="from"/> to <paramref name="to"/> (both
    /// inclusive).
    /// <para>
    /// A rule with due days counts <b>days</b>: the due days of the range up to <paramref name="asOf"/>. A day after
    /// <paramref name="asOf"/> is not judged yet, and <paramref name="asOf"/> itself is only planned once it is done -
    /// an open "today" is not a miss, as in <see cref="HabitStreak"/>. A completed day that was not due is not counted.
    /// </para>
    /// <para>
    /// A quota counts <b>weeks or months</b>: a period belongs to the range it <i>ends</i> in, so a week across two
    /// months is counted once, in the later one. A period is judged when it is over (its last day is before
    /// <paramref name="asOf"/>) - the running one only once it has already reached its N, like the streak. A period the
    /// habit began in is not judged at all: the habit was not there for all of it, and nothing here prorates. (The
    /// streak differs: it counts that period when it reached its N.)
    /// </para>
    /// </summary>
    public static StatisticsTally Tally(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates,
        DateOnly from, DateOnly to, DateOnly asOf)
        => schedule.IsQuota
            ? TallyPeriods(schedule, startDate, completedDates, from, to, asOf)
            : TallyDays(
                UnitOf(schedule), HabitDueDays.Between(schedule, startDate, from, Horizon(to, asOf)), completedDates, asOf);

    /// <summary>
    /// The same for a sub-habit planned on <paramref name="weekdays"/>: its due days are the habit's due days that fall
    /// on one of those weekdays (<see cref="HabitDueDays.IsDue(HabitSchedule, DateOnly, WeekdaySet, DateOnly)"/>), and
    /// it is always counted in days - also under a quota habit, where every day of the week is a "due" day.
    /// </summary>
    public static StatisticsTally Tally(
        HabitSchedule schedule, DateOnly startDate, WeekdaySet weekdays, IReadOnlySet<DateOnly> completedDates,
        DateOnly from, DateOnly to, DateOnly asOf)
        => TallyDays(
            CountingUnit.Days,
            HabitDueDays.Between(schedule, startDate, weekdays, from, Horizon(to, asOf)),
            completedDates,
            asOf);

    /// <summary>
    /// The latest completed day on or before <paramref name="asOf"/> - over the whole history, not a range - or
    /// <c>null</c> if there is none. A record after <paramref name="asOf"/> is ignored, as everywhere else.
    /// </summary>
    public static DateOnly? LastDone(IEnumerable<DateOnly> completedDates, DateOnly asOf)
    {
        DateOnly? last = null;

        foreach (var day in completedDates)
        {
            if (day <= asOf && (last is null || day > last))
                last = day;
        }

        return last;
    }

    private static DateOnly Horizon(DateOnly to, DateOnly asOf) => to < asOf ? to : asOf;

    private static StatisticsTally TallyDays(
        CountingUnit unit, IReadOnlyList<DateOnly> dueDays, IReadOnlySet<DateOnly> completedDates, DateOnly asOf)
    {
        // The days are already cut off at "today"; of that one only a done day is judged.
        var judged = dueDays.Where(day => day < asOf || completedDates.Contains(day)).ToList();

        return new StatisticsTally(unit, judged.Count, judged.Count(completedDates.Contains));
    }

    private static StatisticsTally TallyPeriods(
        HabitSchedule schedule, DateOnly startDate, IReadOnlySet<DateOnly> completedDates,
        DateOnly from, DateOnly to, DateOnly asOf)
    {
        var times = schedule.Times!.Value;
        var judged = HabitQuota.Between(schedule, startDate, completedDates, from, to, asOf)
            .Where(progress => progress.Period.To <= to && progress.Period.From >= startDate)
            .Where(progress => progress.Period.To < asOf || progress.Done >= times)
            .ToList();

        return new StatisticsTally(
            UnitOf(schedule), judged.Count, judged.Count(progress => progress.Done >= times));
    }
}
