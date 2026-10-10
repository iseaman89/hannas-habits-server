using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Services;

public class HabitQuotaTests
{
    // The calendar of the examples (October 2026 with the days around it):
    //   Mon 28 Sep ... Sun 4 Oct | Mon 5 ... Sun 11 | Mon 12 ... Sun 18 | Mon 19 ... Sun 25 | Mon 26 Oct ... Sun 1 Nov
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static readonly HabitSchedule ThreeAWeek = HabitSchedule.TimesPerWeek(3);
    private static readonly HabitSchedule TwoAMonth = HabitSchedule.TimesPerMonth(2);
    private static readonly DateOnly LongAgo = D(1, 1);

    private static IReadOnlySet<DateOnly> Done(params DateOnly[] days) => days.ToHashSet();

    private static int DoneIn(CalendarPeriod period, DateOnly start, DateOnly asOf, params DateOnly[] done)
        => HabitQuota.DoneIn(period, start, Done(done), asOf);

    // ---- DoneIn ----

    [Fact]
    public void DoneIn_CountsTheCompletedDaysOfThePeriod()
    {
        var week = CalendarPeriod.WeekOf(D(10, 8));

        Assert.Equal(3, DoneIn(week, LongAgo, asOf: D(10, 11), D(10, 5), D(10, 7), D(10, 11)));
    }

    [Fact]
    public void DoneIn_IgnoresDaysOutsideThePeriod()
    {
        var week = CalendarPeriod.WeekOf(D(10, 8));

        Assert.Equal(1, DoneIn(week, LongAgo, asOf: D(10, 31), D(10, 4), D(10, 7), D(10, 12)));
    }

    [Fact]
    public void DoneIn_IgnoresDaysBeforeTheStartDate()
    {
        var week = CalendarPeriod.WeekOf(D(10, 8));

        // Started on Wednesday: the Monday and Tuesday did not exist for the habit.
        Assert.Equal(1, DoneIn(week, start: D(10, 7), asOf: D(10, 11), D(10, 5), D(10, 6), D(10, 7)));
    }

    [Fact]
    public void DoneIn_IgnoresDaysAfterTheAsOfDay()
    {
        var week = CalendarPeriod.WeekOf(D(10, 8));

        Assert.Equal(2, DoneIn(week, LongAgo, asOf: D(10, 8), D(10, 5), D(10, 8), D(10, 9), D(10, 10)));
    }

    [Fact]
    public void DoneIn_AStartDateAfterTheAsOfDay_IsZero()
    {
        Assert.Equal(0, DoneIn(CalendarPeriod.WeekOf(D(10, 8)), start: D(10, 9), asOf: D(10, 8), D(10, 8), D(10, 9)));
    }

    [Fact]
    public void DoneIn_ACompletedDayCountsOnce()
    {
        // A set cannot hold a day twice - the count is the number of days, not of records.
        var week = CalendarPeriod.WeekOf(D(10, 8));

        Assert.Equal(1, HabitQuota.DoneIn(week, LongAgo, new[] { D(10, 8), D(10, 8) }.ToHashSet(), D(10, 11)));
    }

    // ---- Between ----

    [Fact]
    public void Between_ListsEveryWeekThatTouchesTheRange_OldestFirst()
    {
        var progress = HabitQuota.Between(ThreeAWeek, LongAgo, Done(), from: D(10, 1), to: D(10, 31), asOf: D(10, 8));

        Assert.Equal(
            [D(9, 28), D(10, 5), D(10, 12), D(10, 19), D(10, 26)],
            progress.Select(p => p.Period.From));
        Assert.Equal(
            [D(10, 4), D(10, 11), D(10, 18), D(10, 25), D(11, 1)],
            progress.Select(p => p.Period.To));
    }

    [Fact]
    public void Between_AWeekThatReachesOutOfTheRange_IsCountedWholeNotCutAtTheRange()
    {
        // The grid shows October: the first week began on 28 September and its marks of September count.
        var done = Done(D(9, 28), D(9, 30), D(10, 2));

        var first = HabitQuota.Between(ThreeAWeek, LongAgo, done, from: D(10, 1), to: D(10, 31), asOf: D(10, 8))[0];

        Assert.Equal(3, first.Done);
    }

