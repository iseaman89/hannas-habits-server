using HannasHabits.Domain.Enums;
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
        HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
    private static readonly HabitSchedule Weekend = HabitSchedule.Weekly([DayOfWeek.Saturday, DayOfWeek.Sunday]);

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
        var thursdays = HabitSchedule.Weekly([DayOfWeek.Thursday]);

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

    // ---- the other rules ----

    [Fact]
    public void EveryOtherWeek_OnlyTheDueWeeksCount_AMissedOffWeekIsNotAMiss()
    {
        // Mondays of October 2026: 5, 12, 19, 26. Every second week from the week of 5 October: due on 5 and 19.
        var fortnightly = HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 2, anchor: D(5));
        var done = new[] { new DateOnly(2026, 9, 21), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 19) };

        // asOf Tuesday 20 October: the Monday 19th and, two weeks before, the 5th and 21 September - nothing was due in between.
        Assert.Equal(3, Streak(fortnightly, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 20), done));
        // A missed due Monday ends it.
        Assert.Equal(1, Streak(fortnightly, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void EveryThreeDays_CountsOnlyEveryThirdDay()
    {
        var everyThree = HabitSchedule.EveryNDays(3, D(8));

        // Due: 8, 5, 2, ... Done on the due days only.
        Assert.Equal(3, Streak(everyThree, D(1), D(8), Days(8, 5, 2)));
        // Records on the days between are ignored.
        Assert.Equal(3, Streak(everyThree, D(1), D(8), Days(8, 7, 6, 5, 4, 3, 2)));
        // Missing the 5th ends it.
        Assert.Equal(1, Streak(everyThree, D(1), D(8), Days(8, 2)));
        // Today (8th) is due but still open: it is skipped, not a break.
        Assert.Equal(2, Streak(everyThree, D(1), D(8), Days(5, 2)));
    }

    [Fact]
    public void MonthlyOnDate_ACompletedStreakReachesBackAcrossMonths()
    {
        var onThe15th = HabitSchedule.MonthlyOnDate(15);
        var done = new[] { new DateOnly(2026, 8, 15), new DateOnly(2026, 9, 15), new DateOnly(2026, 10, 15) };

        Assert.Equal(3, HabitStreak.Current(onThe15th, new DateOnly(2026, 7, 1), done.ToHashSet(), new DateOnly(2026, 10, 20)));
        // On the 12th this month's 15th has not come yet (its record is in the future and ignored): the two months before count.
        Assert.Equal(2, HabitStreak.Current(onThe15th, new DateOnly(2026, 7, 1), done.ToHashSet(), new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void MonthlyOnTheLastSaturday_TheStreakBreaksOnAMissedMonth()
    {
        var lastSaturday = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Saturday);
        // Last Saturdays: 25 July, 29 August, 26 September, 31 October 2026.
        var done = new[] { new DateOnly(2026, 7, 25), new DateOnly(2026, 9, 26) };

        Assert.Equal(1, HabitStreak.Current(lastSaturday, new DateOnly(2026, 7, 1), done.ToHashSet(), new DateOnly(2026, 10, 10)));

        var all = done.Append(new DateOnly(2026, 8, 29)).ToHashSet();
        Assert.Equal(3, HabitStreak.Current(lastSaturday, new DateOnly(2026, 7, 1), all, new DateOnly(2026, 10, 10)));
    }

    // ---- a quota: N times a week / month - the streak counts weeks and months ----

    // Weeks of the examples: 28 Sep - 4 Oct, 5 - 11 Oct, 12 - 18 Oct. D(8) is Thursday 8 October, the running week is
    // 5 - 11 October, the one before it is D(-2)..D(4).
    private static readonly HabitSchedule ThreeAWeek = HabitSchedule.TimesPerWeek(3);
    private static readonly HabitSchedule TwoAMonth = HabitSchedule.TimesPerMonth(2);
    private static readonly DateOnly September1 = new(2026, 9, 1);

    [Fact]
    public void AQuota_AWeekThatReachedItsCount_IsOne()
    {
        Assert.Equal(1, Streak(ThreeAWeek, September1, D(8), Days(5, 6, 8)));
    }

    [Fact]
    public void AQuota_TheRunningWeekThatFellShort_IsSkipped_ItDoesNotBreakTheStreak()
    {
        // 2 of 3 so far - there is still Friday to Sunday. Last week had 3.
        Assert.Equal(1, Streak(ThreeAWeek, September1, D(8), Days(5, 6, 0, 1, 2)));
    }

    [Fact]
    public void AQuota_ARunningWeekThatReachedItsCount_AddsToThePreviousOnes()
    {
        Assert.Equal(2, Streak(ThreeAWeek, September1, D(8), Days(5, 6, 8, 0, 1, 2)));
    }

    [Fact]
    public void AQuota_AWeekThatFellShort_EndsTheStreak_OlderWeeksDoNotCount()
    {
        // Last week 2 of 3; the week before it was complete (21 - 27 September).
        var done = Days(5, 6, 8, 0, 1).Concat([new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23)]);

        Assert.Equal(1, Streak(ThreeAWeek, September1, D(8), done.ToArray()));
    }

    [Fact]
    public void AQuota_NothingDone_IsZero()
    {
        Assert.Equal(0, Streak(ThreeAWeek, September1, D(8)));
    }

    [Fact]
    public void AQuota_AMoreThanNeededWeek_CountsOnce()
    {
        Assert.Equal(1, Streak(ThreeAWeek, September1, D(8), Days(5, 6, 7, 8)));
    }

    [Fact]
    public void AQuota_TheDaysOfTheWeekDoNotMatter_OnlyHowMany()
    {
        // Monday, Wednesday, Sunday of last week - and Tuesday, Friday, Saturday the week before.
        var done = new[]
        {
            D(-2), D(0), D(4),
            new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 26)
        };

        Assert.Equal(2, Streak(ThreeAWeek, September1, D(5), done));
    }

    [Fact]
    public void AQuota_AMarkAfterTheAsOfDay_IsIgnored()
    {
        // Three marks, but two of them lie after Thursday: the running week has 1 so far.
        Assert.Equal(0, Streak(ThreeAWeek, September1, D(8), Days(8, 9, 10)));
        Assert.Equal(1, Streak(ThreeAWeek, September1, D(11), Days(8, 9, 10)));
    }

    [Fact]
    public void AQuota_TheWeekIsMondayToSunday_SoSundayBelongsToTheWeekBeforeIt()
    {
        // Fri 2, Sat 3, Sun 4 October are one week; Monday 5 starts the next.
        Assert.Equal(1, Streak(ThreeAWeek, September1, D(5), Days(2, 3, 4)));
        Assert.Equal(0, Streak(ThreeAWeek, September1, D(5), Days(3, 4, 5)));
    }

    [Fact]
    public void AQuota_AWeekThatSpansTwoMonths_IsOneWeek()
    {
        // 28 and 29 September and 1 October are in the same week.
        var done = new[] { new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 29), D(1) };

        Assert.Equal(1, Streak(ThreeAWeek, September1, D(4), done));
    }

    [Fact]
    public void AQuota_TheWeekOfTheStartDate_CountsOnlyTheDaysFromTheStart_AndWhenItReachedItsCountItIsOne()
    {
        // Started on Wednesday 7 October with 3 a week: Wed, Thu and Fri are enough for the first week.
        Assert.Equal(1, Streak(ThreeAWeek, D(7), D(9), Days(7, 8, 9)));

        // Marks of Monday and Tuesday before the start do not count towards it.
        Assert.Equal(0, Streak(ThreeAWeek, D(7), D(9), Days(5, 6, 7)));
    }

    [Fact]
    public void AQuota_ThePartialFirstWeekThatFellShort_IsNoBreak_ItIsJustTheOldestWeek()
    {
        // The first (partial) week 5 - 11 October had 1; the weeks after it are complete.
        var done = Days(7, 12, 13, 14, 19, 20, 21);

        Assert.Equal(2, HabitStreak.Current(ThreeAWeek, D(7), done.ToHashSet(), D(21)));
    }

    [Fact]
    public void AQuota_BeforeTheStartDate_ThereIsNoStreak()
    {
        Assert.Equal(0, Streak(ThreeAWeek, D(20), D(8), Days(5, 6, 8)));
    }

    [Fact]
    public void AQuota_AStreakMayBeVeryLong()
    {
        var start = new DateOnly(2026, 1, 5); // a Monday
        var done = new HashSet<DateOnly>();
        for (var week = 0; week < 40; week++)
            for (var day = 0; day < 3; day++)
                done.Add(start.AddDays(week * 7 + day));

        // 40 complete weeks end on Sunday 11 October 2026.
        Assert.Equal(40, HabitStreak.Current(ThreeAWeek, start, done, new DateOnly(2026, 10, 11)));
    }

    [Fact]
    public void AQuota_PerMonth_CountsCalendarMonths()
    {
        // October has 2 (the running month reached its count), September 2, August 1.
        var done = new[]
        {
            D(2), D(3),
            new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 20),
            new DateOnly(2026, 8, 1)
        };

        Assert.Equal(2, Streak(TwoAMonth, new DateOnly(2026, 7, 1), D(8), done));
    }

    [Fact]
    public void AQuota_PerMonth_TheRunningMonthThatFellShort_IsSkipped()
    {
        var done = new[] { D(2), new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 20) };

        Assert.Equal(1, Streak(TwoAMonth, new DateOnly(2026, 7, 1), D(8), done));
    }

    [Fact]
    public void AQuota_PerMonth_AMonthThatFellShort_EndsTheStreak()
    {
        var done = new[]
        {
            D(2), D(3), // October
            new DateOnly(2026, 9, 5), // September: 1 of 2
            new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 6)
        };

        Assert.Equal(1, Streak(TwoAMonth, new DateOnly(2026, 7, 1), D(8), done));
    }

    [Fact]
    public void AQuota_AtTheEdgesOfTheCalendar_DoesNotOverflow()
    {
        Assert.Equal(1, HabitStreak.Current(
            HabitSchedule.TimesPerWeek(1), DateOnly.MinValue, new[] { DateOnly.MinValue }.ToHashSet(), DateOnly.MinValue));
        Assert.Equal(1, HabitStreak.Current(
            HabitSchedule.TimesPerWeek(1), new DateOnly(9999, 12, 1), new[] { DateOnly.MaxValue }.ToHashSet(), DateOnly.MaxValue));
        Assert.Equal(1, HabitStreak.Current(
            HabitSchedule.TimesPerMonth(1), DateOnly.MinValue, new[] { DateOnly.MinValue }.ToHashSet(), DateOnly.MinValue));
    }

    // ---- against an independent formulation ----

    // The rule built the other way round: collect the scheduled days from the start up to the as-of day (oldest first),
    // then count from the end. No walking backwards, no day numbers - so it does not share a bug with the real thing.
    private static int Reference(HabitSchedule schedule, DateOnly start, ISet<DateOnly> done, DateOnly asOf)
    {
        var scheduled = new List<DateOnly>();
        for (var day = start; day <= asOf; day = day.AddDays(1))
            if (schedule.IsScheduledOn(day))
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
            var schedule = HabitSchedule.Weekly(allDays.Where(day => (mask & (1 << (int)day)) != 0));

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

    // The quota rule built the other way round: bucket the days from the start to the as-of day by ISO week (or by
    // year and month), count the completed days per bucket, then count the buckets that reached it from the newest.
    // The ISO calendar and a bucket list share nothing with the day-number arithmetic of CalendarPeriod.
    private static int QuotaReference(HabitSchedule schedule, DateOnly start, ISet<DateOnly> done, DateOnly asOf)
    {
        if (asOf < start)
            return 0;

        static (int, int) WeekKey(DateOnly day)
        {
            var date = day.ToDateTime(TimeOnly.MinValue);
            return (System.Globalization.ISOWeek.GetYear(date), System.Globalization.ISOWeek.GetWeekOfYear(date));
        }

        static (int, int) MonthKey(DateOnly day) => (day.Year, day.Month);

        Func<DateOnly, (int, int)> key = schedule.Kind == ScheduleKind.TimesPerWeek ? WeekKey : MonthKey;
        var buckets = new List<((int, int) Key, int Count)>();

        for (var day = start; day <= asOf; day = day.AddDays(1))
        {
            if (buckets.Count == 0 || buckets[^1].Key != key(day))
                buckets.Add((key(day), 0));

            if (done.Contains(day))
                buckets[^1] = (buckets[^1].Key, buckets[^1].Count + 1);
        }

        var times = schedule.Times!.Value;
        var index = buckets.Count - 1;

        if (buckets[index].Count < times)
            index--; // the running period is skipped, not a break

        var streak = 0;
        while (index >= 0 && buckets[index].Count >= times)
        {
            streak++;
            index--;
        }

        return streak;
    }

    [Fact]
    public void AQuota_AgreesWithTheReferenceOnRandomHabits()
    {
        var random = new Random(20261011); // fixed seed: the same cases on every run
        double[] densities = [0.15, 0.4, 0.7, 0.95];
        var longStreaks = 0;
        var nonZero = 0;

        for (var i = 0; i < 20_000; i++)
        {
            var perWeek = random.Next(2) == 0;
            var schedule = perWeek
                ? HabitSchedule.TimesPerWeek(random.Next(1, 8))
                : HabitSchedule.TimesPerMonth(random.Next(1, 29));

            var start = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 200));
            var asOf = start.AddDays(random.Next(-10, 420));
            var density = densities[random.Next(densities.Length)];

            // Records may lie anywhere around the interval: before the start and after the as-of day too.
            var done = new HashSet<DateOnly>();
            for (var day = start.AddDays(-5); day <= asOf.AddDays(5); day = day.AddDays(1))
                if (random.NextDouble() < density)
                    done.Add(day);

            var expected = QuotaReference(schedule, start, done, asOf);
            var actual = HabitStreak.Current(schedule, start, done, asOf);

            Assert.True(expected == actual,
                $"case {i}: schedule {schedule}, start {start:O}, asOf {asOf:O}: expected {expected}, got {actual}");

            if (expected > 0)
                nonZero++;
            if (expected >= 5)
                longStreaks++;
        }

        // Guard against a test that quietly stopped testing anything: streaks of both kinds must actually occur.
        Assert.True(nonZero > 4_000, $"only {nonZero} cases with a streak");
        Assert.True(longStreaks > 500, $"only {longStreaks} cases with a streak of 5 or more");
    }
}
