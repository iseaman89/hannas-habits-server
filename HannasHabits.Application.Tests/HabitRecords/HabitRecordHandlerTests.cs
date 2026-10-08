using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;
using HannasHabits.Application.HabitRecords.Queries.GetRecordsByHabit;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.Entities;

namespace HannasHabits.Application.Tests.HabitRecords;

public class MarkCompletedCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _repository = new();
    private readonly FakeHabitQueries _queries;
    private readonly FakeUnitOfWork _unitOfWork = new();

    public MarkCompletedCommandHandlerTests()
    {
        _queries = new FakeHabitQueries(_repository);
    }

    private Task<HabitRecordDto> Handle(Guid habitId, DateOnly date)
        => new MarkCompletedCommandHandler(_repository, _queries, _unitOfWork, _user, TestMapper.Instance)
            .Handle(new MarkCompletedCommand(habitId, date), CancellationToken.None);

    private Habit AddHabit(Guid? owner = null, IEnumerable<DateOnly>? completed = null)
    {
        var habit = Make.NewHabit(owner ?? _user.UserId, completed: completed);
        _repository.Habits.Add(habit);
        return habit;
    }

    [Fact]
    public async Task MarksTheDay_AndSavesOnce()
    {
        var habit = AddHabit();

        var dto = await Handle(habit.Id, Day);

        Assert.Equal(habit.Id, dto.HabitId);
        Assert.Equal(Day, dto.Date);
        Assert.Equal(Day, Assert.Single(habit.Records).Date);
        Assert.Equal(dto.Id, habit.Records.Single().Id);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task MarkingADayThatIsAlreadyDone_IsNotAnError_ItReturnsTheExistingRecord_WithoutSaving()
    {
        var habit = AddHabit(completed: [Day]);
        var existing = habit.Records.Single();

        var dto = await Handle(habit.Id, Day);

        Assert.Equal(existing.Id, dto.Id);
        Assert.Single(habit.Records);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), Day));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndNothingIsMarked()
    {
        var foreign = AddHabit(owner: Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, Day));

        Assert.Empty(foreign.Records);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // The unique index (habit, day) rejects the second of two concurrent inserts.
    [Fact]
    public async Task WhenAConcurrentRequestWonTheRace_TheCallerGetsTheWinnersRecord()
    {
        var habit = AddHabit();
        var winner = new HabitRecordDto(Guid.NewGuid(), habit.Id, Day);
        _unitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));
        _queries.RecordsOverride = () => [winner];

        var dto = await Handle(habit.Id, Day);

        Assert.Equal(winner, dto);
        Assert.Equal((Day, Day), _queries.LastRecordsRange); // looked up exactly that one day
    }

    [Theory]
    [InlineData(true)]  // the habit has no record for that day any more
    [InlineData(false)] // the habit itself is gone
    public async Task WhenTheWinnerIsGoneAgain_TheDuplicateIsReported_SoTheClientCanRetry(bool habitStillThere)
    {
        var habit = AddHabit();
        _unitOfWork.BeforeSave = _ => throw new DuplicateEntryException(new Exception("unique violation"));
        _queries.RecordsOverride = () => habitStillThere ? [] : null;

        await Assert.ThrowsAsync<DuplicateEntryException>(() => Handle(habit.Id, Day));
    }

    [Fact]
    public async Task OtherSaveFailuresAreNotSwallowed()
    {
        var habit = AddHabit();
        _unitOfWork.BeforeSave = _ => throw new ConflictException("changed by another request");

        await Assert.ThrowsAsync<ConflictException>(() => Handle(habit.Id, Day));
    }
}

public class UnmarkCompletedCommandHandlerTests
{
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private Task Handle(Guid habitId, DateOnly date)
        => new UnmarkCompletedCommandHandler(_repository, _unitOfWork, _user)
            .Handle(new UnmarkCompletedCommand(habitId, date), CancellationToken.None);

    [Fact]
    public async Task TakesTheCompletionBack_AndSaves()
    {
        var habit = Make.NewHabit(_user.UserId, completed: [Day, Day.AddDays(-1)]);
        _repository.Habits.Add(habit);

        await Handle(habit.Id, Day);

        Assert.Equal(Day.AddDays(-1), Assert.Single(habit.Records).Date);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ADayThatWasNotCompleted_IsNotFound()
    {
        var habit = Make.NewHabit(_user.UserId);
        _repository.Habits.Add(habit);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(habit.Id, Day));

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid(), Day));
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound_AndKeepsItsRecord()
    {
        var foreign = Make.NewHabit(Guid.NewGuid(), completed: [Day]);
        _repository.Habits.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id, Day));

        Assert.Single(foreign.Records);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}

public class GetRecordsByHabitQueryHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _repository = new();

    private Task<List<HabitRecordDto>> Handle(GetRecordsByHabitQuery query)
        => new GetRecordsByHabitQueryHandler(new FakeHabitQueries(_repository), _user).Handle(query, CancellationToken.None);

    [Fact]
    public async Task ReturnsTheRecordsInDateOrder_LimitedToTheRange()
    {
        var days = new[] { 9, 7, 8, 10 }.Select(d => new DateOnly(2026, 10, d)).ToArray();
        var habit = Make.NewHabit(_user.UserId, completed: days);
        _repository.Habits.Add(habit);

        var records = await Handle(new GetRecordsByHabitQuery(habit.Id, new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9)));

        Assert.Equal([new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9)], records.Select(r => r.Date));
    }

    [Fact]
    public async Task AHabitWithoutRecords_GivesAnEmptyList()
    {
        var habit = Make.NewHabit(_user.UserId);
        _repository.Habits.Add(habit);

        Assert.Empty(await Handle(new GetRecordsByHabitQuery(habit.Id)));
    }

    [Fact]
    public async Task AnUnknownHabit_IsNotFound_ItIsNotAnEmptyList()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(new GetRecordsByHabitQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task AnotherUsersHabit_IsNotFound()
    {
        var foreign = Make.NewHabit(Guid.NewGuid(), completed: [new DateOnly(2026, 10, 8)]);
        _repository.Habits.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(new GetRecordsByHabitQuery(foreign.Id)));
    }
}
