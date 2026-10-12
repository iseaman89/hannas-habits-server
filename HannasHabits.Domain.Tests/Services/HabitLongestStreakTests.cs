using System.Globalization;
using System.Text;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Services;

/// <summary>
/// <see cref="HabitStreak.Longest(HabitSchedule, DateOnly, IReadOnlySet{DateOnly}, DateOnly)"/>: the best run of a
/// habit's whole history - the same runs as the current streak, so it is never shorter than it.
/// </summary>
public class HabitLongestStreakTests
{
    // The calendar of HabitStreakTests (October 2026): D(8) is Thursday 8 October, D(0) is Wednesday 30 September,
    // D(5) is Monday 5 October, D(10) is Saturday 10 October.
    private static DateOnly D(int dayOfOctober) => new DateOnly(2026, 10, 1).AddDays(dayOfOctober - 1);

    private static readonly HabitSchedule Daily = HabitSchedule.Daily;
    private static readonly HabitSchedule Weekdays =
        HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

    private static int Longest(HabitSchedule schedule, DateOnly start, DateOnly asOf, params DateOnly[] done)
        => HabitStreak.Longest(schedule, start, done.ToHashSet(), asOf);

    private static int Current(HabitSchedule schedule, DateOnly start, DateOnly asOf, params DateOnly[] done)
        => HabitStreak.Current(schedule, start, done.ToHashSet(), asOf);

    private static DateOnly[] Days(params int[] daysOfOctober) => daysOfOctober.Select(D).ToArray();

    // ---- a daily habit ----

    [Fact]
    public void NothingCompleted_IsZero()
    {
        Assert.Equal(0, Longest(Daily, D(1), D(8)));
    }

    [Fact]
    public void ASingleDay_IsOne()
    {
        Assert.Equal(1, Longest(Daily, D(1), D(8), Days(3)));
    }

    [Fact]
    public void AnEarlierRunThatWasLongerThanTheCurrentOne_IsTheLongest()
    {
        // 1-4 (four days), 5 missed, 6-8 (three days, the current streak).
        var done = Days(1, 2, 3, 4, 6, 7, 8);

        Assert.Equal(4, Longest(Daily, D(1), D(8), done));
        Assert.Equal(3, Current(Daily, D(1), D(8), done));
    }

    [Fact]
    public void TheCurrentRun_CanBeTheLongest()
    {
        Assert.Equal(6, Longest(Daily, D(1), D(8), Days(1, 3, 4, 5, 6, 7, 8)));
    }

    [Fact]
    public void ARunThatEndedLongAgo_StaysTheLongestForever()
    {
        // Three days in 1-3, nothing since: today's streak is 0, the record is not forgotten.
        var done = Days(1, 2, 3);

        Assert.Equal(3, Longest(Daily, D(1), D(8), done));
        Assert.Equal(0, Current(Daily, D(1), D(8), done));
    }

    [Fact]
    public void TwoRunsOfTheSameLength_AreOneRecord()
    {
        Assert.Equal(2, Longest(Daily, D(1), D(8), Days(1, 2, 4, 5)));
    }

    [Fact]
    public void AnOpenAsOfDay_IsNotABreak_AndNotACount()
    {
        // Days 1-7 done, today (8) still open: the streak is 7 and so is the record.
        Assert.Equal(7, Longest(Daily, D(1), D(8), Days(1, 2, 3, 4, 5, 6, 7)));
    }

    [Fact]
    public void ADoneAsOfDay_Counts()
    {
        Assert.Equal(8, Longest(Daily, D(1), D(8), Days(1, 2, 3, 4, 5, 6, 7, 8)));
    }

    [Fact]
    public void ARecordAfterTheAsOfDay_IsIgnored()
    {
        Assert.Equal(2, Longest(Daily, D(1), D(8), Days(7, 8, 9, 10, 11)));
    }

    [Fact]
    public void AnAsOfDayInThePast_JudgesOnlyUpToThere()
    {
        Assert.Equal(3, Longest(Daily, D(1), D(3), Days(1, 2, 3, 4, 5, 6, 7, 8)));
    }

    [Fact]
    public void ARecordBeforeTheStartDate_IsNotCounted()
    {
        Assert.Equal(3, Longest(Daily, D(5), D(8), Days(1, 2, 3, 4, 5, 6, 7)));
    }

    [Fact]
    public void BeforeTheStartDate_ThereIsNoStreak()
    {
        Assert.Equal(0, Longest(Daily, D(8), D(5), Days(5, 6, 7, 8)));
    }

