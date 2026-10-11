using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class WeekdaySetTests
{
    [Fact]
    public void Of_KeepsTheGivenDays_InWeekOrderStartingOnSunday()
    {
        var set = WeekdaySet.Of([DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Sunday]);

        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Friday], set.Days);
    }

    [Fact]
    public void Of_CollapsesDuplicates()
    {
        var set = WeekdaySet.Of([DayOfWeek.Monday, DayOfWeek.Monday]);

        Assert.Equal([DayOfWeek.Monday], set.Days);
    }

    [Fact]
    public void Of_RejectsAnEmptySet()
    {
        Assert.Throws<DomainException>(() => WeekdaySet.Of([]));
    }

    [Fact]
    public void Of_RejectsADayThatIsNotOne()
    {
        Assert.Throws<DomainException>(() => WeekdaySet.Of([(DayOfWeek)7]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(-1)]
    public void FromMask_RejectsBitsOutsideTheSevenDays_AndTheEmptySet(int mask)
    {
        Assert.Throws<DomainException>(() => WeekdaySet.FromMask(mask));
    }

    [Fact]
    public void FromMask_RoundTripsTheMask()
    {
        var set = WeekdaySet.Of([DayOfWeek.Tuesday, DayOfWeek.Saturday]);

        Assert.Equal(set, WeekdaySet.FromMask(set.Mask));
        Assert.Equal((1 << (int)DayOfWeek.Tuesday) | (1 << (int)DayOfWeek.Saturday), set.Mask);
    }

    [Fact]
    public void Contains_IsTrueForTheDaysOfTheSetOnly()
    {
        var set = WeekdaySet.Of([DayOfWeek.Monday, DayOfWeek.Wednesday]);

        Assert.True(set.Contains(DayOfWeek.Monday));
        Assert.True(set.Contains(DayOfWeek.Wednesday));
        Assert.False(set.Contains(DayOfWeek.Tuesday));
        Assert.False(set.Contains(DayOfWeek.Sunday));
    }

    [Fact]
    public void IsSubsetOf_ComparesDays()
    {
        var mondayWednesday = WeekdaySet.Of([DayOfWeek.Monday, DayOfWeek.Wednesday]);
        var monday = WeekdaySet.Of([DayOfWeek.Monday]);
        var mondayTuesday = WeekdaySet.Of([DayOfWeek.Monday, DayOfWeek.Tuesday]);

        Assert.True(monday.IsSubsetOf(mondayWednesday));
        Assert.True(mondayWednesday.IsSubsetOf(mondayWednesday));
        Assert.False(mondayWednesday.IsSubsetOf(monday));
        Assert.False(mondayTuesday.IsSubsetOf(mondayWednesday));
    }

    [Fact]
    public void TwoSetsWithTheSameDays_AreEqual()
    {
        Assert.Equal(
            WeekdaySet.Of([DayOfWeek.Monday, DayOfWeek.Friday]),
            WeekdaySet.Of([DayOfWeek.Friday, DayOfWeek.Monday]));
    }

    [Fact]
    public void AllDaysMask_IsEverySevenDays()
    {
        var all = WeekdaySet.FromMask(WeekdaySet.AllDaysMask);

        Assert.Equal(7, all.Days.Count);
        Assert.All(Enum.GetValues<DayOfWeek>(), day => Assert.True(all.Contains(day)));
    }
}
