using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Infrastructure.Queries;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Persistence;

// The read side: SQL projections straight to DTOs. Each of them takes the owner's id, and a row of somebody else must not
// show up in any of them - not even through a join.
[Collection(IntegrationCollection.Name)]
public class QueryTests : IAsyncLifetime
{
    private readonly TestEnvironment _environment;
    private ApplicationDbContext _db = null!;
    private Guid _user;
    private Guid _stranger;

    public QueryTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _db = _environment.CreateContext();
        _user = await Sql.InsertUserAsync(_db);
        _stranger = await Sql.InsertUserAsync(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ---- habits ----

    [Fact]
    public async Task GetAll_ReturnsTheUsersHabits_WithTheirPlan()
    {
        var mine = await Sql.InsertHabitAsync(_db, _user, "Read", schedule: 62);
        await Sql.InsertHabitAsync(_db, _stranger, "Not mine");

        var habits = await new HabitQueries(_db).GetAllAsync(_user, default);

        var habit = Assert.Single(habits);
        Assert.Equal(mine, habit.Id);
        Assert.Equal("Read", habit.Title);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], habit.Schedule);
    }

    [Fact]
    public async Task GetById_ReturnsTheDetails_ButNeverAnotherUsersHabit()
    {
        var mine = await Sql.InsertHabitAsync(_db, _user, "Read", startDate: "2026-03-01");
        var theirs = await Sql.InsertHabitAsync(_db, _stranger, "Secret");
        var queries = new HabitQueries(_db);

        var details = await queries.GetByIdAsync(_user, mine, default);

        Assert.Equal("Read", details!.Title);
        Assert.Equal(new DateOnly(2026, 3, 1), details.StartDate);
        Assert.Null(await queries.GetByIdAsync(_user, theirs, default));
        Assert.Null(await queries.GetByIdAsync(_user, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task WithCompletedDates_InCreationOrder_EveryDateOfEveryHabit_OnlyTheUsers()
    {
        var now = DateTime.UtcNow;
        // Inserted out of order on purpose; the creation time decides.
        var third = await Sql.InsertHabitAsync(_db, _user, "Third", createdAt: now.AddMinutes(-1));
        var first = await Sql.InsertHabitAsync(_db, _user, "First", createdAt: now.AddMinutes(-30));
        var second = await Sql.InsertHabitAsync(_db, _user, "Second", schedule: 65, startDate: "2026-02-02", createdAt: now.AddMinutes(-10));
        await Sql.InsertHabitAsync(_db, _stranger, "Not mine", createdAt: now.AddMinutes(-20));
        await Sql.InsertRecordAsync(_db, first, "2026-10-08");
        await Sql.InsertRecordAsync(_db, first, "2025-01-01"); // far back: the streak needs the whole history
        await Sql.InsertRecordAsync(_db, second, "2026-10-07");

        var habits = await new HabitQueries(_db).GetWithCompletedDatesAsync(_user, default);

        Assert.Equal([first, second, third], habits.Select(h => h.Id));
        Assert.Equal(["2025-01-01", "2026-10-08"], habits[0].CompletedDates.Select(d => d.ToString("yyyy-MM-dd")).Order());
        Assert.Equal([new DateOnly(2026, 10, 7)], habits[1].CompletedDates);
        Assert.Empty(habits[2].CompletedDates);
        Assert.Equal([DayOfWeek.Sunday, DayOfWeek.Saturday], habits[1].Schedule.Days);
        Assert.Equal(new DateOnly(2026, 2, 2), habits[1].StartDate);
    }

    [Fact]
    public async Task Records_AnotherUsersHabitIsNull_NotAnEmptyList_AHabitWithoutRecordsIsEmpty()
    {
        var mine = await Sql.InsertHabitAsync(_db, _user);
        var theirs = await Sql.InsertHabitAsync(_db, _stranger);
        await Sql.InsertRecordAsync(_db, theirs, "2026-10-08");
        var queries = new HabitQueries(_db);

        Assert.Empty((await queries.GetRecordsAsync(_user, mine, null, null, default))!);
        Assert.Null(await queries.GetRecordsAsync(_user, theirs, null, null, default));
        Assert.Null(await queries.GetRecordsAsync(_user, Guid.NewGuid(), null, null, default));
    }

    [Fact]
    public async Task Records_AreOrderedByDate_AndTheRangeIsInclusive()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        foreach (var day in new[] { "2026-10-09", "2026-10-05", "2026-10-07", "2026-10-06", "2026-10-08" })
            await Sql.InsertRecordAsync(_db, habit, day);
        var queries = new HabitQueries(_db);

        async Task<string[]> Dates(DateOnly? from, DateOnly? to) =>
            (await queries.GetRecordsAsync(_user, habit, from, to, default))!.Select(r => r.Date.ToString("yyyy-MM-dd")).ToArray();

        Assert.Equal(["2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08", "2026-10-09"], await Dates(null, null));
        Assert.Equal(["2026-10-06", "2026-10-07", "2026-10-08"], await Dates(new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 8)));
        Assert.Equal(["2026-10-08", "2026-10-09"], await Dates(new DateOnly(2026, 10, 8), null));
        Assert.Equal(["2026-10-05"], await Dates(null, new DateOnly(2026, 10, 5)));
        Assert.Equal(["2026-10-07"], await Dates(new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7)));
        Assert.Empty(await Dates(new DateOnly(2027, 1, 1), null));
    }

    // ---- daily diaries ----

    [Fact]
    public async Task Diary_ByDate_ReturnsTheWholeDocument_AsADto()
    {
        var diary = DailyDiary.Create(_user, new DateOnly(2026, 10, 8), DiaryContent.Create(
            Mood.Good, Percentage.Create(70), null, "A good day", ["coffee"], ["Rust"], [DiaryTask.Create("Call mum", true)]));
        _db.DailyDiaries.Add(diary);
        await _db.SaveChangesAsync();

        var dto = await new DailyDiaryQueries(_db).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default);

        Assert.NotNull(dto);
        Assert.Equal(Mood.Good, dto.Mood);
        Assert.Equal(70, dto.Body);
        Assert.Null(dto.Mind);
        Assert.Equal("A good day", dto.Highlight);
        Assert.Equal(["coffee"], dto.Grateful);
        Assert.Equal(["Rust"], dto.Learned);
        Assert.Equal("Call mum", Assert.Single(dto.Tasks).Title);
        Assert.True(dto.Tasks[0].Done);
    }

    [Fact]
    public async Task Diary_ByDate_NothingWrittenOrSomebodyElses_IsNull()
    {
        await Sql.InsertDiaryAsync(_db, _stranger, "2026-10-08");
        var queries = new DailyDiaryQueries(_db);

        Assert.Null(await queries.GetByDateAsync(_user, new DateOnly(2026, 10, 8), default));
    }

    [Fact]
    public async Task Diary_Days_AreOrdered_InclusiveRanged_AndOnlyTheUsers()
    {
        foreach (var day in new[] { "2026-10-21", "2026-10-03", "2026-10-09" })
            await Sql.InsertDiaryAsync(_db, _user, day, mood: day == "2026-10-03" ? null : 4);
        await Sql.InsertDiaryAsync(_db, _stranger, "2026-10-05");
        var queries = new DailyDiaryQueries(_db);

        var all = await queries.GetDaysAsync(_user, null, null, default);
        Assert.Equal(["2026-10-03", "2026-10-09", "2026-10-21"], all.Select(d => d.Date.ToString("yyyy-MM-dd")));
        Assert.Null(all[0].Mood);
        Assert.Equal(Mood.Good, all[1].Mood);

        var ranged = await queries.GetDaysAsync(_user, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 21), default);
        Assert.Equal(["2026-10-09", "2026-10-21"], ranged.Select(d => d.Date.ToString("yyyy-MM-dd")));
        Assert.Single(await queries.GetDaysAsync(_user, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9), default));
        Assert.Empty(await queries.GetDaysAsync(_user, new DateOnly(2027, 1, 1), null, default));
    }

    // ---- resolutions ----

    [Fact]
    public async Task Resolutions_AreInCreationOrder_ForTheRightUserAndYear()
    {
        var older = await Sql.InsertResolutionAsync(_db, _user, 2026, "Older");
        await Task.Delay(5); // CreatedAt is the application's clock; keep the inserts apart
        var newer = await Sql.InsertResolutionAsync(_db, _user, 2026, "Newer");
        await Sql.InsertResolutionAsync(_db, _user, 2027, "Other year");
        await Sql.InsertResolutionAsync(_db, _stranger, 2026, "Not mine");

        var items = await new ResolutionQueries(_db).GetByYearAsync(_user, 2026, default);

        Assert.Equal([older, newer], items.Select(i => i.Id));
        Assert.All(items, item => Assert.False(item.Kept));
    }

    [Fact]
    public async Task Resolutions_AResolutionWithoutAHabit_IsNotLostByTheJoin()
    {
        var alone = await Sql.InsertResolutionAsync(_db, _user, 2026, "Alone");
        var habit = await Sql.InsertHabitAsync(_db, _user, "Read every day");
        var linked = await Sql.InsertResolutionAsync(_db, _user, 2026, "Linked", habit);

        var items = await new ResolutionQueries(_db).GetByYearAsync(_user, 2026, default);

        Assert.Equal(2, items.Count);
        var aloneItem = items.Single(i => i.Id == alone);
        Assert.Null(aloneItem.HabitId);
        Assert.Null(aloneItem.HabitTitle);
        var linkedItem = items.Single(i => i.Id == linked);
        Assert.Equal(habit, linkedItem.HabitId);
        Assert.Equal("Read every day", linkedItem.HabitTitle);
    }

    [Fact]
    public async Task Resolutions_TheTitleOfSomebodyElsesHabit_NeverLeaksThroughTheJoin()
    {
        // The application refuses such a link, but data isolation must not depend on a rule elsewhere: the join only
        // looks at the asking user's own habits.
        var theirHabit = await Sql.InsertHabitAsync(_db, _stranger, "Their secret habit");
        await Sql.InsertResolutionAsync(_db, _user, 2026, "Mine", theirHabit);

        var item = Assert.Single(await new ResolutionQueries(_db).GetByYearAsync(_user, 2026, default));

        Assert.Null(item.HabitTitle);
    }

    [Fact]
    public async Task Resolutions_AYearWithoutAny_IsEmpty()
    {
        Assert.Empty(await new ResolutionQueries(_db).GetByYearAsync(_user, 2031, default));
    }
}