    [Fact]
    public void ALongHistory_IsWalkedWithoutTrouble()
    {
        var start = D(8).AddDays(-2999);
        // Three runs: 1,000 days, a gap, 1,500 days, a gap, 498 days up to today.
        var done = Enumerable.Range(0, 1000)
            .Concat(Enumerable.Range(1001, 1500))
            .Concat(Enumerable.Range(2502, 498))
            .Select(offset => start.AddDays(offset))
            .ToArray();

        Assert.Equal(1500, Longest(Daily, start, D(8), done));
    }

    // ---- unscheduled days neither count nor break ----

    [Fact]
    public void AWeekdayHabit_TheWeekendIsNotABreak()
    {
        // Thu 1, Fri 2, Mon 5, Tue 6: one run of four across the weekend.
        Assert.Equal(4, Longest(Weekdays, D(1), D(7), Days(1, 2, 5, 6)));
    }

    [Fact]
    public void ARecordOnAnUnscheduledDay_IsIgnored()
    {
        // Sat 3 and Sun 4 are done but not planned: still the four weekdays, not six.
        Assert.Equal(4, Longest(Weekdays, D(1), D(7), Days(1, 2, 3, 4, 5, 6)));
    }

    [Fact]
    public void AMissedScheduledDay_EndsTheRun()
    {
        // Thu 1, Fri 2 | Mon 5 missed | Tue 6, Wed 7.
        Assert.Equal(2, Longest(Weekdays, D(1), D(9), Days(1, 2, 6, 7)));
    }

