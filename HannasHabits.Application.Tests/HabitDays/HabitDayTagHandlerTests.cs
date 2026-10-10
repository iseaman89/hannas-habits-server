using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.HabitDays.Commands.AttachTag;
using HannasHabits.Application.HabitDays.Commands.DetachTag;
using HannasHabits.Application.Tags;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Application.Tests.HabitDays;

public class AttachTagCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeTagRepository _tags = new();
    private readonly FakeHabitDayEntryRepository _days;
    private readonly FakeTagQueries _queries;
    private readonly FakeUnitOfWork _unitOfWork = new();

    public AttachTagCommandHandlerTests()
    {
        _days = new FakeHabitDayEntryRepository(_habits);
        _queries = new FakeTagQueries(_habits, _tags, _days);
    }

    private Task<TaggedDayDto> Handle(Guid habitId, DateOnly date, Guid tagId)
        => new AttachTagCommandHandler(_habits, _tags, _days, _queries, _unitOfWork, _user)
            .Handle(new AttachTagCommand(habitId, date, tagId), CancellationToken.None);

    private Habit AddHabit(Guid? owner = null)
    {
        var habit = Make.NewHabit(owner ?? _user.UserId);
        _habits.Habits.Add(habit);
        return habit;
    }

    private Tag AddTag(string name = "krank", Guid? owner = null)
    {
        var tag = Make.NewTag(owner ?? _user.UserId, name);
        _tags.Tags.Add(tag);
        return tag;
    }

    [Fact]
    public async Task TagsAnUntaggedDay_ByCreatingItsEntry_AndAnswersWithTheDay()
    {
        var habit = AddHabit();
        var tag = AddTag();

        var day = await Handle(habit.Id, Day, tag.Id);

        Assert.Equal(Day, day.Date);
        Assert.Equal([new TagSummaryDto(tag.Id, "krank")], day.Tags);
        var entry = Assert.Single(_days.Entries);
        Assert.Equal(habit.Id, entry.HabitId);
        Assert.Equal(Day, entry.Date);
        Assert.Equal(tag.Id, Assert.Single(entry.Tags).TagId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAlreadyTaggedDay_GetsTheTagOnItsExistingEntry()
    {
        var habit = AddHabit();
        var sick = AddTag("krank");
        var holiday = AddTag("Urlaub");
        var entry = Make.NewDay(habit.Id, Day, holiday);
        _days.Entries.Add(entry);

        var day = await Handle(habit.Id, Day, sick.Id);

        Assert.Same(entry, Assert.Single(_days.Entries));
        Assert.Equal(["krank", "Urlaub"], day.Tags.Select(t => t.Name));
    }

    [Fact]
    public async Task ATagThatIsAlreadyOnTheDay_IsNotAnError_ItAnswersWithTheDay_WithoutSaving()
    {
        var habit = AddHabit();
        var tag = AddTag();
        _days.Entries.Add(Make.NewDay(habit.Id, Day, tag));

        var day = await Handle(habit.Id, Day, tag.Id);

        Assert.Equal([tag.Id], day.Tags.Select(t => t.Id));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheSameTag_CanBeOnTheDaysOfSeveralHabits()
    {
        var run = AddHabit();
        var read = AddHabit();
        var tag = AddTag();

        await Handle(run.Id, Day, tag.Id);
        await Handle(read.Id, Day, tag.Id);

        Assert.Equal(2, _days.Entries.Count);
    }

    [Fact]
    public async Task ADayHoldsAtMostTenTags_TheDomainRejectsTheEleventh()
    {
        var habit = AddHabit();
        var tags = Enumerable.Range(1, HabitDayEntry.MaxTags).Select(i => AddTag($"tag {i}")).ToArray();
        _days.Entries.Add(Make.NewDay(habit.Id, Day, tags));
        var eleventh = AddTag("one too many");

        await Assert.ThrowsAsync<DomainException>(() => Handle(habit.Id, Day, eleventh.Id));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        var tag = AddTag();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), Day, tag.Id));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndNothingIsTagged()
    {
        var foreign = AddHabit(owner: Guid.NewGuid());
        var tag = AddTag();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, Day, tag.Id));

        Assert.Empty(_days.Entries);
    }

    [Fact]
    public async Task AnUnknownTag_IsNotFound()
    {
        var habit = AddHabit();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Day, Guid.NewGuid()));

        Assert.Empty(_days.Entries);
    }

    [Fact]
    public async Task AnotherUsersTag_IsNotFound_AndNothingIsTagged()
    {
        var habit = AddHabit();
        var foreign = AddTag(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Day, foreign.Id));

        Assert.Empty(_days.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADayOutsideThePlausibleRange_IsRejectedByTheDomain()
    {
        var habit = AddHabit();
        var tag = AddTag();

        await Assert.ThrowsAsync<DomainException>(() => Handle(habit.Id, new DateOnly(1999, 12, 31), tag.Id));

        Assert.Empty(_days.Entries);
    }

    // ---- races ----

    [Fact]
    public async Task WhenAConcurrentRequestTaggedTheSameNewDayFirst_OurTagIsAddedToTheEntryThatWon()
    {
        var habit = AddHabit();
        var ours = AddTag("krank");
        var theirs = AddTag("Urlaub");
        HabitDayEntry? winner = null;
        _unitOfWork.BeforeSave = call =>
        {
            if (call != 1)
                return;

            winner = Make.NewDay(habit.Id, Day, theirs);
            _days.Entries.Add(winner);
            throw new DuplicateEntryException(new Exception("unique violation"));
        };

        var day = await Handle(habit.Id, Day, ours.Id);

        Assert.Same(winner, Assert.Single(_days.Entries));  // our own new entry was forgotten
        Assert.Equal(["krank", "Urlaub"], day.Tags.Select(t => t.Name));
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task WhenAConcurrentRequestPutTheSameTagOnTheDay_TheGoalIsReached_NothingMoreIsSaved()
    {
        var habit = AddHabit();
        var tag = AddTag();
        _unitOfWork.BeforeSave = _ =>
        {
            _days.Entries.Add(Make.NewDay(habit.Id, Day, tag));
            throw new DuplicateEntryException(new Exception("unique violation"));
        };

        var day = await Handle(habit.Id, Day, tag.Id);

        Assert.Equal([tag.Id], day.Tags.Select(t => t.Id));
        Assert.Single(_days.Entries);
        Assert.Equal(1, _unitOfWork.SaveCount); // the failed one; there was nothing left to save
    }

    [Fact]
    public async Task WhenTheWinnerIsGoneAgain_TheDuplicateIsReported_SoTheClientCanRetry()
    {
        var habit = AddHabit();
        var tag = AddTag();
        _unitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));

        await Assert.ThrowsAsync<DuplicateEntryException>(() => Handle(habit.Id, Day, tag.Id));
    }
}

