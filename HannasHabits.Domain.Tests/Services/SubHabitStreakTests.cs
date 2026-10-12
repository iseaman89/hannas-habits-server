using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Services;

/// <summary>
/// The streak of a part of a habit: its due days (the habit is due and the weekday is the part's) in a row that were
/// ticked, always counted in days. <see cref="HabitStreak.Current(HabitSchedule, DateOnly, WeekdaySet, IReadOnlySet{DateOnly}, DateOnly)"/>
/// and <see cref="HabitStreak.Longest(HabitSchedule, DateOnly, WeekdaySet, IReadOnlySet{DateOnly}, DateOnly)"/>.
/// </summary>
public class SubHabitStreakTests
{
    // The calendar of HabitStreakTests (October 2026): D(8) is Thursday 8 October. Mondays: 5, 12, 19; Thursdays: 1, 8, 15.
    private static DateOnly D(int dayOfOctober) => new DateOnly(2026, 10, 1).AddDays(dayOfOctober - 1);

    private static readonly HabitSchedule Daily = HabitSchedule.Daily;
    private static readonly WeekdaySet Mondays = WeekdaySet.Of([DayOfWeek.Monday]);
    private static readonly WeekdaySet MondaysAndThursdays = WeekdaySet.Of([DayOfWeek.Monday, DayOfWeek.Thursday]);

    private static int Current(HabitSchedule schedule, DateOnly start, WeekdaySet weekdays, DateOnly asOf, params DateOnly[] done)
        => HabitStreak.Current(schedule, start, weekdays, done.ToHashSet(), asOf);

    private static int Longest(HabitSchedule schedule, DateOnly start, WeekdaySet weekdays, DateOnly asOf, params DateOnly[] done)
        => HabitStreak.Longest(schedule, start, weekdays, done.ToHashSet(), asOf);

    private static DateOnly[] Days(params int[] daysOfOctober) => daysOfOctober.Select(D).ToArray();

    private static readonly DateOnly September1 = new(2026, 9, 1);

    [Fact]
    public void TheCalendarOfTheExamplesIsWhatTheyAssume()
    {
        Assert.Equal(DayOfWeek.Monday, D(5).DayOfWeek);
        Assert.Equal(DayOfWeek.Thursday, D(8).DayOfWeek);
    }

    // ---- the part's own days ----

    [Fact]
    public void OnlyThePartsOwnWeekdaysCount_TheDaysBetweenAreNeitherDueNorAMiss()
    {
        // Mondays 12, 5 and Thursday 8 October done; the other days of the daily habit are not this part's business.
        Assert.Equal(3, Current(Daily, September1, MondaysAndThursdays, D(12), Days(5, 8, 12)));
    }

    [Fact]
    public void AMissedDueDay_EndsTheStreak()
    {
        // Thu 1, Mon 5 | Thu 8 missed | Mon 12.
        Assert.Equal(1, Current(Daily, D(1), MondaysAndThursdays, D(12), Days(1, 5, 12)));
        Assert.Equal(2, Longest(Daily, D(1), MondaysAndThursdays, D(12), Days(1, 5, 12)));
    }

    [Fact]
    public void ATickOnADayThatIsNotThePartsWeekday_IsIgnored()
    {
        // The habit is daily and the part is on Mondays: Tuesday, Wednesday ... ticks are not its days.
        Assert.Equal(1, Current(Daily, September1, Mondays, D(12), Days(6, 7, 8, 12)));
        Assert.Equal(1, Longest(Daily, September1, Mondays, D(12), Days(6, 7, 8, 12)));
    }

    [Fact]
    public void AnOpenDueAsOfDay_IsSkipped_ItDoesNotBreakTheStreak()
    {
        // Thursday 8 is due and still open: the Monday before it still counts.
        Assert.Equal(1, Current(Daily, September1, MondaysAndThursdays, D(8), Days(5)));
        Assert.Equal(1, Longest(Daily, September1, MondaysAndThursdays, D(8), Days(5)));
    }

    [Fact]
    public void ADoneDueAsOfDay_Counts()
    {
        Assert.Equal(2, Current(Daily, September1, MondaysAndThursdays, D(8), Days(5, 8)));
    }

    [Fact]
    public void AnAsOfDayThatIsNotThePartsDay_JudgesTheDaysBefore()
    {
        // Wednesday: nothing is due, so the streak is what ended on Monday.
        Assert.Equal(1, Current(Daily, September1, Mondays, D(7), Days(5)));
    }

