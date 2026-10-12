using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Services;

public class HabitStatisticsTests
{
    // The calendar of the examples (October 2026 with the days around it):
    //   Thu 1 Oct | Mon 5 ... Sun 11 | Mon 12 ... Sun 18 | Mon 19 ... Sun 25 | Mon 26 Oct ... Sun 1 Nov
    //   weeks:  28 Sep-4 Oct | 5-11 | 12-18 | 19-25 | 26 Oct-1 Nov
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static readonly DateOnly LongAgo = D(1, 1);
    private static readonly DateOnly OctoberFirst = D(10, 1);
    private static readonly DateOnly OctoberLast = D(10, 31);

    // Due in October: Thu 1, Mon 5, Thu 8, Mon 12, Thu 15, Mon 19, Thu 22, Mon 26, Thu 29 - nine days.
    private static readonly HabitSchedule MondaysAndThursdays = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday]);
    private static readonly HabitSchedule ThreeAWeek = HabitSchedule.TimesPerWeek(3);
    private static readonly HabitSchedule TwoAMonth = HabitSchedule.TimesPerMonth(2);

    private static IReadOnlySet<DateOnly> Done(params DateOnly[] days) => days.ToHashSet();

    private static StatisticsTally ForHabit(
        HabitSchedule schedule, DateOnly start, DateOnly from, DateOnly to, DateOnly asOf, params DateOnly[] done)
        => HabitStatistics.Tally(schedule, start, Done(done), from, to, asOf);

    private static StatisticsTally ForPart(
        HabitSchedule schedule, DateOnly start, DayOfWeek[] weekdays, DateOnly from, DateOnly to, DateOnly asOf,
        params DateOnly[] done)
        => HabitStatistics.Tally(schedule, start, WeekdaySet.Of(weekdays), Done(done), from, to, asOf);

    // ---- a rule with due days ----

    [Fact]
    public void ADayRule_CountsItsDueDaysAndHowManyOfThemWereDone()
    {
        var tally = ForHabit(MondaysAndThursdays, LongAgo, OctoberFirst, OctoberLast, asOf: OctoberLast,
            D(10, 1), D(10, 5), D(10, 12));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, Planned: 9, Done: 3), tally);
    }

    [Fact]
    public void ADayThatWasDoneButWasNotDue_IsNotCounted()
    {
        // Tuesday 6 and Saturday 10 are not due; ticking them neither plans nor completes anything.
        var tally = ForHabit(MondaysAndThursdays, LongAgo, OctoberFirst, OctoberLast, asOf: OctoberLast,
            D(10, 6), D(10, 10), D(10, 8));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 9, 1), tally);
    }

    [Fact]
    public void DaysAfterTheAsOfDay_AreNotPlannedYet_AndTheirMarksAreIgnored()
    {
        // As of Wednesday 14: due so far are 1, 5, 8 and 12. The Thursday 15 mark is a record from the future.
        var tally = ForHabit(MondaysAndThursdays, LongAgo, OctoberFirst, OctoberLast, asOf: D(10, 14),
            D(10, 5), D(10, 15));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 4, 1), tally);
    }

    [Fact]
    public void AnOpenDayOfToday_IsNotAMissYet()
    {
        // Thursday 8 is due and still open: 1 and 5 are judged, the 8th is not.
        var open = ForHabit(MondaysAndThursdays, LongAgo, OctoberFirst, OctoberLast, asOf: D(10, 8), D(10, 1), D(10, 5));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 2, 2), open);
    }

    [Fact]
    public void ADayOfTodayThatIsDone_IsPlannedAndDone()
    {
        var done = ForHabit(MondaysAndThursdays, LongAgo, OctoberFirst, OctoberLast, asOf: D(10, 8), D(10, 1), D(10, 8));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 3, 2), done);
    }

    [Fact]
    public void DaysBeforeTheStartDate_DoNotExist()
    {
        // Started on Tuesday 6: Thursday 1 and Monday 5 are before the habit.
        var tally = ForHabit(MondaysAndThursdays, start: D(10, 6), OctoberFirst, OctoberLast, asOf: OctoberLast,
            D(10, 1), D(10, 5), D(10, 8));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 7, 1), tally);
    }

    [Fact]
    public void TheRangeIsCutOutOfTheHistory()
    {
        // Only 12-18 October: Monday 12 and Thursday 15.
        var tally = ForHabit(MondaysAndThursdays, LongAgo, D(10, 12), D(10, 18), asOf: OctoberLast,
            D(10, 5), D(10, 12), D(10, 22));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 2, 1), tally);
    }

    [Fact]
    public void ARangeBeforeTheStartDate_IsEmpty()
    {
        var tally = ForHabit(MondaysAndThursdays, start: D(11, 1), OctoberFirst, OctoberLast, asOf: D(11, 15), D(10, 5));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 0, 0), tally);
    }

    [Fact]
    public void ARangeThatHasNotBegun_IsEmpty()
    {
        var tally = ForHabit(MondaysAndThursdays, LongAgo, D(11, 1), D(11, 30), asOf: D(10, 20), D(11, 2));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 0, 0), tally);
    }

    [Fact]
    public void ARangeTurnedRound_IsEmpty()
    {
        var tally = ForHabit(MondaysAndThursdays, LongAgo, D(10, 31), D(10, 1), asOf: OctoberLast, D(10, 5));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 0, 0), tally);
    }

    [Fact]
    public void EveryRuleWithDueDays_IsCountedInDays()
    {
        var rules = new[]
        {
            HabitSchedule.Daily,
            HabitSchedule.EveryNDays(3, D(10, 1)),
            HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Saturday),
            HabitSchedule.MonthlyOnDate(15)
        };

        Assert.All(rules, rule => Assert.Equal(CountingUnit.Days, HabitStatistics.UnitOf(rule)));
    }

    [Fact]
    public void ADailyHabit_MissedTwoOfTenDays()
    {
        var tally = ForHabit(HabitSchedule.Daily, LongAgo, OctoberFirst, D(10, 10), asOf: D(10, 10),
            D(10, 1), D(10, 2), D(10, 3), D(10, 5), D(10, 6), D(10, 7), D(10, 9), D(10, 10));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 10, 8), tally);
    }

    [Fact]
    public void ADayRule_AgreesWithADayByDayReference_OnRandomCases()
    {
        var random = new Random(20261011);
        var rules = new[]
        {
            HabitSchedule.Daily, MondaysAndThursdays, HabitSchedule.EveryNDays(4, D(9, 3)),
            HabitSchedule.Weekly([DayOfWeek.Saturday], 2, D(9, 5)), HabitSchedule.MonthlyOnDate(31)
        };

        for (var run = 0; run < 2_000; run++)
        {
            var schedule = rules[random.Next(rules.Length)];
            var start = D(9, 1).AddDays(random.Next(-5, 40));
            var from = D(10, 1).AddDays(random.Next(-20, 20));
            var to = from.AddDays(random.Next(-3, 45));
            var asOf = D(10, 1).AddDays(random.Next(-10, 45));
            var done = Enumerable.Range(0, 100).Select(i => D(8, 15).AddDays(i)).Where(_ => random.Next(3) > 0).ToHashSet();

            var planned = 0;
            var met = 0;

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (day > asOf || day < start || !schedule.IsScheduledOn(day) || (day == asOf && !done.Contains(day)))
                    continue;

                planned++;
                if (done.Contains(day))
                    met++;
            }

            var tally = HabitStatistics.Tally(schedule, start, done, from, to, asOf);

            Assert.Equal((planned, met), (tally.Planned, tally.Done));
        }
    }

    // ---- a quota ----

    [Fact]
    public void AWeeklyQuota_CountsWeeksThatReachedTheirNumber()
    {
        // Weeks ending in October: 28 Sep-4 Oct (3 ticks), 5-11 (2), 12-18 (3), 19-25 (0). The week of 26 Oct ends in November.
        var tally = ForHabit(ThreeAWeek, LongAgo, OctoberFirst, OctoberLast, asOf: OctoberLast,
            D(9, 29), D(10, 1), D(10, 3),
            D(10, 5), D(10, 9),
            D(10, 12), D(10, 14), D(10, 16));

        Assert.Equal(new StatisticsTally(CountingUnit.Weeks, 4, 2), tally);
    }

    [Fact]
    public void AWeekAcrossTwoMonths_IsCountedOnceInTheMonthItEndsIn_WithAllItsDays()
    {
        // The week of 26 Oct - 1 Nov: two ticks in October, one on Sunday 1 November.
        var ticks = new[] { D(10, 27), D(10, 29), D(11, 1) };

        var october = ForHabit(ThreeAWeek, LongAgo, OctoberFirst, OctoberLast, asOf: D(11, 30), ticks);
        var november = ForHabit(ThreeAWeek, LongAgo, D(11, 1), D(11, 30), asOf: D(11, 30), ticks);

        Assert.Equal(4, october.Planned);
        Assert.Equal(0, october.Done);
        Assert.Equal(5, november.Planned); // 26 Oct-1 Nov, 2-8, 9-15, 16-22, 23-29
        Assert.Equal(1, november.Done);
    }

    [Fact]
    public void TheRunningWeek_IsJudgedOnlyOnceItHasReachedItsNumber()
    {
        var onTrack = ForHabit(ThreeAWeek, LongAgo, OctoberFirst, OctoberLast, asOf: D(10, 14),
            D(10, 12), D(10, 13), D(10, 14));
        var behind = ForHabit(ThreeAWeek, LongAgo, OctoberFirst, OctoberLast, asOf: D(10, 14),
            D(10, 12), D(10, 13));

        // The weeks 28 Sep-4 Oct and 5-11 are over (nothing ticked: two misses); 12-18 is running.
        Assert.Equal(new StatisticsTally(CountingUnit.Weeks, 3, 1), onTrack);
        Assert.Equal(new StatisticsTally(CountingUnit.Weeks, 2, 0), behind);
    }

    [Fact]
    public void ThePeriodTheHabitBeganIn_IsNotJudged()
    {
        // Started on Wednesday 7: the week 5-11 began before the habit, even though 3 days remained and were ticked.
        var tally = ForHabit(ThreeAWeek, start: D(10, 7), OctoberFirst, OctoberLast, asOf: OctoberLast,
            D(10, 7), D(10, 8), D(10, 9));

        Assert.Equal(new StatisticsTally(CountingUnit.Weeks, 2, 0), tally); // 12-18 and 19-25
    }

    [Fact]
    public void AMonthlyQuota_CountsMonths()
    {
        var met = ForHabit(TwoAMonth, LongAgo, OctoberFirst, OctoberLast, asOf: D(11, 2), D(10, 3), D(10, 20));
        var fellShort = ForHabit(TwoAMonth, LongAgo, OctoberFirst, OctoberLast, asOf: D(11, 2), D(10, 3));

        Assert.Equal(new StatisticsTally(CountingUnit.Months, 1, 1), met);
        Assert.Equal(new StatisticsTally(CountingUnit.Months, 1, 0), fellShort);
    }

    [Fact]
    public void TheLastDayOfAMonthIsStillRunning_SoAMonthThatFellShortIsNotJudgedYet()
    {
        var tally = ForHabit(TwoAMonth, LongAgo, OctoberFirst, OctoberLast, asOf: OctoberLast, D(10, 3));

        Assert.Equal(new StatisticsTally(CountingUnit.Months, 0, 0), tally);
    }

    [Fact]
    public void AQuotaIsCountedInWeeksOrMonths()
    {
        Assert.Equal(CountingUnit.Weeks, HabitStatistics.UnitOf(ThreeAWeek));
        Assert.Equal(CountingUnit.Months, HabitStatistics.UnitOf(TwoAMonth));
    }

    [Fact]
    public void AQuotaRangeThatHasNotBegun_IsEmpty()
    {
        var tally = ForHabit(ThreeAWeek, LongAgo, D(11, 1), D(11, 30), asOf: D(10, 20), D(11, 2));

        Assert.Equal(new StatisticsTally(CountingUnit.Weeks, 0, 0), tally);
    }

    // ---- a sub-habit ----

    [Fact]
    public void ASubHabit_IsDueOnTheHabitsDueDaysThatHaveItsWeekday()
    {
        // The habit is due on Monday and Thursday; "Legs" is planned on Mondays: 5, 12, 19, 26.
        var tally = ForPart(MondaysAndThursdays, LongAgo, [DayOfWeek.Monday], OctoberFirst, OctoberLast, OctoberLast,
            D(10, 5), D(10, 19));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 4, 2), tally);
    }

    [Fact]
    public void ASubHabit_IgnoresATickOnADayItIsNotDueOn()
    {
        var tally = ForPart(MondaysAndThursdays, LongAgo, [DayOfWeek.Monday], OctoberFirst, OctoberLast, OctoberLast,
            D(10, 8), D(10, 6), D(10, 12));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 4, 1), tally);
    }

    [Fact]
    public void ASubHabit_AnOpenDayOfToday_IsNotAMissYet()
    {
        var open = ForPart(MondaysAndThursdays, LongAgo, [DayOfWeek.Monday], OctoberFirst, OctoberLast, asOf: D(10, 12),
            D(10, 5));
        var done = ForPart(MondaysAndThursdays, LongAgo, [DayOfWeek.Monday], OctoberFirst, OctoberLast, asOf: D(10, 12),
            D(10, 5), D(10, 12));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 1, 1), open);
        Assert.Equal(new StatisticsTally(CountingUnit.Days, 2, 2), done);
    }

    [Fact]
    public void ASubHabitOfAQuotaHabit_IsCountedInDays_OnItsWeekdays()
    {
        // No due day for the habit itself: every Wednesday from the start counts for the part.
        var tally = ForPart(ThreeAWeek, LongAgo, [DayOfWeek.Wednesday], OctoberFirst, OctoberLast, OctoberLast,
            D(10, 7), D(10, 21));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 4, 2), tally); // 7, 14, 21, 28
    }

    [Fact]
    public void ASubHabitWhoseWeekdayTheHabitLost_IsNeverDue()
    {
        // Planned on Fridays, but the habit is only due on Monday and Thursday now.
        var tally = ForPart(MondaysAndThursdays, LongAgo, [DayOfWeek.Friday], OctoberFirst, OctoberLast, OctoberLast,
            D(10, 2), D(10, 9));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 0, 0), tally);
    }

    [Fact]
    public void ASubHabit_DoesNotExistBeforeTheHabitsStartDate()
    {
        var tally = ForPart(MondaysAndThursdays, start: D(10, 13), [DayOfWeek.Monday], OctoberFirst, OctoberLast, OctoberLast,
            D(10, 5), D(10, 19));

        Assert.Equal(new StatisticsTally(CountingUnit.Days, 2, 1), tally); // 19 and 26
    }

    // ---- last done ----

    [Fact]
    public void LastDone_IsTheLatestDayOnOrBeforeAsOf_FromTheWholeHistory()
    {
        var last = HabitStatistics.LastDone([D(3, 2), D(9, 20), D(10, 4), D(10, 20)], asOf: D(10, 10));

        Assert.Equal(D(10, 4), last);
    }

    [Fact]
    public void LastDone_CountsTheAsOfDayItself()
    {
        Assert.Equal(D(10, 10), HabitStatistics.LastDone([D(10, 4), D(10, 10)], asOf: D(10, 10)));
    }

    [Fact]
    public void LastDone_IsNullWithoutAnyDayOnOrBeforeAsOf()
    {
        Assert.Null(HabitStatistics.LastDone([], asOf: D(10, 10)));
        Assert.Null(HabitStatistics.LastDone([D(10, 11)], asOf: D(10, 10)));
    }
}
