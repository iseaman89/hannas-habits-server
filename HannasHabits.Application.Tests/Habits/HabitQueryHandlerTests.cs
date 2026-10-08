using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Habits.Queries.GetAllHabits;
using HannasHabits.Application.Habits.Queries.GetHabitById;
using HannasHabits.Application.Habits.Queries.GetHabitsOverview;
using HannasHabits.Application.Tests.Fakes;
using HannasHabits.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace HannasHabits.Application.Tests.Habits;

public class GetAllHabitsQueryHandlerTests
{
    [Fact]
    public async Task ReturnsOnlyTheCurrentUsersHabits()
    {
        var user = new FakeCurrentUser();
        var repository = new FakeHabitRepository();
        var queries = new FakeHabitQueries(repository);
        var mine = Make.NewHabit(user.UserId, "Mine");
        repository.Habits.Add(mine);
        repository.Habits.Add(Make.NewHabit(Guid.NewGuid(), "Somebody else's"));

        var result = await new GetAllHabitsQueryHandler(queries, user).Handle(new GetAllHabitsQuery(), CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(result).Id);
        Assert.Equal([user.UserId], queries.AskedForUsers);
    }
}

public class GetHabitByIdQueryHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _repository = new();

    private Task<HabitDetailsDto> Handle(Guid id)
        => new GetHabitByIdQueryHandler(new FakeHabitQueries(_repository), _user).Handle(new GetHabitByIdQuery(id), CancellationToken.None);

    [Fact]
    public async Task ReturnsTheDetails_IncludingTheStartDate()
    {
        var habit = Make.NewHabit(_user.UserId, "Read", new DateOnly(2026, 3, 1), description: "20 pages");
        _repository.Habits.Add(habit);

        var details = await Handle(habit.Id);

        Assert.Equal(habit.Id, details.Id);
        Assert.Equal("Read", details.Title);
        Assert.Equal("20 pages", details.Description);
        Assert.Equal(new DateOnly(2026, 3, 1), details.StartDate);
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
        _repository.Habits.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => Handle(foreign.Id));
    }
}

public class GetHabitsOverviewQueryHandlerTests
{
    private static readonly DateOnly Today = TestClock.Today; // Thursday, 8 October 2026

    private readonly FakeCurrentUser _user = new();
    private readonly FakeHabitRepository _repository = new();
    private readonly FakeTimeProvider _clock = TestClock.Create();

    private Task<List<HabitOverviewDto>> Handle(DateOnly? from, DateOnly? to, DateOnly? asOf = null)
        => new GetHabitsOverviewQueryHandler(new FakeHabitQueries(_repository), _user, _clock)
            .Handle(new GetHabitsOverviewQuery(from, to, asOf), CancellationToken.None);

    private static DateOnly[] Days(int month, params int[] days) => days.Select(d => new DateOnly(2026, month, d)).ToArray();

    [Fact]
    public async Task ReturnsEveryHabitOfTheUser_InCreationOrder_WithItsPlan()
    {
        var first = Make.NewHabit(_user.UserId, "Read", new DateOnly(2026, 1, 5));
        var second = Make.NewHabit(_user.UserId, "Run", new DateOnly(2026, 2, 6),
            HabitSchedule.Create([DayOfWeek.Saturday, DayOfWeek.Monday]));
        _repository.Habits.Add(first);
        _repository.Habits.Add(second);
        _repository.Habits.Add(Make.NewHabit(Guid.NewGuid(), "Somebody else's"));

        var overview = await Handle(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Collection(overview,
            item =>
            {
                Assert.Equal(first.Id, item.Id);
                Assert.Equal("Read", item.Title);
                Assert.Equal(HabitSchedule.Daily.Days, item.Schedule);
                Assert.Equal(new DateOnly(2026, 1, 5), item.StartDate);
            },
            item =>
            {
                Assert.Equal(second.Id, item.Id);
                Assert.Equal([DayOfWeek.Monday, DayOfWeek.Saturday], item.Schedule);
                Assert.Equal(new DateOnly(2026, 2, 6), item.StartDate);
            });
    }

    [Fact]
    public async Task CompletedDatesAreLimitedToTheRange_InclusiveAtBothEnds_OldestFirst()
    {
        // Added in a scrambled order on purpose.
        _repository.Habits.Add(Make.NewHabit(_user.UserId, completed: Days(10, 20, 1, 15, 31)
            .Concat(Days(9, 30)).Concat(Days(11, 1)).ToArray()));

        var overview = await Handle(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(Days(10, 1, 15, 20, 31), Assert.Single(overview).CompletedDates);
    }

    [Fact]
    public async Task ARangeOfOneDay_ReturnsJustThatDay()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId, completed: Days(10, 7, 8, 9)));