public class DetachTagCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeTagRepository _tags = new();
    private readonly FakeHabitDayEntryRepository _days;
    private readonly FakeTagQueries _queries;
    private readonly FakeUnitOfWork _unitOfWork = new();

    public DetachTagCommandHandlerTests()
    {
        _days = new FakeHabitDayEntryRepository(_habits);
        _queries = new FakeTagQueries(_habits, _tags, _days);
    }

    private Task<TaggedDayDto> Handle(Guid habitId, DateOnly date, Guid tagId)
        => new DetachTagCommandHandler(_habits, _tags, _days, _queries, _unitOfWork, _user)
            .Handle(new DetachTagCommand(habitId, date, tagId), CancellationToken.None);

    private Habit AddHabit(Guid? owner = null)
    {
        var habit = Make.NewHabit(owner ?? _user.UserId);
        _habits.Habits.Add(habit);
        return habit;
    }

    private Tag AddTag(string name = "krank", Guid? owner = null)
    {
        var tag = Make.NewTag(owner ?? _user.UserId, name);
        _tags.Tags.Add(tag);
        return tag;
    }

    [Fact]
    public async Task TakesTheTagOffTheDay_TheOtherTagsStay()
    {
        var habit = AddHabit();
        var sick = AddTag("krank");
        var holiday = AddTag("Urlaub");
        var entry = Make.NewDay(habit.Id, Day, sick, holiday);
        _days.Entries.Add(entry);

        var day = await Handle(habit.Id, Day, sick.Id);

        Assert.Equal([holiday.Id], day.Tags.Select(t => t.Id));
        Assert.Same(entry, Assert.Single(_days.Entries));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task TheLastTag_TakesTheEntryWithIt_AnEntryIsNeverStoredEmpty()
    {
        var habit = AddHabit();
        var tag = AddTag();
        var entry = Make.NewDay(habit.Id, Day, tag);
        _days.Entries.Add(entry);

        var day = await Handle(habit.Id, Day, tag.Id);

        Assert.Empty(day.Tags);
        Assert.Empty(_days.Entries);
        Assert.Same(entry, Assert.Single(_days.Removed));
    }

    [Fact]
    public async Task ATagThatIsNotOnTheDay_IsNotAnError_WithoutSaving()
    {
        var habit = AddHabit();
        var tag = AddTag();

        var day = await Handle(habit.Id, Day, tag.Id);

        Assert.Equal(Day, day.Date);
        Assert.Empty(day.Tags);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        var tag = AddTag();

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), Day, tag.Id));
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndItsDayIsUntouched()
    {
        var foreign = AddHabit(owner: Guid.NewGuid());
        var tag = AddTag();
        var entry = Make.NewDay(foreign.Id, Day, tag);
        _days.Entries.Add(entry);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, Day, tag.Id));

        Assert.True(entry.HasTag(tag.Id));
    }

    [Fact]
    public async Task AnUnknownOrForeignTag_IsNotFound()
    {
        var habit = AddHabit();
        var foreign = AddTag(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Day, Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Day, foreign.Id));
    }

    [Fact]
    public async Task WhenAConcurrentRequestTookTheSameTagOffFirst_TheGoalIsReached_NoError()
    {
        var habit = AddHabit();
        var tag = AddTag();
        _days.Entries.Add(Make.NewDay(habit.Id, Day, tag));
        _unitOfWork.BeforeSave = _ => throw new ConflictException("changed by another request");

        var day = await Handle(habit.Id, Day, tag.Id);

        Assert.Equal(Day, day.Date);
    }
}
