using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Commands.DeleteDailyDiary;
using HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryByDate;
using HannasHabits.Application.DailyDiaries.Queries.GetDailyDiaryDays;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.Exceptions;
using HannasHabits.Domain.ValueObjects;

namespace HannasHabits.Application.Tests.DailyDiaries;

public class SaveDailyDiaryCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeDailyDiaryRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(SaveDailyDiaryCommand command)
        => new SaveDailyDiaryCommandHandler(_repository, _unitOfWork, _user).Handle(command, CancellationToken.None);

    private static SaveDailyDiaryCommand Save(
        Mood? mood = null, int? body = null, int? mind = null, string? highlight = null,
        string[]? grateful = null, string[]? learned = null, DiaryTaskDto[]? tasks = null, DateOnly? date = null)
        => new(date ?? Day, mood, body, mind, highlight, grateful, learned, tasks);

    // ---- create ----

    [Fact]
    public async Task TheFirstSaveOfADay_CreatesTheWholeDocument()
    {
        await Handle(Save(Mood.Good, 70, 40, " A good day ", ["coffee"], ["some Rust"], [new DiaryTaskDto("Call mum", true)]));

        var entry = Assert.Single(_repository.Entries);
        Assert.Equal(_user.UserId, entry.UserId);
        Assert.Equal(Day, entry.Date);
        Assert.Equal(Mood.Good, entry.Mood);
        Assert.Equal(Percentage.Create(70), entry.Body);
        Assert.Equal(Percentage.Create(40), entry.Mind);
        Assert.Equal("A good day", entry.Highlight);
        Assert.Equal(["coffee"], entry.Grateful);
        Assert.Equal(["some Rust"], entry.Learned);
        Assert.Equal([DiaryTask.Create("Call mum", true)], entry.Tasks);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnEntryMayBeJustAHighlight_OrJustABodyOfZero()
    {
        await Handle(Save(highlight: "only this", date: Day));
        await Handle(Save(body: 0, date: Day.AddDays(1)));

        Assert.Equal(2, _repository.Entries.Count);
    }

    // ---- replace ----

    [Fact]
    public async Task ASecondSaveOfTheSameDay_ReplacesTheDocument_AndClearsWhatIsMissing()
    {
        var existing = Make.NewDiary(_user.UserId, Day, Mood.Good, "old highlight");
        _repository.Entries.Add(existing);

        await Handle(Save(Mood.Bad, grateful: ["tea"]));

        Assert.Same(existing, Assert.Single(_repository.Entries)); // updated in place, not duplicated
        Assert.Equal(Mood.Bad, existing.Mood);
        Assert.Null(existing.Highlight);
        Assert.Equal(["tea"], existing.Grateful);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SavingTwiceWithTheSameContent_GivesTheSameDocument()
    {
        var command = Save(Mood.Ok, highlight: "same");

        await Handle(command);
        await Handle(command);

        var entry = Assert.Single(_repository.Entries);
        Assert.Equal(Mood.Ok, entry.Mood);
        Assert.Equal("same", entry.Highlight);
    }

    [Fact]
    public async Task AnotherUsersEntryOfTheSameDay_IsNotTouched()
    {
        var foreign = Make.NewDiary(Guid.NewGuid(), Day, Mood.Excellent, "private");
        _repository.Entries.Add(foreign);

        await Handle(Save(Mood.Terrible));

        Assert.Equal(2, _repository.Entries.Count);
        Assert.Equal(Mood.Excellent, foreign.Mood);
        Assert.Equal("private", foreign.Highlight);
    }

    // ---- an empty document is "no entry" ----

    [Fact]
    public async Task AnEmptyDocument_RemovesAnExistingEntry()
    {
        var existing = Make.NewDiary(_user.UserId, Day);
        _repository.Entries.Add(existing);

        await Handle(Save());

        Assert.Same(existing, Assert.Single(_repository.Removed));
        Assert.Empty(_repository.Entries);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ABlankDocument_CountsAsEmpty()
    {
        _repository.Entries.Add(Make.NewDiary(_user.UserId, Day));

        await Handle(Save(highlight: "   ", grateful: [], learned: [], tasks: []));

        Assert.Empty(_repository.Entries);
    }

    [Fact]
    public async Task AnEmptyDocumentForADayWithoutEntry_IsANoOp_NothingIsStored_NothingIsSaved()
    {
        await Handle(Save());

        Assert.Empty(_repository.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnEmptyDocument_DoesNotRemoveAnotherUsersEntry()
    {
        var foreign = Make.NewDiary(Guid.NewGuid(), Day);
        _repository.Entries.Add(foreign);

        await Handle(Save());

        Assert.Same(foreign, Assert.Single(_repository.Entries));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task WhenTheEntryWasRemovedByAnotherRequestInTheMeantime_AnEmptySaveStillSucceeds()
    {
        // The day is empty - exactly what was asked for.
        _repository.Entries.Add(Make.NewDiary(_user.UserId, Day));
        _unitOfWork.BeforeSave = _ => throw new ConflictException("already deleted");

        await Handle(Save());
    }

    // ---- the Domain rules apply ----

    [Fact]
    public async Task ABrokenRule_IsADomainException_AndNothingIsStored()
    {
        await Assert.ThrowsAsync<DomainException>(() => Handle(Save(grateful: ["fine", " "])));
        await Assert.ThrowsAsync<DomainException>(() => Handle(Save(body: 101)));
        await Assert.ThrowsAsync<DomainException>(() => Handle(Save(mood: (Mood)9)));
        await Assert.ThrowsAsync<DomainException>(() => Handle(Save(tasks: [new DiaryTaskDto("", false)])));

        Assert.Empty(_repository.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ARejectedReplace_LeavesTheStoredEntryAsItWas()
    {
        var existing = Make.NewDiary(_user.UserId, Day, Mood.Good, "keep me");
        _repository.Entries.Add(existing);

        await Assert.ThrowsAsync<DomainException>(() => Handle(Save(Mood.Bad, highlight: new string('a', 5001))));

        Assert.Equal(Mood.Good, existing.Mood);
        Assert.Equal("keep me", existing.Highlight);
    }

    // ---- two first saves of a day at once: last write wins ----

    [Fact]
    public async Task WhenAConcurrentRequestCreatedTheDayFirst_OurContentIsPutOntoTheWinner()
    {
        var winner = Make.NewDiary(_user.UserId, Day, Mood.Terrible, "the other one");
        _unitOfWork.BeforeSave = call =>
        {
            if (call != 1)
                return;

            // The other request committed between our lookup and our save; the unique index rejects our insert.
            _repository.Entries.Add(winner);
            throw new DuplicateEntryException(new Exception("unique violation"));
        };

        await Handle(Save(Mood.Excellent, highlight: "ours"));

        // One document for the day: the winner's row, now holding our (later) content - and our failed insert is gone.
        Assert.Same(winner, Assert.Single(_repository.Entries));
        Assert.Equal(Mood.Excellent, winner.Mood);
        Assert.Equal("ours", winner.Highlight);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task WhenTheWinnerIsGoneAgain_TheDuplicateIsReported_SoTheClientCanRetry()
    {
        _unitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));

        await Assert.ThrowsAsync<DuplicateEntryException>(() => Handle(Save(Mood.Ok)));

        Assert.Empty(_repository.Entries); // our failed insert was forgotten, not left behind
    }
}

public class DeleteDailyDiaryCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeDailyDiaryRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(DateOnly date)
        => new DeleteDailyDiaryCommandHandler(_repository, _unitOfWork, _user)
            .Handle(new DeleteDailyDiaryCommand(date), CancellationToken.None);

    [Fact]
    public async Task RemovesTheEntryOfThatDay_AndSaves()
    {
        var entry = Make.NewDiary(_user.UserId, Day);
        var otherDay = Make.NewDiary(_user.UserId, Day.AddDays(1));
        _repository.Entries.Add(entry);
        _repository.Entries.Add(otherDay);

        await Handle(Day);

        Assert.Same(entry, Assert.Single(_repository.Removed));
        Assert.Same(otherDay, Assert.Single(_repository.Entries));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADayWithoutEntry_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Day));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersEntry_IsNotFound_AndStays()
    {
        var foreign = Make.NewDiary(Guid.NewGuid(), Day);
        _repository.Entries.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Day));

        Assert.Same(foreign, Assert.Single(_repository.Entries));
    }
}

public class DailyDiaryQueryHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeDailyDiaryQueries _queries = new();

    [Fact]
    public async Task GetByDate_ReturnsTheDocument_AskingForTheCurrentUserAndDay()
    {
        var dto = new DailyDiaryDto(Day, Mood.Good, 70, null, "hi", ["a"], [], []);
        _queries.ByDate = dto;

        var result = await new GetDailyDiaryByDateQueryHandler(_queries, _user)
            .Handle(new GetDailyDiaryByDateQuery(Day), CancellationToken.None);

        Assert.Same(dto, result);
        Assert.Equal(_user.UserId, _queries.AskedForUser);
        Assert.Equal(Day, _queries.AskedForDate);
    }

    [Fact]
    public async Task GetByDate_ADayWithoutEntry_IsNotFound()
    {
        _queries.ByDate = null;

        await Assert.ThrowsAsync<NotFoundException>(() => new GetDailyDiaryByDateQueryHandler(_queries, _user)
            .Handle(new GetDailyDiaryByDateQuery(Day), CancellationToken.None));
    }

    [Fact]
    public async Task GetDays_PassesTheRangeThrough_ForTheCurrentUser()
    {
        _queries.Days = [new DailyDiaryDayDto(Day, Mood.Ok), new DailyDiaryDayDto(Day.AddDays(1), null)];
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 31);

        var result = await new GetDailyDiaryDaysQueryHandler(_queries, _user)
            .Handle(new GetDailyDiaryDaysQuery(from, to), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal((from, to), _queries.AskedForRange);
        Assert.Equal(_user.UserId, _queries.AskedForUser);
    }
}
