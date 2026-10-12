using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class CalendarPeriodTests
{
    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    // ---- a week: Monday to Sunday ----

    [Theory]
    [InlineData(5)] // Monday
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)] // Thursday
    [InlineData(9)]
    [InlineData(10)] // Saturday
    [InlineData(11)] // Sunday: belongs to the week that began on the Monday before it
    public void EveryDayOfAWeek_IsInTheSameMondayToSundayWeek(int dayOfOctober)
    {
        var week = CalendarPeriod.WeekOf(D(2026, 10, dayOfOctober));

        Assert.Equal(D(2026, 10, 5), week.From);
        Assert.Equal(D(2026, 10, 11), week.To);
        Assert.Equal(7, week.Length);
        Assert.Equal(DayOfWeek.Monday, week.From.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, week.To.DayOfWeek);
    }

    [Fact]
    public void AWeek_MayCrossAMonthAndAYear()
    {
        var overMonth = CalendarPeriod.WeekOf(D(2026, 10, 1));
        Assert.Equal((D(2026, 9, 28), D(2026, 10, 4)), (overMonth.From, overMonth.To));

        // Thursday 31 December 2026 - the week runs into 2027.
        var overYear = CalendarPeriod.WeekOf(D(2026, 12, 31));
        Assert.Equal((D(2026, 12, 28), D(2027, 1, 3)), (overYear.From, overYear.To));
    }

    [Fact]
    public void TheWeeksOfConsecutiveDays_NeitherOverlapNorLeaveAGap()
    {
        var previous = CalendarPeriod.WeekOf(D(2026, 1, 1));

        for (var day = D(2026, 1, 2); day <= D(2028, 3, 1); day = day.AddDays(1))
        {
            var week = CalendarPeriod.WeekOf(day);
            Assert.True(week.Contains(day));

            if (week != previous)
            {
                Assert.Equal(previous.To.AddDays(1), week.From);
                Assert.Equal(DayOfWeek.Monday, day.DayOfWeek);
                previous = week;
            }
        }
    }

    [Fact]
    public void TheFirstAndTheLastWeekOfTheCalendar_DoNotRunOutOfIt()
    {
        var first = CalendarPeriod.WeekOf(DateOnly.MinValue);
        Assert.Equal(DateOnly.MinValue, first.From);
        Assert.Equal(DateOnly.MinValue.AddDays(6), first.To);

        // 9999-12-31 is a Friday: the week would end two days after the end of the calendar.
        var last = CalendarPeriod.WeekOf(DateOnly.MaxValue);
        Assert.Equal(DateOnly.MaxValue, last.To);
        Assert.True(last.Contains(DateOnly.MaxValue));
        Assert.Equal(DayOfWeek.Monday, last.From.DayOfWeek);
    }

    // ---- a month ----

    [Theory]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 9, 30)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)] // leap year
    [InlineData(2026, 12, 31)]
    public void AMonth_RunsFromTheFirstToTheLastDay(int year, int month, int lastDay)
    {
        var period = CalendarPeriod.MonthOf(D(year, month, 17));

        Assert.Equal(D(year, month, 1), period.From);
        Assert.Equal(D(year, month, lastDay), period.To);
        Assert.Equal(lastDay, period.Length);
    }

    [Fact]
    public void AnyDayOfAMonth_GivesTheSameMonth()
    {
        Assert.All(
            Enumerable.Range(1, 31).Select(day => D(2026, 10, day)),
            day => Assert.Equal(CalendarPeriod.MonthOf(D(2026, 10, 1)), CalendarPeriod.MonthOf(day)));
    }

    [Fact]
    public void TheLastMonthOfTheCalendar_IsAMonth()
    {
        var period = CalendarPeriod.MonthOf(DateOnly.MaxValue);

        Assert.Equal(D(9999, 12, 1), period.From);
        Assert.Equal(DateOnly.MaxValue, period.To);
    }

    // ---- the period as a value ----

    [Fact]
    public void Contains_IncludesBothEnds_AndNothingElse()
    {
        var week = CalendarPeriod.WeekOf(D(2026, 10, 8));

        Assert.True(week.Contains(D(2026, 10, 5)));
        Assert.True(week.Contains(D(2026, 10, 11)));
        Assert.False(week.Contains(D(2026, 10, 4)));
        Assert.False(week.Contains(D(2026, 10, 12)));
    }

    [Fact]
    public void TwoPeriodsWithTheSameDays_AreEqual()
    {
        Assert.Equal(CalendarPeriod.WeekOf(D(2026, 10, 5)), CalendarPeriod.WeekOf(D(2026, 10, 11)));
        Assert.NotEqual(CalendarPeriod.WeekOf(D(2026, 10, 5)), CalendarPeriod.WeekOf(D(2026, 10, 12)));
    }

    [Fact]
    public void ToString_ShowsTheTwoDays()
    {
        Assert.Equal("2026-10-05..2026-10-11", CalendarPeriod.WeekOf(D(2026, 10, 8)).ToString());
    }
}