    [Fact]
    public void Between_AWeekThatHasNotBegun_IsListedWithZero()
    {
        var progress = HabitQuota.Between(ThreeAWeek, LongAgo, Done(D(10, 5)), D(10, 1), D(10, 31), asOf: D(10, 8));

        Assert.Equal([0, 1, 0, 0, 0], progress.Select(p => p.Done));
    }

    [Fact]
    public void Between_LeavesOutAPeriodThatEndedBeforeTheStartDate()
    {
        var progress = HabitQuota.Between(ThreeAWeek, start(), Done(), D(10, 1), D(10, 31), asOf: D(10, 8));

        Assert.Equal(D(10, 5), progress[0].Period.From);
        Assert.Equal(4, progress.Count);

        static DateOnly start() => D(10, 8);
    }

    [Fact]
    public void Between_KeepsThePeriodTheStartDateIsIn_EvenOnItsLastDay()
    {
        // Sunday 4 October: the habit exists for one day of that week.
        var progress = HabitQuota.Between(ThreeAWeek, D(10, 4), Done(D(10, 4)), D(10, 1), D(10, 10), asOf: D(10, 4));

        Assert.Equal(D(9, 28), progress[0].Period.From);
        Assert.Equal(1, progress[0].Done);
    }

    [Fact]
    public void Between_TheMonths_OfARangeThatCrossesThem()
    {
        var done = Done(D(9, 20), D(10, 2), D(10, 3), D(10, 31));

        var progress = HabitQuota.Between(TwoAMonth, LongAgo, done, from: D(9, 15), to: D(11, 3), asOf: D(10, 31));

        Assert.Equal([D(9, 1), D(10, 1), D(11, 1)], progress.Select(p => p.Period.From));
        Assert.Equal([D(9, 30), D(10, 31), D(11, 30)], progress.Select(p => p.Period.To));
        Assert.Equal([1, 3, 0], progress.Select(p => p.Done));
    }

    [Fact]
    public void Between_ARuleWithDueDays_HasNoQuotaPeriods()
    {
        Assert.Empty(HabitQuota.Between(HabitSchedule.Daily, LongAgo, Done(D(10, 5)), D(10, 1), D(10, 31), D(10, 8)));
        Assert.Empty(HabitQuota.Between(
            HabitSchedule.MonthlyOnDate(15), LongAgo, Done(D(10, 15)), D(10, 1), D(10, 31), D(10, 8)));
    }

    [Fact]
    public void Between_ATurnedRoundRange_IsEmpty()
    {
        Assert.Empty(HabitQuota.Between(ThreeAWeek, LongAgo, Done(), from: D(10, 11), to: D(10, 5), asOf: D(10, 8)));
    }

    [Fact]
    public void Between_ASingleDay_IsItsPeriod()
    {
        var progress = HabitQuota.Between(ThreeAWeek, LongAgo, Done(D(10, 5), D(10, 6)), D(10, 8), D(10, 8), D(10, 8));

        var only = Assert.Single(progress);
        Assert.Equal((D(10, 5), D(10, 11), 2), (only.Period.From, only.Period.To, only.Done));
    }

    [Fact]
    public void Between_TheLastDayOfTheCalendar_DoesNotOverflowOrLoop()
    {
        var week = HabitQuota.Between(
            ThreeAWeek, LongAgo, Done(DateOnly.MaxValue), DateOnly.MaxValue, DateOnly.MaxValue, DateOnly.MaxValue);
        var month = HabitQuota.Between(
            TwoAMonth, LongAgo, Done(DateOnly.MaxValue), DateOnly.MaxValue, DateOnly.MaxValue, DateOnly.MaxValue);

        Assert.Equal(1, Assert.Single(week).Done);
        Assert.Equal(1, Assert.Single(month).Done);
    }
}
