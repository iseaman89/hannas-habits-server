using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class HabitScheduleTests
{
    private static readonly DayOfWeek[] AllDays = Enum.GetValues<DayOfWeek>();

    // The calendar of the examples (a Monday-first view of the months the tests use):
    //   September 2026: Mon 7, 14, 21, 28          October 2026: Thu 1, Mon 5, 12, 19, 26, Sat 31
    //   November 2026: Sun 1, Mon 2, 9, 16, 23, 30 February 2026: Sun 1, Fri 6, 13, 20, 27 (28 days)
    //   February 2028 (leap): Tue 1, 8, 15, 22, 29
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static bool Due(HabitSchedule schedule, int year, int month, int day) => schedule.IsScheduledOn(D(year, month, day));

    private static IEnumerable<DateOnly> DaysOfRange(DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
            yield return day;
    }

    // ---- the weekly rule (every week) ----

    [Fact]
    public void Daily_IsEveryDayOfTheWeek_SundayFirst()
    {
        Assert.Equal(
            [
                DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
            ],
            HabitSchedule.Daily.Days);
    }

    [Fact]
    public void Daily_IsAWeeklyRuleEveryWeek()
    {
        Assert.Equal(ScheduleKind.Weekly, HabitSchedule.Daily.Kind);
        Assert.Equal(1, HabitSchedule.Daily.Interval);
        Assert.Null(HabitSchedule.Daily.Anchor);
    }

    [Fact]
    public void Weekly_ReturnsTheDaysInWeekOrderWhateverTheInputOrder()
    {
        var schedule = HabitSchedule.Weekly([DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Wednesday]);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], schedule.Days);
    }

    [Fact]
    public void Weekly_CollapsesDuplicates()
    {
        var schedule = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Monday, DayOfWeek.Monday]);

        Assert.Equal([DayOfWeek.Monday], schedule.Days);
    }

    [Fact]
    public void Weekly_RejectsAnEmptySchedule()
    {
        Assert.Throws<DomainException>(() => HabitSchedule.Weekly([]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Weekly_RejectsANumberThatIsNoDayOfTheWeek(int day)
    {
        Assert.Throws<DomainException>(() => HabitSchedule.Weekly([DayOfWeek.Monday, (DayOfWeek)day]));
    }

    [Fact]
    public void Weekly_IsDueOnlyOnTheScheduledWeekdays()
    {
        var schedule = HabitSchedule.Weekly([DayOfWeek.Tuesday, DayOfWeek.Saturday]);

        // 2026-10-05 is a Monday: the week runs Mon 5 .. Sun 11.
        var week = DaysOfRange(D(2026, 10, 5), D(2026, 10, 11)).ToList();

        Assert.Equal(
            [D(2026, 10, 6), D(2026, 10, 10)],
            week.Where(schedule.IsScheduledOn));
    }

    // Every one of the 127 possible schedules: the days that go in are the days that come out, and the rule is due on
    // exactly those weekdays - in any week.
    [Fact]
    public void EveryNonEmptySetOfDaysRoundTrips()
    {
        var aWeek = DaysOfRange(D(2026, 10, 5), D(2026, 10, 11)).ToList();

        for (var mask = 1; mask <= 0b111_1111; mask++)
        {
            var expected = AllDays.Where(day => (mask & (1 << (int)day)) != 0).ToArray();

            var schedule = HabitSchedule.Weekly(expected);

            Assert.Equal(expected, schedule.Days);
            Assert.Equal(expected.Length, aWeek.Count(schedule.IsScheduledOn));
            Assert.All(aWeek, date => Assert.Equal(expected.Contains(date.DayOfWeek), schedule.IsScheduledOn(date)));
        }
    }

    // ---- every N weeks ----

    [Fact]
    public void EveryOtherWeek_IsDueInTheAnchorWeekAndEveryTwoWeeksAfterAndBefore()
    {
        // Anchor: Wednesday 7 October - its week began on Monday 5 October.
        var schedule = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday], everyWeeks: 2, anchor: D(2026, 10, 7));

        Assert.True(Due(schedule, 2026, 10, 5));   // Mon of the anchor week
        Assert.True(Due(schedule, 2026, 10, 8));   // Thu of the anchor week
        Assert.False(Due(schedule, 2026, 10, 6));  // right week, wrong weekday
        Assert.False(Due(schedule, 2026, 10, 12)); // next week: off
        Assert.False(Due(schedule, 2026, 10, 15));
        Assert.True(Due(schedule, 2026, 10, 19));  // two weeks on
        Assert.True(Due(schedule, 2026, 10, 22));
        Assert.True(Due(schedule, 2026, 9, 21));   // before the anchor, the same rhythm
        Assert.False(Due(schedule, 2026, 9, 28));
    }

    [Fact]
    public void EveryNWeeks_CountsWeeksFromMonday_ASundayBelongsToTheWeekBeforeIt()
    {
        var schedule = HabitSchedule.Weekly([DayOfWeek.Sunday], everyWeeks: 2, anchor: D(2026, 10, 5));

        Assert.True(Due(schedule, 2026, 10, 11));  // the Sunday that ends the anchor week
        Assert.False(Due(schedule, 2026, 10, 18)); // end of the off week
        Assert.True(Due(schedule, 2026, 10, 25));
        Assert.False(Due(schedule, 2026, 10, 4));  // the Sunday before the anchor week belongs to an off week
    }

    [Fact]
    public void AnAnchorOnASunday_BelongsToTheWeekThatEndsThere()
    {
        var onSunday = HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 2, anchor: D(2026, 10, 11));
        var onMonday = HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 2, anchor: D(2026, 10, 5));

        foreach (var date in DaysOfRange(D(2026, 9, 1), D(2026, 12, 31)))
            Assert.Equal(onMonday.IsScheduledOn(date), onSunday.IsScheduledOn(date));
    }

    [Fact]
    public void EveryThreeWeeks_HitsOneWeekInThree()
    {
        var schedule = HabitSchedule.Weekly([DayOfWeek.Friday], everyWeeks: 3, anchor: D(2026, 10, 5));

        var fridays = DaysOfRange(D(2026, 10, 1), D(2026, 12, 31)).Where(schedule.IsScheduledOn).ToList();

        Assert.Equal(
            [D(2026, 10, 9), D(2026, 10, 30), D(2026, 11, 20), D(2026, 12, 11)],
            fridays);
    }

    [Fact]
    public void EveryWeek_IgnoresTheAnchor_SoTheRhythmHasNoStateToDrift()
    {
        var withAnchor = HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 1, anchor: D(2026, 10, 7));
        var without = HabitSchedule.Weekly([DayOfWeek.Monday]);

        Assert.Equal(without, withAnchor);
        Assert.Null(withAnchor.Anchor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(HabitSchedule.MaxWeekInterval + 1)]
    public void Weekly_RejectsAnIntervalOutOfRange(int everyWeeks)
    {
        Assert.Throws<DomainException>(() => HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks, D(2026, 10, 5)));
    }

    [Fact]
    public void Weekly_AcceptsTheLongestInterval()
    {
        var schedule = HabitSchedule.Weekly([DayOfWeek.Monday], HabitSchedule.MaxWeekInterval, D(2026, 10, 5));

        Assert.Equal(HabitSchedule.MaxWeekInterval, schedule.Interval);
        Assert.True(Due(schedule, 2026, 10, 5));
        Assert.True(schedule.IsScheduledOn(D(2026, 10, 5).AddDays(7 * HabitSchedule.MaxWeekInterval)));
    }

    [Fact]
    public void Weekly_EveryOtherWeekNeedsAnAnchor()
    {
        Assert.Throws<DomainException>(() => HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 2));
    }

    // ---- every N days ----

    [Fact]
    public void EveryNDays_IsDueOnTheAnchorAndEveryNDaysFromIt_InBothDirections()
    {
        var schedule = HabitSchedule.EveryNDays(3, D(2026, 10, 10));

        var due = DaysOfRange(D(2026, 9, 28), D(2026, 10, 22)).Where(schedule.IsScheduledOn).ToList();

        Assert.Equal(
            [
                D(2026, 9, 28), D(2026, 10, 1), D(2026, 10, 4), D(2026, 10, 7), D(2026, 10, 10),
                D(2026, 10, 13), D(2026, 10, 16), D(2026, 10, 19), D(2026, 10, 22)
            ],
            due);
    }

    [Fact]
    public void EveryNDays_CrossesMonthAndYearEnds()
    {
        var schedule = HabitSchedule.EveryNDays(10, D(2026, 12, 25));

        Assert.True(Due(schedule, 2027, 1, 4));
        Assert.True(Due(schedule, 2027, 1, 14));
        Assert.False(Due(schedule, 2027, 1, 5));
        Assert.True(Due(schedule, 2026, 12, 15));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(HabitSchedule.MaxDayInterval + 1)]
    public void EveryNDays_RejectsAnIntervalOutOfRange_EveryDayIsTheDailySchedule(int days)
    {
        Assert.Throws<DomainException>(() => HabitSchedule.EveryNDays(days, D(2026, 10, 10)));
    }

    [Theory]
    [InlineData(HabitSchedule.MinDayInterval)]
    [InlineData(HabitSchedule.MaxDayInterval)]
    public void EveryNDays_AcceptsTheBounds(int days)
    {
        Assert.Equal(days, HabitSchedule.EveryNDays(days, D(2026, 10, 10)).Interval);
    }

    // ---- anchors ----

    [Fact]
    public void AnAnchor_MustBeWithinThePlausibleRange()
    {
        Assert.Throws<DomainException>(() => HabitSchedule.EveryNDays(3, D(1999, 12, 31)));
        Assert.Throws<DomainException>(() => HabitSchedule.EveryNDays(3, D(2101, 1, 1)));
        Assert.Throws<DomainException>(() => HabitSchedule.Weekly([DayOfWeek.Monday], 2, DateOnly.MinValue));

        Assert.Equal(HabitSchedule.MinAnchor, HabitSchedule.EveryNDays(3, HabitSchedule.MinAnchor).Anchor);
        Assert.Equal(HabitSchedule.MaxAnchor, HabitSchedule.EveryNDays(3, HabitSchedule.MaxAnchor).Anchor);
    }

    // ---- monthly on a weekday ----

    [Theory]
    [InlineData(WeekdayOccurrence.First, 2026, 10, 5)]
    [InlineData(WeekdayOccurrence.Second, 2026, 10, 12)]
    [InlineData(WeekdayOccurrence.Third, 2026, 10, 19)]
    [InlineData(WeekdayOccurrence.Fourth, 2026, 10, 26)]
    [InlineData(WeekdayOccurrence.Last, 2026, 10, 26)]  // October 2026 has four Mondays: the fourth is the last
    [InlineData(WeekdayOccurrence.Fourth, 2026, 11, 23)]
    [InlineData(WeekdayOccurrence.Last, 2026, 11, 30)]  // November 2026 has five Mondays: the last is the fifth
    public void MonthlyOnWeekday_FindsTheMonday(WeekdayOccurrence occurrence, int year, int month, int expectedDay)
    {
        var schedule = HabitSchedule.MonthlyOnWeekday(occurrence, DayOfWeek.Monday);

        var due = DaysOfRange(D(year, month, 1), D(year, month, DateTime.DaysInMonth(year, month))).Where(schedule.IsScheduledOn);

        Assert.Equal([D(year, month, expectedDay)], due);
    }

    [Fact]
    public void MonthlyOnWeekday_TheLastDay_IsTheLastOccurrence_InAMonthOfEveryLength()
    {
        var lastFriday = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Friday);
        var lastTuesday = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Tuesday);

        Assert.True(Due(lastFriday, 2026, 2, 27));   // February, 28 days: Fridays 6, 13, 20, 27
        Assert.True(Due(lastTuesday, 2028, 2, 29));  // leap February: Tuesdays 1, 8, 15, 22, 29
        Assert.False(Due(lastTuesday, 2028, 2, 22));
    }

    [Fact]
    public void MonthlyOnWeekday_OccursExactlyOncePerMonth_ForEveryWeekdayAndOccurrence_AcrossFiveYears()
    {
        foreach (var occurrence in Enum.GetValues<WeekdayOccurrence>())
        foreach (var day in AllDays)
        {
            var schedule = HabitSchedule.MonthlyOnWeekday(occurrence, day);

            for (var year = 2026; year <= 2030; year++)
            for (var month = 1; month <= 12; month++)
            {
                var due = DaysOfRange(D(year, month, 1), D(year, month, DateTime.DaysInMonth(year, month)))
                    .Where(schedule.IsScheduledOn)
                    .ToList();

                Assert.True(due.Count == 1, $"{occurrence} {day} in {year}-{month:00}: {due.Count} days");
            }
        }
    }

    [Fact]
    public void MonthlyOnWeekday_KnowsItsWeekday_AndRejectsNonsense()
    {
        var schedule = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Second, DayOfWeek.Saturday);

        Assert.Equal([DayOfWeek.Saturday], schedule.Days);
        Assert.Equal(WeekdayOccurrence.Second, schedule.Occurrence);

        Assert.Throws<DomainException>(() => HabitSchedule.MonthlyOnWeekday((WeekdayOccurrence)0, DayOfWeek.Monday));
        Assert.Throws<DomainException>(() => HabitSchedule.MonthlyOnWeekday((WeekdayOccurrence)6, DayOfWeek.Monday));
        Assert.Throws<DomainException>(() => HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.First, (DayOfWeek)7));
    }

    // ---- monthly on a date ----

    [Fact]
    public void MonthlyOnDate_IsDueOnThatDateOfEveryMonth()
    {
        var schedule = HabitSchedule.MonthlyOnDate(15);

        var due = DaysOfRange(D(2026, 10, 1), D(2027, 1, 31)).Where(schedule.IsScheduledOn);

        Assert.Equal([D(2026, 10, 15), D(2026, 11, 15), D(2026, 12, 15), D(2027, 1, 15)], due);
    }

    [Fact]
    public void MonthlyOnDate_ADateAMonthDoesNotHave_FallsOnItsLastDay()
    {
        var the31st = HabitSchedule.MonthlyOnDate(31);
        var the30th = HabitSchedule.MonthlyOnDate(30);
        var the29th = HabitSchedule.MonthlyOnDate(29);

        Assert.True(Due(the31st, 2026, 1, 31));
        Assert.True(Due(the31st, 2026, 4, 30));  // April has 30 days
        Assert.False(Due(the31st, 2026, 4, 29));
        Assert.True(Due(the31st, 2026, 2, 28));
        Assert.True(Due(the31st, 2028, 2, 29));  // leap year

        Assert.True(Due(the30th, 2026, 2, 28));
        Assert.True(Due(the30th, 2026, 3, 30));
        Assert.False(Due(the30th, 2026, 3, 31));

        Assert.True(Due(the29th, 2026, 2, 28));  // not a leap year
        Assert.True(Due(the29th, 2028, 2, 29));
        Assert.False(Due(the29th, 2028, 2, 28));
    }

    [Fact]
    public void MonthlyOnDate_OccursExactlyOncePerMonth_ForEveryDate_AcrossFiveYears()
    {
        for (var dayOfMonth = 1; dayOfMonth <= HabitSchedule.MaxDayOfMonth; dayOfMonth++)
        {
            var schedule = HabitSchedule.MonthlyOnDate(dayOfMonth);

            for (var year = 2026; year <= 2030; year++)
            for (var month = 1; month <= 12; month++)
            {
                var due = DaysOfRange(D(year, month, 1), D(year, month, DateTime.DaysInMonth(year, month)))
                    .Count(schedule.IsScheduledOn);

                Assert.True(due == 1, $"day {dayOfMonth} in {year}-{month:00}: {due} days");
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32)]
    public void MonthlyOnDate_RejectsADateOutOfRange(int dayOfMonth)
    {
        Assert.Throws<DomainException>(() => HabitSchedule.MonthlyOnDate(dayOfMonth));
    }

    // ---- what the kinds expose ----

    [Fact]
    public void TheRulesThatDoNotDependOnTheWeekday_SayEveryWeekdayIsPossible()
    {
        var all = AllDays;

        Assert.Equal(all, HabitSchedule.EveryNDays(3, D(2026, 10, 10)).Days);
        Assert.Equal(all, HabitSchedule.MonthlyOnDate(15).Days);
    }

    [Fact]
    public void TheFactsThatDoNotApplyToAKind_AreEmpty()
    {
        var weekly = HabitSchedule.Weekly([DayOfWeek.Monday]);
        var everyNDays = HabitSchedule.EveryNDays(3, D(2026, 10, 10));
        var onWeekday = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Friday);
        var onDate = HabitSchedule.MonthlyOnDate(15);

        Assert.Equal((ScheduleKind.Weekly, 1, null, null, null),
            (weekly.Kind, weekly.Interval, weekly.Anchor, weekly.Occurrence, weekly.DayOfMonth));
        Assert.Equal((ScheduleKind.EveryNDays, 3, D(2026, 10, 10), null, null),
            (everyNDays.Kind, everyNDays.Interval, everyNDays.Anchor, everyNDays.Occurrence, everyNDays.DayOfMonth));
        Assert.Equal((ScheduleKind.MonthlyOnWeekday, 1, null, WeekdayOccurrence.Last, null),
            (onWeekday.Kind, onWeekday.Interval, onWeekday.Anchor, onWeekday.Occurrence, onWeekday.DayOfMonth));
        Assert.Equal((ScheduleKind.MonthlyOnDate, 1, null, null, 15),
            (onDate.Kind, onDate.Interval, onDate.Anchor, onDate.Occurrence, onDate.DayOfMonth));
    }

    // ---- equality ----

    [Fact]
    public void SchedulesWithTheSameDaysAreEqual_RegardlessOfOrderAndDuplicates()
    {
        var first = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var second = HabitSchedule.Weekly([DayOfWeek.Tuesday, DayOfWeek.Monday, DayOfWeek.Monday]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void SchedulesWithDifferentDaysAreNotEqual()
    {
        Assert.NotEqual(
            HabitSchedule.Weekly([DayOfWeek.Monday]),
            HabitSchedule.Weekly([DayOfWeek.Tuesday]));
    }

    [Fact]
    public void ADailyScheduleEqualsOneBuiltFromAllSevenDays()
    {
        Assert.Equal(HabitSchedule.Daily, HabitSchedule.Weekly(AllDays));
    }

    [Fact]
    public void TheSameRuleIsEqual_ADifferentParameterOrKindIsNot()
    {
        Assert.Equal(HabitSchedule.EveryNDays(3, D(2026, 10, 10)), HabitSchedule.EveryNDays(3, D(2026, 10, 10)));
        Assert.NotEqual(HabitSchedule.EveryNDays(3, D(2026, 10, 10)), HabitSchedule.EveryNDays(3, D(2026, 10, 11)));
        Assert.NotEqual(HabitSchedule.EveryNDays(3, D(2026, 10, 10)), HabitSchedule.EveryNDays(4, D(2026, 10, 10)));

        Assert.Equal(HabitSchedule.MonthlyOnDate(15), HabitSchedule.MonthlyOnDate(15));
        Assert.NotEqual(HabitSchedule.MonthlyOnDate(15), HabitSchedule.MonthlyOnDate(16));

        Assert.NotEqual(
            HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.First, DayOfWeek.Monday),
            HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Monday));

        // Seven days of every week and "every day" are the same rule; "every 2nd week" is not.
        Assert.NotEqual(HabitSchedule.Daily, HabitSchedule.Weekly(AllDays, 2, D(2026, 10, 5)));
        // Every weekday possible, but a different rule.
        Assert.NotEqual(HabitSchedule.MonthlyOnDate(15), HabitSchedule.Daily);
    }

    // ---- the rule against an independent formulation ----

    // The same rules, written the slow and obvious way: walk to the Monday of a week, step from the anchor by N,
    // list the days of the month and pick one. No day-number arithmetic - so it shares no bug with the real thing.
    private static bool Naive(HabitSchedule schedule, DateOnly date)
    {
        switch (schedule.Kind)
        {
            case ScheduleKind.Weekly:
                if (!schedule.Days.Contains(date.DayOfWeek))
                    return false;

                if (schedule.Interval == 1)
                    return true;

                var weeks = 0;
                var week = MondayOf(schedule.Anchor!.Value);
                var target = MondayOf(date);
                for (; week < target; week = week.AddDays(7)) weeks++;
                for (; week > target; week = week.AddDays(-7)) weeks++;
                return weeks % schedule.Interval == 0;

            case ScheduleKind.EveryNDays:
                var anchor = schedule.Anchor!.Value;
                var step = date >= anchor ? schedule.Interval : -schedule.Interval;
                for (var day = anchor; step > 0 ? day <= date : day >= date; day = day.AddDays(step))
                    if (day == date)
                        return true;
                return false;

            case ScheduleKind.MonthlyOnWeekday:
                var sameWeekday = DaysOfMonth(date).Where(d => d.DayOfWeek == schedule.Days.Single()).ToList();
                var wanted = schedule.Occurrence == WeekdayOccurrence.Last ? sameWeekday[^1] : sameWeekday[(int)schedule.Occurrence! - 1];
                return wanted == date;

            default:
                var days = DaysOfMonth(date).ToList();
                return days[Math.Min(schedule.DayOfMonth!.Value, days.Count) - 1] == date;
        }
    }

    private static DateOnly MondayOf(DateOnly date)
    {
        while (date.DayOfWeek != DayOfWeek.Monday)
            date = date.AddDays(-1);
        return date;
    }

    private static IEnumerable<DateOnly> DaysOfMonth(DateOnly date)
        => DaysOfRange(new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));

    [Fact]
    public void IsScheduledOn_AgreesWithTheNaiveFormulation_OnRandomRules()
    {
        var random = new Random(20261010); // fixed seed: the same cases on every run
        var dueSomewhere = 0;

        for (var i = 0; i < 400; i++)
        {
            var anchor = new DateOnly(2026, 1, 1).AddDays(random.Next(0, 400));
            var schedule = random.Next(4) switch
            {
                0 => HabitSchedule.Weekly(AllDays.Where(_ => random.Next(2) == 0).DefaultIfEmpty(DayOfWeek.Monday), random.Next(1, 8), anchor),
                1 => HabitSchedule.EveryNDays(random.Next(2, 40), anchor),
                2 => HabitSchedule.MonthlyOnWeekday((WeekdayOccurrence)random.Next(1, 6), AllDays[random.Next(7)]),
                _ => HabitSchedule.MonthlyOnDate(random.Next(1, 32))
            };

            foreach (var date in DaysOfRange(new DateOnly(2025, 6, 1), new DateOnly(2027, 8, 31)).Where(_ => random.Next(6) == 0))
            {
                var expected = Naive(schedule, date);

                Assert.True(expected == schedule.IsScheduledOn(date), $"{schedule} on {date:O}: expected {expected}");

                if (expected)
                    dueSomewhere++;
            }
        }

        // A test that never sees a due day would agree with a rule that is never due.
        Assert.True(dueSomewhere > 300, $"only {dueSomewhere} due days were compared");
    }

    [Fact]
    public void TheDayNumberZeroIsAMonday_WhichTheWeekCountingRestsOn()
    {
        Assert.Equal(DayOfWeek.Monday, DateOnly.MinValue.DayOfWeek);
        Assert.Equal(0, DateOnly.MinValue.DayNumber);
    }

    // ---- text ----

    [Fact]
    public void ToString_ListsTheDays()
    {
        Assert.Equal("Monday, Friday", HabitSchedule.Weekly([DayOfWeek.Friday, DayOfWeek.Monday]).ToString());
    }

    [Fact]
    public void ToString_DescribesTheOtherRules()
    {
        Assert.Equal(
            "Every 2 weeks on Monday, Thursday (weeks counted from 2026-10-07)",
            HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday], 2, D(2026, 10, 7)).ToString());
        Assert.Equal("Every 3 days (counted from 2026-10-10)", HabitSchedule.EveryNDays(3, D(2026, 10, 10)).ToString());
        Assert.Equal("The last Saturday of the month", HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Saturday).ToString());
        Assert.Equal("Day 15 of the month", HabitSchedule.MonthlyOnDate(15).ToString());
    }
}
