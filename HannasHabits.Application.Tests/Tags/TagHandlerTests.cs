using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Tags;
using HannasHabits.Application.Tags.Commands.CreateTag;
using HannasHabits.Application.Tags.Commands.DeleteTag;
using HannasHabits.Application.Tags.Commands.RenameTag;
using HannasHabits.Application.Tags.Queries.GetTags;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Exceptions;

namespace HannasHabits.Application.Tests.Tags;

public class CreateTagCommandHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeTagRepository _tags = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task<CreateTagResult> Handle(string? name)
        => new CreateTagCommandHandler(_tags, _unitOfWork, _user)
            .Handle(new CreateTagCommand(name), CancellationToken.None);

    [Fact]
    public async Task CreatesTheTag_TrimmedAndOwnedByTheUser_AndSavesOnce()
    {
        var result = await Handle("  krank ");

        Assert.True(result.Created);
        Assert.Equal("krank", result.Tag.Name);
        var tag = Assert.Single(_tags.Tags);
        Assert.Equal(tag.Id, result.Tag.Id);
        Assert.Equal(_user.UserId, tag.UserId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData("krank")]
    [InlineData("KRANK")]
    [InlineData("  Krank  ")]
    public async Task ANameThatExists_IgnoringCase_IsNotAnError_ItReturnsTheExistingTag_WithoutSaving(string typed)
    {
        var existing = Make.NewTag(_user.UserId, "krank");
        _tags.Tags.Add(existing);

        var result = await Handle(typed);

        Assert.False(result.Created);
        Assert.Equal(existing.Id, result.Tag.Id);
        Assert.Equal("krank", result.Tag.Name); // the spelling the tag was stored with, not the typed one
        Assert.Single(_tags.Tags);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ASimilarNameOfAnotherUser_DoesNotCount()
    {
        _tags.Tags.Add(Make.NewTag(Guid.NewGuid(), "krank"));

        var result = await Handle("krank");

        Assert.True(result.Created);
        Assert.Equal(2, _tags.Tags.Count);
    }

    [Fact]
    public async Task AtTheLimit_TheDomainRejects_AndNothingIsStored()
    {
        _tags.CountOverride = Tag.MaxPerUser;

        await Assert.ThrowsAsync<DomainException>(() => Handle("one too many"));

        Assert.Empty(_tags.Tags);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ABlankName_IsRejectedByTheDomain(string? name)
    {
        await Assert.ThrowsAsync<DomainException>(() => Handle(name));
    }

    [Fact]
    public async Task WhenAConcurrentRequestCreatedTheNameFirst_TheWinnersTagIsReturned_AndOurInsertIsForgotten()
    {
        Tag? winner = null;
        _unitOfWork.BeforeSave = _ =>
        {
            winner = Make.NewTag(_user.UserId, "Krank");
            _tags.Tags.Add(winner);
            throw new DuplicateEntryException(new Exception("unique violation"));
        };

        var result = await Handle("krank");

        Assert.False(result.Created);
        Assert.Equal(winner!.Id, result.Tag.Id);
        Assert.Equal("Krank", result.Tag.Name);
        Assert.Same(winner, Assert.Single(_tags.Tags));
        Assert.Single(_tags.Removed); // the tag we tried to insert
    }

    [Fact]
    public async Task WhenTheWinnerIsGoneAgain_TheDuplicateIsReported_SoTheClientCanRetry()
    {
        _unitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));

        await Assert.ThrowsAsync<DuplicateEntryException>(() => Handle("krank"));
    }
}