        var overview = await Handle(Today, Today);

        Assert.Equal([Today], Assert.Single(overview).CompletedDates);
    }

    [Fact]
    public async Task ADayWithoutCompletionsGivesAnEmptyList_NotAMissingHabit()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId));

        var item = Assert.Single(await Handle(Today, Today));

        Assert.Empty(item.CompletedDates);
        Assert.Equal(0, item.CurrentStreak);
    }

    [Fact]
    public async Task TheStreakDoesNotDependOnTheRange()
    {
        // 28 Sep .. 8 Oct, eleven days in a row, across the month boundary - the range is a single day.
        var days = Enumerable.Range(0, 11).Select(i => new DateOnly(2026, 9, 28).AddDays(i)).ToArray();
        _repository.Habits.Add(Make.NewHabit(_user.UserId, start: new DateOnly(2026, 9, 1), completed: days));

        var item = Assert.Single(await Handle(Today, Today));

        Assert.Equal([Today], item.CompletedDates);
        Assert.Equal(11, item.CurrentStreak);
    }

    [Fact]
    public async Task WithoutAsOf_TheStreakIsCountedFromTheServersUtcDate()
    {
        // Done 5th, 6th and 7th; the 8th (the clock's day) is still open and therefore skipped.
        _repository.Habits.Add(Make.NewHabit(_user.UserId, completed: Days(10, 5, 6, 7)));

        Assert.Equal(3, Assert.Single(await Handle(Today, Today)).CurrentStreak);

        _clock.Advance(TimeSpan.FromDays(1)); // now the 9th is open and the 8th is a miss
        Assert.Equal(0, Assert.Single(await Handle(Today, Today)).CurrentStreak);
    }

    [Fact]
    public async Task AnExplicitAsOf_WinsOverTheServerClock()
    {
        _repository.Habits.Add(Make.NewHabit(_user.UserId, completed: Days(10, 5, 6, 7)));

        // The client lives a day ahead of the server (e.g. New Zealand): for it the 9th is today, the 8th was missed.
        var item = Assert.Single(await Handle(Today, Today, asOf: new DateOnly(2026, 10, 9)));

        Assert.Equal(0, item.CurrentStreak);
    }

    [Fact]
    public async Task ARecordOnAnUnscheduledDayDoesNotCountForTheStreak_ButIsStillListed()
    {
        var weekdays = HabitSchedule.Create([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        // Thu 8 done, Sat 3 + Sun 4 done too (not planned), Fri 2 + Thu 1 done.
        _repository.Habits.Add(Make.NewHabit(_user.UserId, start: new DateOnly(2026, 10, 1), schedule: weekdays,
            completed: Days(10, 8, 3, 4, 2, 1)));

        var item = Assert.Single(await Handle(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        Assert.Equal(Days(10, 1, 2, 3, 4, 8), item.CompletedDates);
        Assert.Equal(1, item.CurrentStreak); // Thu 8 done; Wed 7 was planned and missed
    }

    [Fact]
    public async Task NoHabits_GiveAnEmptyOverview()
    {
        Assert.Empty(await Handle(Today, Today));
    }

    [Fact]
    public async Task OnlyTheCurrentUserIsAskedFor()
    {
        var queries = new FakeHabitQueries(_repository);
        await new GetHabitsOverviewQueryHandler(queries, _user, _clock)
            .Handle(new GetHabitsOverviewQuery(Today, Today, null), CancellationToken.None);

        Assert.Equal([_user.UserId], queries.AskedForUsers);
    }
}
