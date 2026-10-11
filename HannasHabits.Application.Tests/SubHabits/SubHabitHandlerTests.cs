using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.SubHabits;
using HannasHabits.Application.SubHabits.Commands.AddSubHabit;
using HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;
using HannasHabits.Application.SubHabits.Commands.RemoveSubHabit;
using HannasHabits.Application.SubHabits.Commands.UnmarkSubHabitCompleted;
using HannasHabits.Application.SubHabits.Commands.UpdateSubHabit;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tests.SubHabits;

/// <summary>Shared arrangement: Sport on Monday, Wednesday and Friday of the signed-in user.</summary>
public abstract class SubHabitHandlerTestBase
{
    // October 2026: the 5th is a Monday.
    protected static readonly DateOnly Monday = new(2026, 10, 5);
    protected static readonly DateOnly Wednesday = new(2026, 10, 7);

    private protected readonly FakeCurrentUser User = new();
    private protected readonly FakeHabitRepository Repository = new();
    private protected readonly FakeHabitQueries Queries;
    private protected readonly FakeUnitOfWork UnitOfWork = new();

    protected SubHabitHandlerTestBase()
    {
        Queries = new FakeHabitQueries(Repository);
    }

    protected Habit AddSport(Guid? owner = null)
    {
        var habit = Make.NewHabit(owner ?? User.UserId, "Sport", new DateOnly(2026, 9, 1),
            HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]));
        Repository.Habits.Add(habit);
        return habit;
    }

    protected static SubHabit AddPart(Habit habit, string title, params DayOfWeek[] days)
        => habit.AddSubHabit(HabitTitle.Create(title), WeekdaySet.Of(days));

    // Records compare their lists by reference, so the answers are compared fact by fact.
    protected static void AssertDay(HabitDayDto day, DateOnly date, bool habitCompleted, params Guid[] completedSubHabitIds)
    {
        Assert.Equal(date, day.Date);
        Assert.Equal(habitCompleted, day.HabitCompleted);
        Assert.Equal(completedSubHabitIds, day.CompletedSubHabitIds);
    }

    protected static void AssertSubHabit(SubHabitDto dto, Guid id, string title, params DayOfWeek[] weekdays)
    {
        Assert.Equal(id, dto.Id);
        Assert.Equal(title, dto.Title);
        Assert.Equal(weekdays, dto.Weekdays);
    }
}

public class AddSubHabitCommandHandlerTests : SubHabitHandlerTestBase
{
    private Task<SubHabitDto> Handle(Guid habitId, string title, params DayOfWeek[] days)
        => new AddSubHabitCommandHandler(Repository, UnitOfWork, User)
            .Handle(new AddSubHabitCommand(habitId, title, days.ToList()), CancellationToken.None);