public class RenameTagCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeTagRepository _tags = new();
    private readonly FakeHabitDayEntryRepository _days;
    private readonly FakeUnitOfWork _unitOfWork = new();

    public RenameTagCommandHandlerTests()
    {
        _days = new FakeHabitDayEntryRepository(_habits);
    }

    private Task<RenamedTagDto> Handle(Guid tagId, string? name)
        => new RenameTagCommandHandler(_tags, _days, _unitOfWork, _user)
            .Handle(new RenameTagCommand(tagId, name), CancellationToken.None);

    private Tag AddTag(string name, Guid? owner = null)
    {
        var tag = Make.NewTag(owner ?? _user.UserId, name);
        _tags.Tags.Add(tag);
        return tag;
    }

    private Habit AddHabit()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);
        return habit;
    }

    [Fact]
    public async Task RenamesTheTag_Trimmed_AndSavesOnce()
    {
        var tag = AddTag("krank");

        var result = await Handle(tag.Id, "  Erkältet ");

        Assert.Equal(new RenamedTagDto(tag.Id, "Erkältet", Merged: false), result);
        Assert.Equal("Erkältet", tag.Name.Value);
        Assert.Equal("ERKÄLTET", tag.NameKey);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ChangingOnlyTheSpelling_RenamesTheSameTag_ItIsNotMergedWithItself()
    {
        var tag = AddTag("krank");

        var result = await Handle(tag.Id, "Krank");

        Assert.False(result.Merged);
        Assert.Equal(tag.Id, result.Id);
        Assert.Equal("Krank", tag.Name.Value);
        Assert.Single(_tags.Tags);
    }

    [Fact]
    public async Task AnUnknownTag_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), "x"));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersTag_IsNotFound_AndKeepsItsName()
    {
        var foreign = AddTag("krank", owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, "mine now"));

        Assert.Equal("krank", foreign.Name.Value);
    }

    [Fact]
    public async Task TheNameOfAnotherUsersTag_IsFree_NoMerge()
    {
        var mine = AddTag("krank");
        AddTag("Urlaub", owner: Guid.NewGuid());

        var result = await Handle(mine.Id, "urlaub");

        Assert.False(result.Merged);
        Assert.Equal("urlaub", mine.Name.Value);
    }

    [Fact]
    public async Task ABlankName_IsRejectedByTheDomain_AndTheTagKeepsItsName()
    {
        var tag = AddTag("krank");

        await Assert.ThrowsAsync<DomainException>(() => Handle(tag.Id, "  "));

        Assert.Equal("krank", tag.Name.Value);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // ---- merge ----

    [Fact]
    public async Task ANameAnotherTagHas_MergesThem_TheDaysGoToTheOtherTag_AndTheRenamedTagIsGone()
    {
        var habit = AddHabit();
        var flu = AddTag("Grippe");
        var sick = AddTag("krank");
        _days.Entries.Add(Make.NewDay(habit.Id, Day, flu));
        _days.Entries.Add(Make.NewDay(habit.Id, Day.AddDays(1), flu));

        var result = await Handle(flu.Id, "KRANK");

        Assert.Equal(new RenamedTagDto(sick.Id, "krank", Merged: true), result); // the survivor's id and spelling
        Assert.Same(sick, Assert.Single(_tags.Tags));
        Assert.Same(flu, Assert.Single(_tags.Removed));
        Assert.All(_days.Entries, entry =>
        {
            Assert.True(entry.HasTag(sick.Id));
            Assert.False(entry.HasTag(flu.Id));
        });
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADayThatHadBothTags_KeepsOne()
    {
        var habit = AddHabit();
        var flu = AddTag("Grippe");
        var sick = AddTag("krank");
        var entry = Make.NewDay(habit.Id, Day, flu, sick);
        _days.Entries.Add(entry);

        await Handle(flu.Id, "krank");

        var remaining = Assert.Single(entry.Tags);
        Assert.Equal(sick.Id, remaining.TagId);
    }

    [Fact]
    public async Task OtherTagsOfTheMergedDays_AreKept()
    {
        var habit = AddHabit();
        var flu = AddTag("Grippe");
        var sick = AddTag("krank");
        var holiday = AddTag("Urlaub");
        var entry = Make.NewDay(habit.Id, Day, flu, holiday);
        _days.Entries.Add(entry);

        await Handle(flu.Id, "krank");

        Assert.Equal(new[] { holiday.Id, sick.Id }.Order(), entry.Tags.Select(t => t.TagId).Order());
    }

    [Fact]
    public async Task OnlyTheUsersOwnDaysAreMoved()
    {
        var mine = AddHabit();
        var foreignHabit = Make.NewHabit(Guid.NewGuid());
        _habits.Habits.Add(foreignHabit);
        var flu = AddTag("Grippe");
        var sick = AddTag("krank");
        var theirDay = Make.NewDay(foreignHabit.Id, Day, flu); // cannot happen through the API; the fake proves the filter
        _days.Entries.Add(theirDay);
        _days.Entries.Add(Make.NewDay(mine.Id, Day, flu));

        await Handle(flu.Id, "krank");

        Assert.True(theirDay.HasTag(flu.Id));
    }
}

public class DeleteTagCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeTagRepository _tags = new();
    private readonly FakeHabitDayEntryRepository _days;
    private readonly FakeUnitOfWork _unitOfWork = new();

    public DeleteTagCommandHandlerTests()
    {
        _days = new FakeHabitDayEntryRepository(_habits);
    }

    private Task<DeletedTagDto> Handle(Guid tagId)
        => new DeleteTagCommandHandler(_tags, _days, _unitOfWork, _user)
            .Handle(new DeleteTagCommand(tagId), CancellationToken.None);

    private Tag AddTag(string name, Guid? owner = null)
    {
        var tag = Make.NewTag(owner ?? _user.UserId, name);
        _tags.Tags.Add(tag);
        return tag;
    }

    private Habit AddHabit()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);
        return habit;
    }

    [Fact]
    public async Task DeletesTheTag_TakesItOffItsDays_AndSaysOnHowManyDaysItWas()
    {
        var habit = AddHabit();
        var other = AddHabit();
        var sick = AddTag("krank");
        var holiday = AddTag("Urlaub");
        var withBoth = Make.NewDay(habit.Id, Day, sick, holiday);
        _days.Entries.Add(withBoth);
        _days.Entries.Add(Make.NewDay(habit.Id, Day.AddDays(1), sick));
        _days.Entries.Add(Make.NewDay(other.Id, Day, sick));

        var result = await Handle(sick.Id);

        Assert.Equal(3, result.RemovedFromDays);
        Assert.Same(holiday, Assert.Single(_tags.Tags));
        Assert.Equal(1, _unitOfWork.SaveCount);

        // The day that held only this tag is gone with it; the day that held another tag stays with that one.
        Assert.Equal(2, _days.Removed.Count);
        Assert.Same(withBoth, Assert.Single(_days.Entries));
        Assert.Equal(holiday.Id, Assert.Single(withBoth.Tags).TagId);
    }

    [Fact]
    public async Task ATagThatIsNotUsed_IsDeleted_OnZeroDays()
    {
        var tag = AddTag("krank");

        var result = await Handle(tag.Id);

        Assert.Equal(0, result.RemovedFromDays);
        Assert.Empty(_tags.Tags);
    }

    [Fact]
    public async Task AnUnknownTag_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid()));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersTag_IsNotFound_AndStays()
    {
        var foreign = AddTag("krank", owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id));

        Assert.Single(_tags.Tags);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}

