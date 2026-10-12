using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.SubHabits;
using HannasHabits.Application.SubHabits.Commands.ClearSubHabitNote;
using HannasHabits.Application.SubHabits.Commands.SetSubHabitNote;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;
using MediatR;

namespace HannasHabits.Application.Tests.SubHabits;

/// <summary>Shared arrangement: Sport with a part "Legs" on Mondays, and the notes' repository next to the habits.</summary>
public abstract class SubHabitNoteHandlerTestBase : SubHabitHandlerTestBase
{
    private protected readonly FakeSubHabitNoteRepository Notes;

    protected SubHabitNoteHandlerTestBase()
    {
        Notes = new FakeSubHabitNoteRepository(Repository);
    }

    protected (Habit Habit, SubHabit Legs) SportWithLegs(Guid? owner = null)
    {
        var habit = AddSport(owner);
        return (habit, AddPart(habit, "Legs", DayOfWeek.Monday));
    }
}

public class SetSubHabitNoteCommandHandlerTests : SubHabitNoteHandlerTestBase
{
    private Task<SubHabitNoteDto> Handle(Guid habitId, Guid subHabitId, DateOnly date, string? text)
        => new SetSubHabitNoteCommandHandler(Repository, Notes, UnitOfWork, User)
            .Handle(new SetSubHabitNoteCommand(habitId, subHabitId, date, text), CancellationToken.None);

    [Fact]
    public async Task WritesTheNote_AndSavesOnce()
    {
        var (habit, legs) = SportWithLegs();

        var dto = await Handle(habit.Id, legs.Id, Monday, "3 x 10");

        var stored = Assert.Single(Notes.Notes);
        Assert.Equal((legs.Id, Monday, "3 x 10"), (stored.SubHabitId, stored.Date, stored.Text));
        Assert.Equal((Monday, "3 x 10"), (dto.Date, dto.Text));
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ASecondWriteOnTheSameDay_ReplacesTheText_AndStaysOneNote()
    {
        var (habit, legs) = SportWithLegs();
        await Handle(habit.Id, legs.Id, Monday, "first");

        var dto = await Handle(habit.Id, legs.Id, Monday, "second");

        Assert.Equal("second", Assert.Single(Notes.Notes).Text);
        Assert.Equal("second", dto.Text);
    }

    [Fact]
    public async Task NotesOnOtherDaysAndOtherParts_AreNotTouched()
    {
        var (habit, legs) = SportWithLegs();
        var arms = AddPart(habit, "Arms", DayOfWeek.Monday);
        await Handle(habit.Id, legs.Id, Monday, "legs monday");
        await Handle(habit.Id, arms.Id, Monday, "arms monday");
        await Handle(habit.Id, legs.Id, Wednesday, "legs wednesday");

        await Handle(habit.Id, legs.Id, Monday, "legs monday again");

        Assert.Equal(
            ["arms monday", "legs monday again", "legs wednesday"],
            Notes.Notes.Select(n => n.Text).Order());
    }

    [Fact]
    public async Task ADayThePartIsNotDueOn_IsFine_LikeATick()
    {
        var (habit, legs) = SportWithLegs();

        var dto = await Handle(habit.Id, legs.Id, new DateOnly(2026, 10, 6), "stretched instead"); // a Tuesday

        Assert.Equal("stretched instead", dto.Text);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => Handle(Guid.NewGuid(), Guid.NewGuid(), Monday, "x"));

        Assert.Empty(Notes.Notes);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task APartThatIsNotOfTheHabit_IsNotFound()
    {
        var (habit, _) = SportWithLegs();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Guid.NewGuid(), Monday, "x"));