    [Fact]
    public void ATickAfterTheAsOfDay_IsIgnored()
    {
        Assert.Equal(1, Current(Daily, September1, MondaysAndThursdays, D(8), Days(5, 12, 15)));
        Assert.Equal(1, Longest(Daily, September1, MondaysAndThursdays, D(8), Days(5, 12, 15)));
    }

    [Fact]
    public void ATickBeforeTheHabitsStart_IsNotCounted()
    {
        // Started Thursday 8: Monday 5 does not exist for the part.
        Assert.Equal(1, Current(Daily, D(8), MondaysAndThursdays, D(8), Days(5, 8)));
        Assert.Equal(1, Longest(Daily, D(8), MondaysAndThursdays, D(8), Days(5, 8)));
    }

    [Fact]
    public void NothingTicked_IsZero()
    {
        Assert.Equal(0, Current(Daily, September1, MondaysAndThursdays, D(12)));
        Assert.Equal(0, Longest(Daily, September1, MondaysAndThursdays, D(12)));
    }

    // ---- the habit's rhythm carries the part along ----

    [Fact]
    public void ThePartIsDueOnlyWhereTheHabitIsDue()
    {
        // A habit on Mondays and Wednesdays; a part on Mondays and Thursdays: only Mondays are due for it.
        var habit = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Wednesday]);

        // Thursday ticks are on a day the habit is not due on: ignored; Mondays 5, 12 are the run.
        Assert.Equal(2, Current(habit, September1, MondaysAndThursdays, D(12), Days(5, 8, 12)));
        Assert.Equal(2, Longest(habit, September1, MondaysAndThursdays, D(12), Days(5, 8, 12)));
    }

    [Fact]
    public void EveryOtherWeekHabit_ThePartsOffWeeksAreNoMiss()
    {
        // Weeks of 5 and 19 October are due (counted from 5 October), 12 October is an off week.
        var fortnightly = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday], everyWeeks: 2, anchor: D(5));

        // The part's Mondays: 5 and 19 are due, 12 is not.
        Assert.Equal(2, Current(fortnightly, September1, Mondays, D(20), Days(5, 19)));
        Assert.Equal(1, Current(fortnightly, September1, Mondays, D(20), Days(19)));
    }

    [Fact]
    public void APartWhoseDaysTheHabitDoesNotHave_HasNoStreak()
    {
        // A "stranded" part after a change of the habit's rhythm: nothing is ever due.
        var habit = HabitSchedule.Weekly([DayOfWeek.Wednesday]);

        Assert.Equal(0, Current(habit, September1, Mondays, D(12), Days(5, 12)));
        Assert.Equal(0, Longest(habit, September1, Mondays, D(12), Days(5, 12)));
    }

    // ---- under a quota habit a part is still counted in days ----

    [Fact]
    public void UnderAQuotaHabit_APartIsCountedInDays_OnItsOwnWeekdays()
    {
        var threeAWeek = HabitSchedule.TimesPerWeek(3);

        // Mondays 5 and 12 and Thursday 8, all ticked - three days in a row, not "weeks".
        Assert.Equal(3, Current(threeAWeek, September1, MondaysAndThursdays, D(12), Days(5, 8, 12)));
        // Thursday 8 missed: Monday 12 on its own.
        Assert.Equal(1, Current(threeAWeek, September1, MondaysAndThursdays, D(12), Days(5, 12)));
        Assert.Equal(1, Longest(threeAWeek, September1, MondaysAndThursdays, D(12), Days(5, 12)));
    }

    // ---- the record ----

    [Fact]
    public void TheLongestRun_CanBeEarlierThanTheCurrentOne()
    {
        // Mon 28 Sep, Thu 1, Mon 5, Thu 8 done (four due days in a row), Mon 12 missed, Thu 15 done.
        var done = new[] { new DateOnly(2026, 9, 28), D(1), D(5), D(8), D(15) };

        Assert.Equal(4, Longest(Daily, September1, MondaysAndThursdays, D(15), done));
        Assert.Equal(1, Current(Daily, September1, MondaysAndThursdays, D(15), done));
    }

    // ---- the ends of the calendar ----

    [Fact]
    public void AtTheEdgesOfTheCalendar_DoesNotOverflow()
    {
        var all = WeekdaySet.Of(Enum.GetValues<DayOfWeek>());

        Assert.Equal(1, Current(Daily, DateOnly.MaxValue, all, DateOnly.MaxValue, DateOnly.MaxValue));
        Assert.Equal(1, Longest(Daily, DateOnly.MaxValue, all, DateOnly.MaxValue, DateOnly.MaxValue));
        Assert.Equal(1, Current(Daily, DateOnly.MinValue, all, DateOnly.MinValue, DateOnly.MinValue));
        Assert.Equal(1, Longest(Daily, DateOnly.MinValue, all, DateOnly.MinValue, DateOnly.MinValue));
    }

    // ---- against an independent formulation ----

    // The due days written down by their definition ("the habit's rule says so, the weekday is the part's, the habit
    // has started"), oldest first, from the start up to the as-of day - not through HabitDueDays.
    private static List<DateOnly> DueDays(HabitSchedule schedule, DateOnly start, WeekdaySet weekdays, DateOnly asOf)
    {
        var due = new List<DateOnly>();

        for (var day = start; day <= asOf; day = day.AddDays(1))
            if (schedule.IsScheduledOn(day) && weekdays.Contains(day.DayOfWeek))
                due.Add(day);

        return due;
    }

    private static int CurrentReference(
        HabitSchedule schedule, DateOnly start, WeekdaySet weekdays, ISet<DateOnly> done, DateOnly asOf)
    {
        var due = DueDays(schedule, start, weekdays, asOf);
        var index = due.Count - 1;

        if (index >= 0 && due[index] == asOf && !done.Contains(asOf))
            index--; // an open as-of day is skipped

        var streak = 0;
        while (index >= 0 && done.Contains(due[index]))
        {
            streak++;
            index--;
        }

        return streak;
    }

    private static int LongestReference(
        HabitSchedule schedule, DateOnly start, WeekdaySet weekdays, ISet<DateOnly> done, DateOnly asOf)
    {
        var marks = string.Concat(DueDays(schedule, start, weekdays, asOf).Select(day => done.Contains(day) ? 'x' : '.'));

        return marks.Split('.').Max(run => run.Length);
    }

    private static HabitSchedule RandomSchedule(Random random)
    {
        var allDays = Enum.GetValues<DayOfWeek>();
        var anchor = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 120));

        switch (random.Next(5))
        {
            case 0:
                return HabitSchedule.Daily;
            case 1:
                var mask = random.Next(1, 128);
                return HabitSchedule.Weekly(allDays.Where(day => (mask & (1 << (int)day)) != 0));
            case 2:
                return HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday], everyWeeks: random.Next(2, 4), anchor: anchor);
            case 3:
                return HabitSchedule.EveryNDays(random.Next(HabitSchedule.MinDayInterval, 5), anchor);
            default:
                // A quota habit: every day is "due", the part keeps to its weekdays.
                return random.Next(2) == 0 ? HabitSchedule.TimesPerWeek(random.Next(1, 8)) : HabitSchedule.TimesPerMonth(random.Next(1, 29));
        }
    }

    [Fact]
    public void AgreesWithTheReferenceOnRandomParts()
    {
        var random = new Random(20261014); // fixed seed: the same cases on every run
        double[] densities = [0.3, 0.7, 0.9, 0.98];
        var streaks = 0;
        var records = 0;

        for (var i = 0; i < 20_000; i++)
        {
            var schedule = RandomSchedule(random);
            var mask = random.Next(1, 128);
            var weekdays = WeekdaySet.FromMask(mask);
            var start = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 200));
            var asOf = start.AddDays(random.Next(-10, 260));
            var density = densities[random.Next(densities.Length)];

            // Ticks may lie anywhere around the interval: before the start, on days that are not due, after the as-of day.
            var done = new HashSet<DateOnly>();
            for (var day = start.AddDays(-5); day <= asOf.AddDays(5); day = day.AddDays(1))
                if (random.NextDouble() < density)
                    done.Add(day);

            var expectedCurrent = CurrentReference(schedule, start, weekdays, done, asOf);
            var expectedLongest = LongestReference(schedule, start, weekdays, done, asOf);
            var current = HabitStreak.Current(schedule, start, weekdays, done, asOf);
            var longest = HabitStreak.Longest(schedule, start, weekdays, done, asOf);

            Assert.True(expectedCurrent == current,
                $"case {i}: {schedule}, weekdays {mask}, start {start:O}, asOf {asOf:O}: current expected {expectedCurrent}, got {current}");
            Assert.True(expectedLongest == longest,
                $"case {i}: {schedule}, weekdays {mask}, start {start:O}, asOf {asOf:O}: longest expected {expectedLongest}, got {longest}");
            Assert.True(longest >= current, $"case {i}: the record {longest} is shorter than the streak {current}");

            if (current >= 5)
                streaks++;
            if (longest > current)
                records++;
        }

        // Guard against a test that quietly stopped testing anything.
        Assert.True(streaks > 1_000, $"only {streaks} cases with a streak of 5 or more");
        Assert.True(records > 1_000, $"only {records} cases where the record is longer than the streak");
    }
}
