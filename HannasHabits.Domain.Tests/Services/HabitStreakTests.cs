using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Services;

public class HabitStreakTests
{
    // The calendar of the examples (October 2026):
    //   Mon 28 Sep, Tue 29, Wed 30 | Thu 1, Fri 2, Sat 3, Sun 4 | Mon 5, Tue 6, Wed 7, Thu 8, Fri 9, Sat 10, Sun 11 | Mon 12
    // D(8) is "today", D(0) is 30 September.
    private static DateOnly D(int dayOfOctober) => new DateOnly(2026, 10, 1).AddDays(dayOfOctober - 1);

    private static readonly HabitSchedule Daily = HabitSchedule.Daily;
    private static readonly HabitSchedule Weekdays =
        HabitSchedule.Create([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
    private static readonly HabitSchedule Weekend = HabitSchedule.Create([DayOfWeek.Saturday, DayOfWeek.Sunday]);

    private static int Streak(HabitSchedule schedule, DateOnly start, DateOnly asOf, params DateOnly[] done)
        => HabitStreak.Current(schedule, start, done.ToHashSet(), asOf);

    private static DateOnly[] Days(params int[] daysOfOctober) => daysOfOctober.Select(D).ToArray();

    [Fact]
    public void TheCalendarOfTheExamplesIsWhatTheyAssume()
    {
        Assert.Equal(DayOfWeek.Thursday, D(8).DayOfWeek);
        Assert.Equal(DayOfWeek.Wednesday, D(0).DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, D(10).DayOfWeek);
    }

    // ---- a daily habit ----

    [Fact]
    public void NothingCompleted_IsZero()
    {
        Assert.Equal(0, Streak(Daily, D(1), D(8)));
    }

    [Fact]
    public void ADoneAsOfDayCounts()
    {
        Assert.Equal(1, Streak(Daily, D(1), D(8), Days(8)));
        Assert.Equal(3, Streak(Daily, D(1), D(8), Days(8, 7, 6)));
    }

    [Fact]
    public void AnOpenAsOfDayIsSkipped_ItDoesNotBreakTheStreak()
    {
        // Today is not over yet: yesterday's streak is still alive.
        Assert.Equal(3, Streak(Daily, D(1), D(8), Days(7, 6, 5)));
    }

    [Fact]
    public void AMissedYesterday_EndsTheStreak_WhetherOrNotTodayIsDone()
    {
        Assert.Equal(0, Streak(Daily, D(1), D(8), Days(6, 5, 4)));
        Assert.Equal(1, Streak(Daily, D(1), D(8), Days(8, 6, 5, 4)));
    }

    [Fact]
    public void TheFirstMissedScheduledDayEndsTheStreak_OlderDaysDoNotCount()
    {
        Assert.Equal(2, Streak(Daily, D(1), D(8), Days(8, 7, 5, 4, 3)));
    }

    [Fact]
    public void ARecordAfterTheAsOfDayIsIgnored()
    {
        Assert.Equal(0, Streak(Daily, D(1), D(8), Days(9, 10, 11)));
        Assert.Equal(1, Streak(Daily, D(1), D(8), Days(8, 9, 10)));
    }

    [Fact]
    public void AnAsOfDayInThePast_CountsBackFromThere()
    {
        Assert.Equal(5, Streak(Daily, D(1), D(5), Days(1, 2, 3, 4, 5, 6, 7, 8)));
    }

    [Fact]
    public void AnAsOfDayInTheFuture_BehavesLikeAnyOtherOpenDay()
    {
        // The client's date is authoritative. A "today" without a record is open and skipped; one more empty day
        // before it is a miss like any other.
        Assert.Equal(2, Streak(Daily, D(1), D(10), Days(9, 8)));
        Assert.Equal(0, Streak(Daily, D(1), D(11), Days(9, 8)));
    }

    [Fact]
    public void AStreakMayBeVeryLong()
    {
        var start = D(8).AddDays(-1999);
        var done = Enumerable.Range(0, 2000).Select(offset => start.AddDays(offset)).ToArray();

        Assert.Equal(2000, Streak(Daily, start, D(8), done));
    }

    // ---- the start date ----

    [Fact]
    public void TheStartDateEndsTheStreak_RecordsBeforeItAreNotCounted()
    {
        Assert.Equal(3, Streak(Daily, D(6), D(8), Days(5, 4, 6, 7, 8)));
    }

    [Fact]
    public void OnTheStartDateItself_AnOpenDayIsZero_AndADoneDayIsOne()
    {
        Assert.Equal(0, Streak(Daily, D(8), D(8)));
        Assert.Equal(1, Streak(Daily, D(8), D(8), Days(8)));
    }

    [Fact]
    public void BeforeTheStartDate_ThereIsNoStreak()
    {
        Assert.Equal(0, Streak(Daily, D(8), D(5), Days(5, 6, 7, 8)));
    }

    // ---- unscheduled days neither count nor break ----

    [Fact]
    public void AWeekdayHabit_SkipsTheWeekend()
    {
        // Thu 1 and Fri 2 done, Mon 5 still open: the weekend in between is not a miss.
        Assert.Equal(2, Streak(Weekdays, D(1), D(5), Days(2, 1)));
    }

    [Fact]
    public void ARecordOnAnUnscheduledDay_IsIgnored()
    {
        // Sat 3 and Sun 4 are done, but not planned: the streak is still only Thu + Fri.
        Assert.Equal(2, Streak(Weekdays, D(1), D(5), Days(2, 1, 3, 4)));
    }

    [Fact]
    public void ACompletedMondayContinuesTheStreakOfTheFridayBefore()
    {
        Assert.Equal(3, Streak(Weekdays, D(1), D(6), Days(2, 5, 6)));
    }

    [Fact]
    public void AMissedWeekdayInTheMiddle_EndsTheStreak()
    {
        // Tue 6 is missing: Fri + Thu + Wed count, Mon does not.
        Assert.Equal(3, Streak(Weekdays, D(1), D(9), Days(9, 8, 7, 5)));
    }

    [Fact]
    public void AnAsOfDayThatIsNotScheduled_IsSkipped()
    {
        // Saturday: nothing is planned, so the streak is whatever ended on Friday.
        Assert.Equal(2, Streak(Weekdays, D(1), D(10), Days(9, 8)));
        Assert.Equal(2, Streak(Weekdays, D(1), D(10), Days(10, 9, 8)));
    }

    [Fact]
    public void AWeekendHabit_CountsOnlyWeekendDays()
    {
        Assert.Equal(4, Streak(Weekend, D(1), D(11), Days(11, 10, 4, 3)));
    }

    [Fact]
    public void AWeekendHabit_OnAWednesday_CountsTheLastWeekend()
    {
        Assert.Equal(2, Streak(Weekend, D(1), D(7), Days(4, 3)));
    }

    [Fact]
    public void AMissedWeekendDay_EndsTheStreak()
    {
        // Sat 3 is missing.
        Assert.Equal(3, Streak(Weekend, D(1), D(11), Days(11, 10, 4)));
    }

    [Fact]
    public void AOnceAWeekHabit_CountsWeeks()
    {
        var thursdays = HabitSchedule.Create([DayOfWeek.Thursday]);

        Assert.Equal(2, Streak(thursdays, D(1), D(8), Days(8, 1)));
        Assert.Equal(1, Streak(thursdays, D(1), D(8), Days(8)));
        Assert.Equal(1, Streak(thursdays, D(1), D(9), Days(8))); // Friday: nothing planned, Thursday counts
        Assert.Equal(1, Streak(thursdays, D(2), D(8), Days(8, 1))); // Thu 1 lies before the start (Fri 2)
        Assert.Equal(0, Streak(thursdays, D(2), D(9), Days(1)));    // Thu 8 was planned and missed
    }

    // ---- the ends of the calendar ----

    [Fact]
    public void TheLastDayOfTheCalendar_DoesNotOverflow()
    {
        var max = DateOnly.MaxValue;

        Assert.Equal(1, Streak(Daily, max, max, max));
        Assert.Equal(0, Streak(Daily, max, max));
        Assert.Equal(2, Streak(Daily, DateOnly.MinValue, max, max, max.AddDays(-1)));
    }

    [Fact]
    public void TheFirstDayOfTheCalendar_DoesNotUnderflow()
    {
        var min = DateOnly.MinValue;

        Assert.Equal(1, Streak(Daily, min, min, min));
        Assert.Equal(0, Streak(Daily, min, min));
        Assert.Equal(0, Streak(Daily, DateOnly.MaxValue, min, min));
    }

    // ---- against an independent formulation ----

    // The rule built the other way round: collect the scheduled days from the start up to the as-of day (oldest first),
    // then count from the end. No walking backwards, no day numbers - so it does not share a bug with the real thing.
    private static int Reference(HabitSchedule schedule, DateOnly start, ISet<DateOnly> done, DateOnly asOf)
    {
        var scheduled = new List<DateOnly>();
        for (var day = start; day <= asOf; day = day.AddDays(1))
            if (schedule.IncludesDay(day.DayOfWeek))
                scheduled.Add(day);

        var index = scheduled.Count - 1;

        if (index >= 0 && scheduled[index] == asOf && !done.Contains(asOf))
            index--; // an open as-of day is skipped

        var streak = 0;
        while (index >= 0 && done.Contains(scheduled[index]))
        {
            streak++;
            index--;
        }

        return streak;
    }

    [Fact]
    public void AgreesWithTheReferenceOnRandomHabits()
    {
        var random = new Random(20261008); // fixed seed: the same cases on every run
        var allDays = Enum.GetValues<DayOfWeek>();
        double[] densities = [0.3, 0.7, 0.95, 1.0];
        var longStreaks = 0;

        for (var i = 0; i < 20_000; i++)
        {
            var mask = random.Next(1, 128);
            var schedule = HabitSchedule.Create(allDays.Where(day => (mask & (1 << (int)day)) != 0));

            var start = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 200));
            var asOf = start.AddDays(random.Next(-10, 200));
            var density = densities[random.Next(densities.Length)];

            // Records may lie anywhere around the interval: before the start, on unscheduled days, after the as-of day.
            var done = new HashSet<DateOnly>();
            for (var day = start.AddDays(-5); day <= asOf.AddDays(5); day = day.AddDays(1))
                if (random.NextDouble() < density)
                    done.Add(day);

            var expected = Reference(schedule, start, done, asOf);
            var actual = HabitStreak.Current(schedule, start, done, asOf);

            Assert.True(expected == actual,
                $"case {i}: schedule {schedule}, start {start:O}, asOf {asOf:O}: expected {expected}, got {actual}");

            if (expected >= 20)
                longStreaks++;
        }

        // Guard against a test that quietly stopped testing anything: long streaks must actually occur.
        Assert.True(longStreaks > 500, $"only {longStreaks} cases with a streak of 20 or more");
    }
}