    [Fact]
    public void EveryOtherWeek_OnlyTheDueWeeksCount_AMissedOffWeekIsNotAMiss()
    {
        // Mondays 7, 21 September and 5, 19 October are due (every second week from 5 October); 14 September and 12
        // October are off weeks.
        var fortnightly = HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 2, anchor: D(5));
        var done = new[] { new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 21), D(5), D(19) };

        Assert.Equal(4, HabitStreak.Longest(fortnightly, new DateOnly(2026, 9, 1), done.ToHashSet(), D(20)));
    }

    [Fact]
    public void EveryThreeDays_IgnoresTheDaysBetween()
    {
        var everyThree = HabitSchedule.EveryNDays(3, D(8));

        // Due: 2, 5, 8 (and 29 September, 26 ...). Done on the due days and on the days between.
        Assert.Equal(3, Longest(everyThree, D(1), D(8), Days(2, 3, 4, 5, 6, 7, 8)));
        Assert.Equal(1, Longest(everyThree, D(1), D(8), Days(2, 8)));
    }

    [Fact]
    public void MonthlyOnTheLastSaturday_OneADayPerMonth_AMissedMonthEndsTheRun()
    {
        var lastSaturday = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Saturday);
        // Last Saturdays: 25 July, 29 August, 26 September, 31 October 2026.
        var done = new[] { new DateOnly(2026, 7, 25), new DateOnly(2026, 8, 29), new DateOnly(2026, 10, 31) };

        Assert.Equal(2, HabitStreak.Longest(lastSaturday, new DateOnly(2026, 7, 1), done.ToHashSet(), new DateOnly(2026, 10, 31)));
    }

    // ---- the ends of the calendar ----

    [Fact]
    public void TheLastDayOfTheCalendar_DoesNotOverflow()
    {
        var max = DateOnly.MaxValue;

        Assert.Equal(1, Longest(Daily, max, max, max));
        Assert.Equal(0, Longest(Daily, max, max));
        Assert.Equal(2, Longest(Daily, DateOnly.MinValue, max, max, max.AddDays(-1)));
    }

    [Fact]
    public void TheFirstDayOfTheCalendar_DoesNotUnderflow()
    {
        var min = DateOnly.MinValue;

        Assert.Equal(1, Longest(Daily, min, min, min));
        Assert.Equal(0, Longest(Daily, min, min));
        Assert.Equal(0, Longest(Daily, DateOnly.MaxValue, min, min));
    }

    // ---- a quota: N times a week / month - the record counts weeks and months ----

    // Weeks of the examples: 28 Sep - 4 Oct, 5 - 11 Oct, 12 - 18 Oct; the running week of D(8) is 5 - 11 October.
    private static readonly HabitSchedule ThreeAWeek = HabitSchedule.TimesPerWeek(3);
    private static readonly HabitSchedule TwoAMonth = HabitSchedule.TimesPerMonth(2);
    private static readonly DateOnly September1 = new(2026, 9, 1);

    private static IEnumerable<DateOnly> Run(DateOnly first, int count) =>
        Enumerable.Range(0, count).Select(first.AddDays);

    [Fact]
    public void AQuota_ConsecutiveMetWeeks_AreOneRun()
    {
        // 14 - 20 and 21 - 27 September and the week of 28 September: three marks each.
        var done = Run(new DateOnly(2026, 9, 14), 3)
            .Concat(Run(new DateOnly(2026, 9, 21), 3))
            .Concat(Run(new DateOnly(2026, 9, 28), 3))
            .ToArray();

        Assert.Equal(3, Longest(ThreeAWeek, September1, D(8), done));
    }

    [Fact]
    public void AQuota_AWeekThatFellShort_EndsTheRun_AnEarlierRunStaysTheRecord()
    {
        // Two met weeks (14 and 21 September), a short one (28 September, two marks), the running one met (5 October).
        var done = Run(new DateOnly(2026, 9, 14), 3)
            .Concat(Run(new DateOnly(2026, 9, 21), 3))
            .Concat(Run(new DateOnly(2026, 9, 28), 2))
            .Concat(Days(5, 6, 7))
            .ToArray();

        Assert.Equal(2, Longest(ThreeAWeek, September1, D(8), done));
        Assert.Equal(1, Current(ThreeAWeek, September1, D(8), done));
    }

    [Fact]
    public void AQuota_TheRunningWeekThatFellShort_IsNotABreak_AndNotACount()
    {
        // The week of 28 September is met; the running week (5 - 11) has 2 of 3 so far.
        var done = Run(new DateOnly(2026, 9, 28), 3).Concat(Days(5, 6)).ToArray();

        Assert.Equal(1, Longest(ThreeAWeek, September1, D(8), done));
    }

    [Fact]
    public void AQuota_AMoreThanNeededWeek_CountsOnce()
    {
        Assert.Equal(1, Longest(ThreeAWeek, September1, D(8), Days(5, 6, 7, 8)));
    }

    [Fact]
    public void AQuota_TheWeekOfTheStartDate_CountsOnlyFromTheStart()
    {
        // Started Wednesday 7 October: Wed, Thu, Fri reach 3; Monday and Tuesday before it do not count.
        Assert.Equal(1, Longest(ThreeAWeek, D(7), D(9), Days(7, 8, 9)));
        Assert.Equal(0, Longest(ThreeAWeek, D(7), D(9), Days(5, 6, 7)));
    }

    [Fact]
    public void AQuota_AMarkAfterTheAsOfDay_IsIgnored()
    {
        Assert.Equal(0, Longest(ThreeAWeek, September1, D(8), Days(8, 9, 10)));
        Assert.Equal(1, Longest(ThreeAWeek, September1, D(11), Days(8, 9, 10)));
    }

    [Fact]
    public void AQuota_BeforeTheStartDate_ThereIsNoStreak()
    {
        Assert.Equal(0, Longest(ThreeAWeek, D(20), D(8), Days(5, 6, 8)));
    }

    [Fact]
    public void AQuota_PerMonth_CountsCalendarMonths()
    {
        // August 2, September 1 (short), October 2 (the running month reached it).
        var done = new[]
        {
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2),
            new DateOnly(2026, 9, 5),
            D(2), D(3)
        };

        Assert.Equal(1, Longest(TwoAMonth, new DateOnly(2026, 7, 1), D(8), done));

        var withSeptember = done.Append(new DateOnly(2026, 9, 20)).ToArray();
        Assert.Equal(3, Longest(TwoAMonth, new DateOnly(2026, 7, 1), D(8), withSeptember));
    }

    [Fact]
    public void AQuota_AtTheEdgesOfTheCalendar_DoesNotOverflow()
    {
        Assert.Equal(1, HabitStreak.Longest(
            HabitSchedule.TimesPerWeek(1), DateOnly.MinValue, new[] { DateOnly.MinValue }.ToHashSet(), DateOnly.MinValue));
        Assert.Equal(1, HabitStreak.Longest(
            HabitSchedule.TimesPerWeek(1), new DateOnly(9999, 12, 1), new[] { DateOnly.MaxValue }.ToHashSet(), DateOnly.MaxValue));
        Assert.Equal(1, HabitStreak.Longest(
            HabitSchedule.TimesPerMonth(1), DateOnly.MinValue, new[] { DateOnly.MinValue }.ToHashSet(), DateOnly.MinValue));
    }

    // ---- against an independent formulation ----

    // The record built the other way round: write the judged days (or weeks / months) from the start up to the as-of
    // day as a string - 'x' met, '.' not met - and take the longest run of 'x'. No day-number arithmetic, no walk
    // from the first completed day, no running counter: nothing it could share a bug with.
    private static int LongestRun(string marks) => marks.Split('.').Max(run => run.Length);

    private static string DayMarks(HabitSchedule schedule, DateOnly start, ISet<DateOnly> done, DateOnly asOf)
    {
        var marks = new StringBuilder();

        for (var day = start; day <= asOf; day = day.AddDays(1))
            if (schedule.IsScheduledOn(day))
                marks.Append(done.Contains(day) ? 'x' : '.');

        return marks.ToString();
    }

    private static string PeriodMarks(HabitSchedule schedule, DateOnly start, ISet<DateOnly> done, DateOnly asOf)
    {
        static (int, int) WeekKey(DateOnly day)
        {
            var date = day.ToDateTime(TimeOnly.MinValue);
            return (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
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

        return string.Concat(buckets.Select(bucket => bucket.Count >= times ? 'x' : '.'));
    }

    private static HabitSchedule RandomSchedule(Random random)
    {
        var allDays = Enum.GetValues<DayOfWeek>();
        var occurrences = Enum.GetValues<WeekdayOccurrence>();
        var anchor = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 120));

        switch (random.Next(5))
        {
            case 0:
                var mask = random.Next(1, 128);
                return HabitSchedule.Weekly(allDays.Where(day => (mask & (1 << (int)day)) != 0));
            case 1:
                return HabitSchedule.Weekly(
                    [allDays[random.Next(allDays.Length)], allDays[random.Next(allDays.Length)]],
                    everyWeeks: random.Next(2, 5), anchor: anchor);
            case 2:
                return HabitSchedule.EveryNDays(random.Next(HabitSchedule.MinDayInterval, 6), anchor);
            case 3:
                return HabitSchedule.MonthlyOnDate(random.Next(1, HabitSchedule.MaxDayOfMonth + 1));
            default:
                return HabitSchedule.MonthlyOnWeekday(
                    occurrences[random.Next(occurrences.Length)], allDays[random.Next(allDays.Length)]);
        }
    }

    [Fact]
    public void AgreesWithTheReferenceOnRandomHabits_AndIsNeverShorterThanTheCurrentStreak()
    {
        var random = new Random(20261012); // fixed seed: the same cases on every run
        double[] densities = [0.3, 0.7, 0.9, 0.98];
        var longer = 0;

        for (var i = 0; i < 20_000; i++)
        {
            var schedule = RandomSchedule(random);
            var start = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 200));
            var asOf = start.AddDays(random.Next(-10, 260));
            var density = densities[random.Next(densities.Length)];

            // Records may lie anywhere around the interval: before the start, on unscheduled days, after the as-of day.
            var done = new HashSet<DateOnly>();
            for (var day = start.AddDays(-5); day <= asOf.AddDays(5); day = day.AddDays(1))
                if (random.NextDouble() < density)
                    done.Add(day);

            var expected = LongestRun(DayMarks(schedule, start, done, asOf));
            var actual = HabitStreak.Longest(schedule, start, done, asOf);
            var current = HabitStreak.Current(schedule, start, done, asOf);

            Assert.True(expected == actual,
                $"case {i}: schedule {schedule}, start {start:O}, asOf {asOf:O}: expected {expected}, got {actual}");
            Assert.True(actual >= current, $"case {i}: the record {actual} is shorter than the streak {current}");

            if (actual > current)
                longer++;
        }

        // Guard against a test that quietly stopped testing anything: records that beat the current streak must occur.
        Assert.True(longer > 2_000, $"only {longer} cases where the record is longer than the streak");
    }

    [Fact]
    public void AQuota_AgreesWithTheReferenceOnRandomHabits_AndIsNeverShorterThanTheCurrentStreak()
    {
        var random = new Random(20261013); // fixed seed: the same cases on every run
        double[] densities = [0.15, 0.4, 0.7, 0.95];
        var longer = 0;
        var nonZero = 0;

        for (var i = 0; i < 20_000; i++)
        {
            var schedule = random.Next(2) == 0
                ? HabitSchedule.TimesPerWeek(random.Next(1, 8))
                : HabitSchedule.TimesPerMonth(random.Next(1, 29));
            var start = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 200));
            var asOf = start.AddDays(random.Next(-10, 420));
            var density = densities[random.Next(densities.Length)];

            var done = new HashSet<DateOnly>();
            for (var day = start.AddDays(-5); day <= asOf.AddDays(5); day = day.AddDays(1))
                if (random.NextDouble() < density)
                    done.Add(day);

            // A start after "today" has no period at all.
            var expected = asOf < start ? 0 : LongestRun(PeriodMarks(schedule, start, done, asOf));
            var actual = HabitStreak.Longest(schedule, start, done, asOf);
            var current = HabitStreak.Current(schedule, start, done, asOf);

            Assert.True(expected == actual,
                $"case {i}: schedule {schedule}, start {start:O}, asOf {asOf:O}: expected {expected}, got {actual}");
            Assert.True(actual >= current, $"case {i}: the record {actual} is shorter than the streak {current}");

            if (actual > 0)
                nonZero++;
            if (actual > current)
                longer++;
        }

        Assert.True(nonZero > 4_000, $"only {nonZero} cases with a record");
        Assert.True(longer > 1_000, $"only {longer} cases where the record is longer than the streak");
    }
}
