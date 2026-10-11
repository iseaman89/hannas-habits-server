using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Domain.Tests.Entities;

/// <summary>The sub-habits of a habit and the rule "the habit's day follows its due sub-habits".</summary>
public class HabitSubHabitTests
{
    // October 2026: the 5th is a Monday.
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateOnly Tuesday = new(2026, 10, 6);
    private static readonly DateOnly Wednesday = new(2026, 10, 7);
    private static readonly DateOnly Friday = new(2026, 10, 9);
    private static readonly DateOnly NextMonday = new(2026, 10, 12);

    private static readonly HabitSchedule MondayWednesdayFriday =
        HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);

    private static Habit NewHabit(HabitSchedule? schedule = null, DateOnly? start = null)
        => Habit.Create(Guid.NewGuid(), HabitTitle.Create("Sport"), start ?? Start, null, schedule ?? MondayWednesdayFriday);

    private static HabitTitle Title(string text) => HabitTitle.Create(text);

    private static WeekdaySet On(params DayOfWeek[] days) => WeekdaySet.Of(days);

    private static SubHabit AddLegsOnMonday(Habit habit)
        => habit.AddSubHabit(Title("Legs"), On(DayOfWeek.Monday));

    // ---- Add ----

    [Fact]
    public void ANewHabit_HasNoSubHabits()
    {
        Assert.Empty(NewHabit().SubHabits);
    }

    [Fact]
    public void AddSubHabit_AddsAPartOfTheHabit()
    {
        var habit = NewHabit();

        var legs = habit.AddSubHabit(Title(" Legs "), On(DayOfWeek.Monday, DayOfWeek.Friday));

        Assert.Same(legs, Assert.Single(habit.SubHabits));
        Assert.Equal(habit.Id, legs.HabitId);
        Assert.Equal("Legs", legs.Title.Value);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], legs.Weekdays.Days);
        Assert.Empty(legs.Records);
        Assert.NotEqual(Guid.Empty, legs.Id);
    }

    [Fact]
    public void AddSubHabit_RejectsATitleTheHabitAlreadyHas_IgnoringCase()
    {
        var habit = NewHabit();
        AddLegsOnMonday(habit);

        Assert.Throws<DomainException>(() => habit.AddSubHabit(Title("legs"), On(DayOfWeek.Wednesday)));
        Assert.Single(habit.SubHabits);
    }

    [Fact]
    public void AddSubHabit_AllowsAtMostTen_AndAnElevenInvitesAnError()
    {
        var habit = NewHabit(HabitSchedule.Daily);

        for (var i = 0; i < Habit.MaxSubHabits; i++)
            habit.AddSubHabit(Title($"Part {i}"), On(DayOfWeek.Monday));

        Assert.Throws<DomainException>(() => habit.AddSubHabit(Title("One more"), On(DayOfWeek.Monday)));
        Assert.Equal(Habit.MaxSubHabits, habit.SubHabits.Count);
    }

    [Fact]
    public void AddSubHabit_RejectsADayTheHabitIsNotPlannedOn()
    {
        var habit = NewHabit();

        Assert.Throws<DomainException>(() => habit.AddSubHabit(Title("Legs"), On(DayOfWeek.Tuesday)));
        Assert.Throws<DomainException>(() => habit.AddSubHabit(Title("Legs"), On(DayOfWeek.Monday, DayOfWeek.Tuesday)));
        Assert.Empty(habit.SubHabits);
    }

    [Fact]
    public void AddSubHabit_OnAHabitWithoutFixedWeekdays_AcceptsAnyDay()
    {
        // "Every 3 days", "the 15th" and a quota do not depend on the weekday: the rule decides, the weekdays narrow it.
        foreach (var schedule in new[]
                 {
                     HabitSchedule.EveryNDays(3, Start), HabitSchedule.MonthlyOnDate(15), HabitSchedule.TimesPerWeek(3)
                 })
        {
            var habit = NewHabit(schedule);

            habit.AddSubHabit(Title("Legs"), On(DayOfWeek.Saturday));

            Assert.Single(habit.SubHabits);
        }
    }

    [Fact]
    public void AddSubHabit_OnAMonthlyWeekdayHabit_AcceptsThatWeekdayOnly()
    {
        var habit = NewHabit(HabitSchedule.MonthlyOnWeekday(WeekdayOccurrence.Last, DayOfWeek.Saturday));

        habit.AddSubHabit(Title("Long run"), On(DayOfWeek.Saturday));

        Assert.Throws<DomainException>(() => habit.AddSubHabit(Title("Sprint"), On(DayOfWeek.Sunday)));
    }

    // ---- Update / remove ----

    [Fact]
    public void UpdateSubHabit_RenamesAndMovesIt()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        habit.UpdateSubHabit(legs.Id, Title("Legs & glutes"), On(DayOfWeek.Wednesday));

        Assert.Equal("Legs & glutes", legs.Title.Value);
        Assert.Equal([DayOfWeek.Wednesday], legs.Weekdays.Days);
    }

    [Fact]
    public void UpdateSubHabit_MayChangeOnlyTheCaseOfItsOwnTitle()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        habit.UpdateSubHabit(legs.Id, Title("LEGS"), legs.Weekdays);

        Assert.Equal("LEGS", legs.Title.Value);
    }

    [Fact]
    public void UpdateSubHabit_RejectsTheTitleOfAnotherSubHabit_AndChangesNothing()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.AddSubHabit(Title("Arms"), On(DayOfWeek.Wednesday));

        Assert.Throws<DomainException>(() => habit.UpdateSubHabit(legs.Id, Title("arms"), On(DayOfWeek.Friday)));

        Assert.Equal("Legs", legs.Title.Value);
        Assert.Equal([DayOfWeek.Monday], legs.Weekdays.Days);
    }

    [Fact]
    public void UpdateSubHabit_RejectsADayTheHabitIsNotPlannedOn_AndChangesNothing()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        Assert.Throws<DomainException>(() => habit.UpdateSubHabit(legs.Id, Title("Quads"), On(DayOfWeek.Tuesday)));

        Assert.Equal("Legs", legs.Title.Value);
        Assert.Equal([DayOfWeek.Monday], legs.Weekdays.Days);
    }

    [Fact]
    public void UpdateSubHabit_CanRenameASubHabitWhoseDayLeftTheHabitsRhythm()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.Update(HabitTitle.Create("Sport"), null, HabitSchedule.Weekly([DayOfWeek.Tuesday]));

        habit.UpdateSubHabit(legs.Id, Title("Quads"), legs.Weekdays);

        Assert.Equal("Quads", legs.Title.Value);
        Assert.Equal([DayOfWeek.Monday], legs.Weekdays.Days);
    }

    [Fact]
    public void UpdateSubHabit_OfAnUnknownSubHabit_Throws()
    {
        Assert.Throws<DomainException>(() => NewHabit().UpdateSubHabit(Guid.NewGuid(), Title("Legs"), On(DayOfWeek.Monday)));
    }

    [Fact]
    public void RemoveSubHabit_TakesItWithItsMarks_AndLeavesTheHabitsDays()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.MarkCompleted(Monday);

        habit.RemoveSubHabit(legs.Id);

        Assert.Empty(habit.SubHabits);
        Assert.NotNull(habit.RecordOn(Monday));
    }

    [Fact]
    public void RemoveSubHabit_OfAnUnknownSubHabit_Throws()
    {
        Assert.Throws<DomainException>(() => NewHabit().RemoveSubHabit(Guid.NewGuid()));
    }

    // ---- Due ----

    [Fact]
    public void ASubHabit_IsDueWhereTheHabitIsDueAndTheWeekdayMatches()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        Assert.True(habit.IsDueOn(legs, Monday));
        Assert.True(habit.IsDueOn(legs, NextMonday));
        Assert.False(habit.IsDueOn(legs, Wednesday)); // the habit is due, the sub-habit is not planned on Wednesdays
        Assert.False(habit.IsDueOn(legs, Tuesday)); // neither is due
    }

    [Fact]
    public void ASubHabit_IsNotDueBeforeTheHabitStarted()
    {
        var habit = NewHabit(start: Monday);
        var legs = AddLegsOnMonday(habit);

        Assert.False(habit.IsDueOn(legs, Monday.AddDays(-7)));
        Assert.True(habit.IsDueOn(legs, Monday));
    }

    [Fact]
    public void ASubHabit_FollowsAChangeOfTheHabitsRhythm()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        habit.Update(HabitTitle.Create("Sport"), null, HabitSchedule.Weekly([DayOfWeek.Monday], everyWeeks: 2, anchor: Monday));

        Assert.True(habit.IsDueOn(legs, Monday));
        Assert.False(habit.IsDueOn(legs, NextMonday));
        Assert.True(habit.IsDueOn(legs, NextMonday.AddDays(7)));
    }

    [Fact]
    public void DueSubHabitsOn_ListsThePartsOfTheDay()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        var core = habit.AddSubHabit(Title("Core"), On(DayOfWeek.Monday, DayOfWeek.Friday));
        habit.AddSubHabit(Title("Arms"), On(DayOfWeek.Wednesday));

        Assert.Equal([legs, core], habit.DueSubHabitsOn(Monday));
        Assert.Equal([core], habit.DueSubHabitsOn(Friday));
        Assert.Empty(habit.DueSubHabitsOn(Tuesday));
    }

    // ---- The habit's own tick ----

    [Fact]
    public void MarkingTheHabit_TicksTheSubHabitsThatAreDueThatDay_AndNoOthers()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        var core = habit.AddSubHabit(Title("Core"), On(DayOfWeek.Monday));
        var arms = habit.AddSubHabit(Title("Arms"), On(DayOfWeek.Wednesday));

        habit.MarkCompleted(Monday);

        Assert.True(legs.IsCompletedOn(Monday));
        Assert.True(core.IsCompletedOn(Monday));
        Assert.False(arms.IsCompletedOn(Monday));
    }

    [Fact]
    public void MarkingTheHabit_KeepsASubHabitMarkThatIsAlreadyThere()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.AddSubHabit(Title("Core"), On(DayOfWeek.Monday));
        habit.MarkSubHabitCompleted(legs.Id, Monday);
        var legsRecord = Assert.Single(legs.Records);

        habit.MarkCompleted(Monday);

        Assert.Same(legsRecord, Assert.Single(legs.Records));
    }

    [Fact]
    public void MarkingTheHabit_OnADayWithoutDueSubHabits_TicksNone()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        habit.MarkCompleted(Wednesday);

        Assert.Empty(legs.Records);
    }

    [Fact]
    public void UnmarkingTheHabit_ClearsTheMarksOfThatDay_OnlyThatDay()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.MarkCompleted(Monday);
        habit.MarkCompleted(NextMonday);

        habit.UnmarkCompleted(Monday);

        Assert.False(legs.IsCompletedOn(Monday));
        Assert.True(legs.IsCompletedOn(NextMonday));
    }

    [Fact]
    public void UnmarkingTheHabit_AlsoClearsAMarkOfASubHabitThatWasNotDue()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.MarkSubHabitCompleted(legs.Id, Wednesday); // permissive: not due on Wednesdays
        habit.MarkCompleted(Wednesday);

        habit.UnmarkCompleted(Wednesday);

        Assert.Empty(legs.Records);
    }

    // ---- A sub-habit's tick ----

    [Fact]
    public void TickingTheOnlyDueSubHabit_CompletesTheHabitsDay()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        var record = habit.MarkSubHabitCompleted(legs.Id, Monday);

        Assert.Equal(Monday, record.Date);
        Assert.Equal(legs.Id, record.SubHabitId);
        Assert.NotNull(habit.RecordOn(Monday));
    }

    [Fact]
    public void TheHabitsDayIsDone_OnlyWhenAllItsDueSubHabitsAre()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        var core = habit.AddSubHabit(Title("Core"), On(DayOfWeek.Monday));

        habit.MarkSubHabitCompleted(legs.Id, Monday);
        Assert.Null(habit.RecordOn(Monday));

        habit.MarkSubHabitCompleted(core.Id, Monday);
        Assert.NotNull(habit.RecordOn(Monday));
        Assert.Single(habit.Records);
    }

    [Fact]
    public void TickingASubHabitOnADayItIsNotDueOn_IsOnlyAMark_AndNeverCompletesTheHabit()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        habit.MarkSubHabitCompleted(legs.Id, Wednesday);

        Assert.True(legs.IsCompletedOn(Wednesday));
        Assert.Null(habit.RecordOn(Wednesday));
    }

    [Fact]
    public void TickingASubHabit_OnADayWhereAnotherDueOneIsOpen_LeavesTheHabitOpen()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.AddSubHabit(Title("Core"), On(DayOfWeek.Monday));
        var arms = habit.AddSubHabit(Title("Arms"), On(DayOfWeek.Wednesday));

        habit.MarkSubHabitCompleted(arms.Id, Monday); // not due on Monday: does not count towards Monday

        Assert.Null(habit.RecordOn(Monday));
        habit.MarkSubHabitCompleted(legs.Id, Monday);
        Assert.Null(habit.RecordOn(Monday));
    }

    [Fact]
    public void TickingASubHabitTwice_Throws()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.MarkSubHabitCompleted(legs.Id, Monday);

        Assert.Throws<DomainException>(() => habit.MarkSubHabitCompleted(legs.Id, Monday));
    }

    [Fact]
    public void TickingAnUnknownSubHabit_Throws()
    {
        Assert.Throws<DomainException>(() => NewHabit().MarkSubHabitCompleted(Guid.NewGuid(), Monday));
    }

    [Fact]
    public void TickingTheLastDueSubHabit_OfADayTheHabitWasAlreadyDoneOn_AddsNoSecondRecord()
    {
        var habit = NewHabit();
        habit.MarkCompleted(Monday); // done before there were any parts
        var legs = AddLegsOnMonday(habit);

        habit.MarkSubHabitCompleted(legs.Id, Monday);

        Assert.Single(habit.Records);
    }

    [Fact]
    public void UntickingASubHabit_TakesTheHabitsDayBack()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        habit.MarkSubHabitCompleted(legs.Id, Monday);

        habit.UnmarkSubHabitCompleted(legs.Id, Monday);

        Assert.False(legs.IsCompletedOn(Monday));
        Assert.Null(habit.RecordOn(Monday));
    }

    [Fact]
    public void UntickingOneOfTwoSubHabits_KeepsTheOtherOne_AndTakesTheDayBack()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        var core = habit.AddSubHabit(Title("Core"), On(DayOfWeek.Monday));
        habit.MarkCompleted(Monday);

        habit.UnmarkSubHabitCompleted(core.Id, Monday);

        Assert.True(legs.IsCompletedOn(Monday));
        Assert.False(core.IsCompletedOn(Monday));
        Assert.Null(habit.RecordOn(Monday));
    }

    [Fact]
    public void UntickingASubHabitThatWasNotDue_LeavesTheHabitsDay()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);
        var arms = habit.AddSubHabit(Title("Arms"), On(DayOfWeek.Wednesday));
        habit.MarkSubHabitCompleted(arms.Id, Monday);
        habit.MarkSubHabitCompleted(legs.Id, Monday);
        Assert.NotNull(habit.RecordOn(Monday));

        habit.UnmarkSubHabitCompleted(arms.Id, Monday);

        Assert.NotNull(habit.RecordOn(Monday));
    }

    [Fact]
    public void UntickingASubHabitThatIsNotDone_Throws()
    {
        var habit = NewHabit();
        var legs = AddLegsOnMonday(habit);

        Assert.Throws<DomainException>(() => habit.UnmarkSubHabitCompleted(legs.Id, Monday));
    }

    [Fact]
    public void AQuotaHabit_FollowsItsSubHabitsToo()
    {
        var habit = NewHabit(HabitSchedule.TimesPerWeek(2));
        var legs = habit.AddSubHabit(Title("Legs"), On(DayOfWeek.Monday));

        habit.MarkSubHabitCompleted(legs.Id, Monday);

        Assert.NotNull(habit.RecordOn(Monday));
    }

    // ---- The invariant, under any order of ticks ----

    [Fact]
    public void TheHabitsDay_AlwaysFollowsItsDueSubHabits_UnderAnySequenceOfTicks()
    {
        // Whatever mix of ticks the person makes - the habit itself, a part, on due and on other days - a day with at
        // least one due sub-habit is done exactly when all of its due sub-habits are done. The reference is the
        // definition, not the implementation.
        var random = new Random(20261011);
        var days = Enumerable.Range(0, 14).Select(i => Monday.AddDays(i)).ToList();

        for (var run = 0; run < 200; run++)
        {
            var habit = NewHabit(HabitSchedule.Weekly(
                [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday]));
            var all = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday };

            for (var i = 0; i < 3; i++)
            {
                var chosen = all.Where(_ => random.Next(2) == 0).DefaultIfEmpty(all[random.Next(all.Length)]);
                habit.AddSubHabit(Title($"Part {i}"), WeekdaySet.Of(chosen));
            }

            for (var step = 0; step < 60; step++)
            {
                var day = days[random.Next(days.Count)];
                var subHabit = habit.SubHabits.ElementAt(random.Next(habit.SubHabits.Count));

                switch (random.Next(4))
                {
                    case 0 when habit.RecordOn(day) is null:
                        habit.MarkCompleted(day);
                        break;
                    case 1 when habit.RecordOn(day) is not null:
                        habit.UnmarkCompleted(day);
                        break;
                    case 2 when !subHabit.IsCompletedOn(day):
                        habit.MarkSubHabitCompleted(subHabit.Id, day);
                        break;
                    case 3 when subHabit.IsCompletedOn(day):
                        habit.UnmarkSubHabitCompleted(subHabit.Id, day);
                        break;
                }

                foreach (var checkedDay in days)
                {
                    var due = habit.DueSubHabitsOn(checkedDay);

                    if (due.Count == 0)
                        continue;

                    Assert.Equal(due.All(s => s.IsCompletedOn(checkedDay)), habit.RecordOn(checkedDay) is not null);
                }
            }
        }
    }
}
