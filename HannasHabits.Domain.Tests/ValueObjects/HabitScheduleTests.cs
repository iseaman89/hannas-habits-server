using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.ValueObjects;

public class HabitScheduleTests
{
    private static readonly DayOfWeek[] AllDays = Enum.GetValues<DayOfWeek>();

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
    public void Create_ReturnsTheDaysInWeekOrderWhateverTheInputOrder()
    {
        var schedule = HabitSchedule.Create([DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Wednesday]);

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], schedule.Days);
    }

    [Fact]
    public void Create_CollapsesDuplicates()
    {
        var schedule = HabitSchedule.Create([DayOfWeek.Monday, DayOfWeek.Monday, DayOfWeek.Monday]);

        Assert.Equal([DayOfWeek.Monday], schedule.Days);
    }

    [Fact]
    public void Create_RejectsAnEmptySchedule()
    {
        Assert.Throws<DomainException>(() => HabitSchedule.Create([]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Create_RejectsANumberThatIsNoDayOfTheWeek(int day)
    {
        Assert.Throws<DomainException>(() => HabitSchedule.Create([DayOfWeek.Monday, (DayOfWeek)day]));
    }

    [Fact]
    public void IncludesDay_IsTrueOnlyForTheScheduledDays()
    {
        var schedule = HabitSchedule.Create([DayOfWeek.Tuesday, DayOfWeek.Saturday]);

        foreach (var day in AllDays)
            Assert.Equal(day is DayOfWeek.Tuesday or DayOfWeek.Saturday, schedule.IncludesDay(day));
    }

    // Every one of the 127 possible schedules: the days that go in are the days that come out.
    [Fact]
    public void EveryNonEmptySetOfDaysRoundTrips()
    {
        for (var mask = 1; mask <= 0b111_1111; mask++)
        {
            var expected = AllDays.Where(day => (mask & (1 << (int)day)) != 0).ToArray();

            var schedule = HabitSchedule.Create(expected);

            Assert.Equal(expected, schedule.Days);
            Assert.All(AllDays, day => Assert.Equal(expected.Contains(day), schedule.IncludesDay(day)));
        }
    }

    [Fact]
    public void SchedulesWithTheSameDaysAreEqual_RegardlessOfOrderAndDuplicates()
    {
        var first = HabitSchedule.Create([DayOfWeek.Monday, DayOfWeek.Tuesday]);
        var second = HabitSchedule.Create([DayOfWeek.Tuesday, DayOfWeek.Monday, DayOfWeek.Monday]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void SchedulesWithDifferentDaysAreNotEqual()
    {
        Assert.NotEqual(
            HabitSchedule.Create([DayOfWeek.Monday]),
            HabitSchedule.Create([DayOfWeek.Tuesday]));
    }

    [Fact]
    public void ADailyScheduleEqualsOneBuiltFromAllSevenDays()
    {
        Assert.Equal(HabitSchedule.Daily, HabitSchedule.Create(AllDays));
    }

    [Fact]
    public void ToString_ListsTheDays()
    {
        Assert.Equal("Monday, Friday", HabitSchedule.Create([DayOfWeek.Friday, DayOfWeek.Monday]).ToString());
    }
}