        Assert.Empty(Notes.Notes);
    }

    [Fact]
    public async Task AnotherUsersPart_IsNotFound_AndGetsNoNote()
    {
        var (foreign, foreignLegs) = SportWithLegs(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, foreignLegs.Id, Monday, "x"));

        Assert.Empty(Notes.Notes);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABlankText_IsRefusedByTheDomain_AndNothingIsSaved()
    {
        var (habit, legs) = SportWithLegs();

        await Assert.ThrowsAsync<DomainException>(() => Handle(habit.Id, legs.Id, Monday, "   "));

        Assert.Empty(Notes.Notes);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task WhenAConcurrentRequestWroteTheFirstNote_OursIsPutOnTheirs()
    {
        var (habit, legs) = SportWithLegs();
        var winner = SubHabitNote.Create(legs.Id, Monday, "the winner's text");
        UnitOfWork.BeforeSave = call =>
        {
            if (call != 1) return;

            // What the database did: the other request's insert got there first, ours hit the unique index.
            Notes.Notes.Add(winner);
            throw new DuplicateEntryException(new Exception("unique violation"));
        };

        var dto = await Handle(habit.Id, legs.Id, Monday, "ours");

        Assert.Same(winner, Assert.Single(Notes.Notes));
        Assert.Equal("ours", winner.Text);
        Assert.Equal("ours", dto.Text);
        Assert.Equal(2, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task WhenTheWinnerIsGoneAgain_TheDuplicateIsReported_SoTheClientCanRetry()
    {
        var (habit, legs) = SportWithLegs();
        UnitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));

        await Assert.ThrowsAsync<DuplicateEntryException>(() => Handle(habit.Id, legs.Id, Monday, "ours"));

        Assert.Empty(Notes.Notes);
    }

    [Fact]
    public async Task OtherSaveFailuresAreNotSwallowed()
    {
        var (habit, legs) = SportWithLegs();
        UnitOfWork.BeforeSave = _ => throw new ConflictException("changed by another request");

        await Assert.ThrowsAsync<ConflictException>(() => Handle(habit.Id, legs.Id, Monday, "x"));
    }
}

public class ClearSubHabitNoteCommandHandlerTests : SubHabitNoteHandlerTestBase
{
    private Task<Unit> Handle(Guid habitId, Guid subHabitId, DateOnly date)
        => new ClearSubHabitNoteCommandHandler(Repository, Notes, UnitOfWork, User)
            .Handle(new ClearSubHabitNoteCommand(habitId, subHabitId, date), CancellationToken.None);

    [Fact]
    public async Task TakesTheNoteAway_AndSavesOnce()
    {
        var (habit, legs) = SportWithLegs();
        Notes.Notes.Add(SubHabitNote.Create(legs.Id, Monday, "gone soon"));

        await Handle(habit.Id, legs.Id, Monday);

        Assert.Empty(Notes.Notes);
        Assert.Equal(1, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADayWithoutANote_IsNoError_AndSavesNothing()
    {
        var (habit, legs) = SportWithLegs();

        await Handle(habit.Id, legs.Id, Monday);

        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task OnlyTheNoteOfThatDayGoes()
    {
        var (habit, legs) = SportWithLegs();
        Notes.Notes.Add(SubHabitNote.Create(legs.Id, Monday, "monday"));
        Notes.Notes.Add(SubHabitNote.Create(legs.Id, Wednesday, "wednesday"));

        await Handle(habit.Id, legs.Id, Monday);

        Assert.Equal("wednesday", Assert.Single(Notes.Notes).Text);
    }

    [Fact]
    public async Task AnUnknownPart_IsNotFound()
    {
        var (habit, _) = SportWithLegs();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Guid.NewGuid(), Monday));
    }

    [Fact]
    public async Task AnotherUsersPart_IsNotFound_AndItsNoteStays()
    {
        var (foreign, foreignLegs) = SportWithLegs(owner: Guid.NewGuid());
        Notes.Notes.Add(SubHabitNote.Create(foreignLegs.Id, Monday, "not yours"));

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, foreignLegs.Id, Monday));

        Assert.Single(Notes.Notes);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }
}