    [Fact]
    public async Task AddsThePart_AndSavesOnce()
    {
        var habit = AddSport();

        var dto = await Handle(habit.Id, "Legs", DayOfWeek.Monday);

        var stored = Assert.Single(habit.SubHabits);
        AssertSubHabit(dto, stored.Id, "Legs", DayOfWeek.Monday);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), "Legs", DayOfWeek.Monday));

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndGetsNothing()
    {
        var foreign = AddSport(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, "Legs", DayOfWeek.Monday));

        Assert.Empty(foreign.SubHabits);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ATitleTheHabitAlreadyHas_IsRefusedByTheDomain_AndNothingIsSaved()
    {
        var habit = AddSport();
        AddPart(habit, "Legs", DayOfWeek.Monday);

        await Assert.ThrowsAsync<DomainException>(() => Handle(habit.Id, "legs", DayOfWeek.Wednesday));

        Assert.Single(habit.SubHabits);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADayTheHabitIsNotPlannedOn_IsRefusedByTheDomain()
    {
        var habit = AddSport();

        await Assert.ThrowsAsync<DomainException>(() => Handle(habit.Id, "Legs", DayOfWeek.Tuesday));

        Assert.Empty(habit.SubHabits);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}

public class UpdateSubHabitCommandHandlerTests : SubHabitHandlerTestBase
{
    private Task<SubHabitDto> Handle(Guid habitId, Guid subHabitId, string title, params DayOfWeek[] days)
        => new UpdateSubHabitCommandHandler(Repository, UnitOfWork, User)
            .Handle(new UpdateSubHabitCommand(habitId, subHabitId, title, days.ToList()), CancellationToken.None);

    [Fact]
    public async Task ChangesTheTitleAndTheDays_AndSavesOnce()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);

        var dto = await Handle(habit.Id, legs.Id, "Legs & glutes", DayOfWeek.Wednesday, DayOfWeek.Friday);

        AssertSubHabit(dto, legs.Id, "Legs & glutes", DayOfWeek.Wednesday, DayOfWeek.Friday);
        Assert.Equal("Legs & glutes", legs.Title.Value);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownSubHabit_IsNotFound()
    {
        var habit = AddSport();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Guid.NewGuid(), "Legs", DayOfWeek.Monday));

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ASubHabitOfAnotherUsersHabit_IsNotFound_AndStaysAsItWas()
    {
        var foreign = AddSport(owner: Guid.NewGuid());
        var legs = AddPart(foreign, "Legs", DayOfWeek.Monday);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, legs.Id, "Mine now", DayOfWeek.Monday));

        Assert.Equal("Legs", legs.Title.Value);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ASubHabitIsNotFoundThroughAnotherHabitOfTheSameUser()
    {
        var sport = AddSport();
        var other = Make.NewHabit(User.UserId, "Read");
        Repository.Habits.Add(other);
        var legs = AddPart(sport, "Legs", DayOfWeek.Monday);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(other.Id, legs.Id, "Legs", DayOfWeek.Monday));
    }
}

public class RemoveSubHabitCommandHandlerTests : SubHabitHandlerTestBase
{
    private Task Handle(Guid habitId, Guid subHabitId)
        => new RemoveSubHabitCommandHandler(Repository, UnitOfWork, User)
            .Handle(new RemoveSubHabitCommand(habitId, subHabitId), CancellationToken.None);

    [Fact]
    public async Task RemovesThePart_AndSavesOnce()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        var arms = AddPart(habit, "Arms", DayOfWeek.Wednesday);

        await Handle(habit.Id, legs.Id);

        Assert.Same(arms, Assert.Single(habit.SubHabits));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownSubHabit_IsNotFound()
    {
        var habit = AddSport();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Guid.NewGuid()));

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersSubHabit_IsNotFound_AndStays()
    {
        var foreign = AddSport(owner: Guid.NewGuid());
        var legs = AddPart(foreign, "Legs", DayOfWeek.Monday);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, legs.Id));

        Assert.Single(foreign.SubHabits);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}

public class MarkSubHabitCompletedCommandHandlerTests : SubHabitHandlerTestBase
{
    private Task<HabitDayDto> Handle(Guid habitId, Guid subHabitId, DateOnly date)
        => new MarkSubHabitCompletedCommandHandler(Repository, Queries, UnitOfWork, User)
            .Handle(new MarkSubHabitCompletedCommand(habitId, subHabitId, date), CancellationToken.None);