public class GetTagsQueryHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _habits = new();
    private readonly FakeTagRepository _tags = new();
    private readonly FakeHabitDayEntryRepository _days;
    private readonly FakeTagQueries _queries;

    public GetTagsQueryHandlerTests()
    {
        _days = new FakeHabitDayEntryRepository(_habits);
        _queries = new FakeTagQueries(_habits, _tags, _days);
    }

    private Task<List<TagDto>> Handle(Guid? habitId = null)
        => new GetTagsQueryHandler(_queries, _user).Handle(new GetTagsQuery(habitId), CancellationToken.None);

    [Fact]
    public async Task AsksForTheCurrentUser_AndTheGivenHabit()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);
        _tags.Tags.Add(Make.NewTag(_user.UserId, "krank"));

        var tags = await Handle(habit.Id);

        Assert.Single(tags);
        Assert.Equal([_user.UserId], _queries.AskedForUsers);
        Assert.Equal(habit.Id, _queries.AskedForHabit);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid()));
    }

    [Fact]
    public async Task AnotherUsersHabit_LooksExactlyLikeAnUnknownOne()
    {
        var foreign = Make.NewHabit(Guid.NewGuid());
        _habits.Habits.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id));
    }

    [Fact]
    public async Task WithoutAHabit_NoHabitIsLookedUp_AndTheHabitUseIsZero()
    {
        var habit = Make.NewHabit(_user.UserId);
        _habits.Habits.Add(habit);
        var sick = Make.NewTag(_user.UserId, "krank");
        _tags.Tags.Add(sick);
        _days.Entries.Add(Make.NewDay(habit.Id, Day, sick));

        var tag = Assert.Single(await Handle());

        Assert.Equal(1, tag.UseCount);
        Assert.Equal(0, tag.HabitUseCount);
        Assert.Equal(Day, tag.LastUsedOn);
    }
}
