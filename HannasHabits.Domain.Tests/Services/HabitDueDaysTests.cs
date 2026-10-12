using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Services;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Services;

public class HabitDueDaysTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static readonly HabitSchedule MondayAndThursday = HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Thursday]);

    [Fact]
    public void ReturnsTheDueDaysOfTheRange_OldestFirst_BothEndsIncluded()
    {
        // 5 October 2026 is a Monday, 8 October a Thursday.
        var due = HabitDueDays.Between(MondayAndThursday, D(1, 1), from: D(10, 5), to: D(10, 15));

        Assert.Equal([D(10, 5), D(10, 8), D(10, 12), D(10, 15)], due);
    }

    [Fact]
    public void DaysBeforeTheStartDate_AreNeverDue_EvenIfTheRuleSaysSo()
    {
        var due = HabitDueDays.Between(MondayAndThursday, startDate: D(10, 8), from: D(10, 1), to: D(10, 15));

        Assert.Equal([D(10, 8), D(10, 12), D(10, 15)], due);
    }

    [Fact]
    public void ARangeThatEndsBeforeTheStartDate_IsEmpty()
    {
        Assert.Empty(HabitDueDays.Between(HabitSchedule.Daily, startDate: D(10, 20), from: D(10, 1), to: D(10, 19)));
    }

    [Fact]
    public void ATurnedRoundRange_IsEmpty()
    {
        Assert.Empty(HabitDueDays.Between(HabitSchedule.Daily, D(1, 1), from: D(10, 10), to: D(10, 1)));
    }

    [Fact]
    public void ASingleDay_IsDueOrNot()
    {
        Assert.Equal([D(10, 5)], HabitDueDays.Between(MondayAndThursday, D(1, 1), D(10, 5), D(10, 5)));
        Assert.Empty(HabitDueDays.Between(MondayAndThursday, D(1, 1), D(10, 6), D(10, 6)));
    }

    [Fact]
    public void TheRangeMayCrossMonthsAndRules()
    {
        var lastSaturday = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Saturday);

        var due = HabitDueDays.Between(lastSaturday, D(1, 1), from: D(9, 1), to: D(11, 30));

        Assert.Equal([D(9, 26), D(10, 31), D(11, 28)], due);
    }

    [Fact]
    public void TheLastDayOfTheCalendar_DoesNotOverflow()
    {
        var max = DateOnly.MaxValue;

        Assert.Equal([max], HabitDueDays.Between(HabitSchedule.Daily, startDate: DateOnly.MinValue, from: max, to: max));
    }

    // ---- A sub-habit: the habit's due days that fall on its weekdays ----

    private static readonly WeekdaySet OnlyThursday = WeekdaySet.Of([DayOfWeek.Thursday]);

    [Fact]
    public void ForASubHabit_OnlyTheDueDaysOnItsWeekdaysAreDue()
    {
        // Mondays and Thursdays are due; the sub-habit is planned on Thursdays only.
        var due = HabitDueDays.Between(MondayAndThursday, D(1, 1), OnlyThursday, from: D(10, 5), to: D(10, 15));

        Assert.Equal([D(10, 8), D(10, 15)], due);
    }

    [Fact]
    public void ForASubHabit_AWeekdayTheHabitIsNotDueOn_IsNeverDue()
    {
        var onlyTuesday = WeekdaySet.Of([DayOfWeek.Tuesday]);

        Assert.Empty(HabitDueDays.Between(MondayAndThursday, D(1, 1), onlyTuesday, D(10, 1), D(10, 31)));
        Assert.False(HabitDueDays.IsDue(MondayAndThursday, D(1, 1), onlyTuesday, D(10, 6)));
    }

    [Fact]
    public void ForASubHabit_TheRhythmOfTheHabitCarriesIt()
    {
        // Every second week on Thursday, counted from the week of 5 October: 8 and 22 October are due, 15 October is not.
        var everyOtherWeek = HabitSchedule.Weekly([DayOfWeek.Thursday], everyWeeks: 2, anchor: D(10, 5));

        var due = HabitDueDays.Between(everyOtherWeek, D(1, 1), OnlyThursday, D(10, 1), D(10, 31));

        Assert.Equal([D(10, 8), D(10, 22)], due);
    }

    [Fact]
    public void ForASubHabit_ADayBeforeTheStartDate_IsNotDue()
    {
        Assert.False(HabitDueDays.IsDue(MondayAndThursday, D(10, 9), OnlyThursday, D(10, 8)));
        Assert.True(HabitDueDays.IsDue(MondayAndThursday, D(10, 8), OnlyThursday, D(10, 8)));
    }

    [Fact]
    public void IsDue_AgreesWithBetween_ForEveryDayOfAYear()
    {
        var schedule = HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Second, DayOfWeek.Thursday);
        var weekdays = WeekdaySet.Of([DayOfWeek.Thursday, DayOfWeek.Friday]);
        var start = D(3, 1);

        var between = HabitDueDays.Between(schedule, start, weekdays, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(1))
            Assert.Equal(between.Contains(day), HabitDueDays.IsDue(schedule, start, weekdays, day));
    }
}