    [Fact]
    public async Task TicksThePart_AndCompletesTheHabitsDay_WhenItWasTheLastOne()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);

        var day = await Handle(habit.Id, legs.Id, Monday);

        AssertDay(day, Monday, habitCompleted: true, legs.Id);
        Assert.True(legs.IsCompletedOn(Monday));
        Assert.NotNull(habit.RecordOn(Monday));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task WhenAnotherDuePartIsOpen_TheHabitsDayStaysOpen()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        AddPart(habit, "Core", DayOfWeek.Monday);

        var day = await Handle(habit.Id, legs.Id, Monday);

        AssertDay(day, Monday, habitCompleted: false, legs.Id);
        Assert.Null(habit.RecordOn(Monday));
    }

    [Fact]
    public async Task TickingAPartThatIsAlreadyDone_IsNotAnError_ItAnswersWithTheDay_WithoutSaving()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        habit.MarkSubHabitCompleted(legs.Id, Monday);

        var day = await Handle(habit.Id, legs.Id, Monday);

        AssertDay(day, Monday, habitCompleted: true, legs.Id);
        Assert.Single(legs.Records);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), Guid.NewGuid(), Monday));

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownSubHabit_IsNotFound()
    {
        var habit = AddSport();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Guid.NewGuid(), Monday));

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersSubHabit_IsNotFound_AndIsNotTicked()
    {
        var foreign = AddSport(owner: Guid.NewGuid());
        var legs = AddPart(foreign, "Legs", DayOfWeek.Monday);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, legs.Id, Monday));

        Assert.Empty(legs.Records);
        Assert.Empty(foreign.Records);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    // The unique index (sub-habit, day) rejects the second of two concurrent ticks.
    [Fact]
    public async Task WhenAConcurrentRequestWonTheRace_TheCallerGetsTheDayAsItIsNow()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        var winnersDay = new HabitDayDto(Monday, HabitCompleted: true, [legs.Id]);
        UnitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));
        Queries.DayOverride = () => winnersDay;

        var day = await Handle(habit.Id, legs.Id, Monday);

        Assert.Same(winnersDay, day);
    }

    [Theory]
    [InlineData(true)]  // the winner took its tick back again
    [InlineData(false)] // the habit itself is gone
    public async Task WhenTheWinnerIsGoneAgain_TheDuplicateIsReported_SoTheClientCanRetry(bool habitStillThere)
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        UnitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));
        Queries.DayOverride = () => habitStillThere ? new HabitDayDto(Monday, false, []) : null;

        await Assert.ThrowsAsync<DuplicateEntryException>(() => Handle(habit.Id, legs.Id, Monday));
    }

    [Fact]
    public async Task OtherSaveFailuresAreNotSwallowed()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        UnitOfWork.BeforeSave = _ => throw new ConflictException("changed by another request");

        await Assert.ThrowsAsync<ConflictException>(() => Handle(habit.Id, legs.Id, Monday));
    }
}

public class UnmarkSubHabitCompletedCommandHandlerTests : SubHabitHandlerTestBase
{
    private Task<HabitDayDto> Handle(Guid habitId, Guid subHabitId, DateOnly date)
        => new UnmarkSubHabitCompletedCommandHandler(Repository, UnitOfWork, User)
            .Handle(new UnmarkSubHabitCompletedCommand(habitId, subHabitId, date), CancellationToken.None);

    [Fact]
    public async Task TakesThePartBack_AndTheHabitsDayWithIt()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        habit.MarkSubHabitCompleted(legs.Id, Monday);

        var day = await Handle(habit.Id, legs.Id, Monday);

        AssertDay(day, Monday, habitCompleted: false);
        Assert.Empty(legs.Records);
        Assert.Empty(habit.Records);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UntickingAPartThatIsNotDone_IsNotAnError_ItAnswersWithTheDay_WithoutSaving()
    {
        var habit = AddSport();
        var legs = AddPart(habit, "Legs", DayOfWeek.Monday);
        var arms = AddPart(habit, "Arms", DayOfWeek.Wednesday);
        habit.MarkSubHabitCompleted(arms.Id, Wednesday);

        var day = await Handle(habit.Id, legs.Id, Wednesday);

        AssertDay(day, Wednesday, habitCompleted: true, arms.Id);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownSubHabit_IsNotFound()
    {
        var habit = AddSport();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Guid.NewGuid(), Monday));

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersSubHabit_IsNotFound_AndKeepsItsTick()
    {
        var foreign = AddSport(owner: Guid.NewGuid());
        var legs = AddPart(foreign, "Legs", DayOfWeek.Monday);
        foreign.MarkSubHabitCompleted(legs.Id, Monday);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, legs.Id, Monday));

        Assert.Single(legs.Records);
        Assert.NotNull(foreign.RecordOn(Monday));
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}
