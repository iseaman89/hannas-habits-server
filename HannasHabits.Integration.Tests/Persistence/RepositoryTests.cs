using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Domain.Entities;
using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Infrastructure.Repositories;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace HannasHabits.Integration.Tests.Persistence;

// The command side: repositories hand out tracked aggregates - to their owner only - and the unit of work saves them.
[Collection(IntegrationCollection.Name)]
public class RepositoryTests : IAsyncLifetime
{
    private readonly TestEnvironment _environment;
    private ApplicationDbContext _db = null!;
    private Guid _user;

    public RepositoryTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _db = _environment.CreateContext();
        _user = await Sql.InsertUserAsync(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ApplicationDbContext FreshContext() => _environment.CreateContext();

    private static DiaryContent Content(
        Mood? mood = null, int? body = null, int? mind = null, string? highlight = null,
        string[]? grateful = null, string[]? learned = null, DiaryTask[]? tasks = null)
        => DiaryContent.Create(mood, body is { } b ? Percentage.Create(b) : null, mind is { } m ? Percentage.Create(m) : null, highlight, grateful, learned, tasks);

    // ---- habits ----

    [Fact]
    public async Task AHabit_RoundTripsThroughTheDatabase_WithEveryValue()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1), "20 pages",
            HabitSchedule.Create([DayOfWeek.Monday, DayOfWeek.Friday]));
        new HabitRepository(_db).Add(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        await using var other = FreshContext();
        var loaded = await new HabitRepository(other).GetByIdAsync(_user, habit.Id, default);

        Assert.NotNull(loaded);
        Assert.Equal(habit.Id, loaded.Id);
        Assert.Equal("Read", loaded.Title.Value);
        Assert.Equal("20 pages", loaded.Description);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], loaded.Schedule.Days);
        Assert.Equal(new DateOnly(2026, 3, 1), loaded.StartDate);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.InRange(loaded.CreatedAt, habit.CreatedAt.AddSeconds(-1), habit.CreatedAt.AddSeconds(1));
    }

    [Fact]
    public async Task AHabit_IsHandedOutOnlyToItsOwner()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1));
        var repository = new HabitRepository(_db);
        repository.Add(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        var stranger = await Sql.InsertUserAsync(_db);

        Assert.Null(await repository.GetByIdAsync(stranger, habit.Id, default));
        Assert.Null(await repository.GetByIdWithRecordsAsync(stranger, habit.Id, default));
        Assert.Null(await repository.GetByIdAsync(_user, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task WithoutRecords_TheHabitIsLoadedLight_WithRecords_Whole()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1));
        habit.MarkCompleted(new DateOnly(2026, 10, 7));
        habit.MarkCompleted(new DateOnly(2026, 10, 8));
        var writer = new HabitRepository(_db);
        writer.Add(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        await using var light = FreshContext();
        await using var whole = FreshContext();

        Assert.Empty((await new HabitRepository(light).GetByIdAsync(_user, habit.Id, default))!.Records);
        var loaded = (await new HabitRepository(whole).GetByIdWithRecordsAsync(_user, habit.Id, default))!;
        Assert.Equal([new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8)], loaded.Records.Select(r => r.Date).Order());
    }

    [Fact]
    public async Task MarkingADay_ThroughTheAggregate_InsertsARecord_Unmarking_DeletesIt()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1));
        new HabitRepository(_db).Add(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        // Mark in a later request: load, mark, save.
        await using (var mark = FreshContext())
        {
            var loaded = (await new HabitRepository(mark).GetByIdWithRecordsAsync(_user, habit.Id, default))!;
            loaded.MarkCompleted(new DateOnly(2026, 10, 8));
            await ((IUnitOfWork)mark).SaveChangesAsync();
        }
        Assert.Equal(1, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit.Id));

        await using (var unmark = FreshContext())
        {
            var loaded = (await new HabitRepository(unmark).GetByIdWithRecordsAsync(_user, habit.Id, default))!;
            loaded.UnmarkCompleted(new DateOnly(2026, 10, 8));
            await ((IUnitOfWork)unmark).SaveChangesAsync();
        }
        Assert.Equal(0, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit.Id));
    }

    [Fact]
    public async Task RemovingAHabit_RemovesItsRecordsToo()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1));
        habit.MarkCompleted(new DateOnly(2026, 10, 8));
        var repository = new HabitRepository(_db);
        repository.Add(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        repository.Remove(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        Assert.Equal(0, await Sql.CountAsync(_db, "Habits", "Id", habit.Id));
        Assert.Equal(0, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit.Id));
    }

    // ---- daily diaries ----

    [Fact]
    public async Task ADiary_RoundTripsThroughTheDatabase_WithEveryPart()
    {
        var tasks = new[] { DiaryTask.Create("Call mum", true), DiaryTask.Create("Run", false) };
        var diary = DailyDiary.Create(_user, new DateOnly(2026, 10, 8),
            Content(Mood.Good, 70, 0, "A good day", ["coffee", "sun", "coffee"], ["some Rust"], tasks));
        new DailyDiaryRepository(_db).Add(diary);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        await using var other = FreshContext();
        var loaded = (await new DailyDiaryRepository(other).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;

        Assert.Equal(Mood.Good, loaded.Mood);
        Assert.Equal(Percentage.Create(70), loaded.Body);
        Assert.Equal(Percentage.Create(0), loaded.Mind);
        Assert.Equal("A good day", loaded.Highlight);
        Assert.Equal(["coffee", "sun", "coffee"], loaded.Grateful);
        Assert.Equal(["some Rust"], loaded.Learned);
        Assert.Equal(tasks, loaded.Tasks);
    }

    [Fact]
    public async Task ADiaryWithOnlyAMood_LoadsWithNullsAndEmptyLists()
    {
        new DailyDiaryRepository(_db).Add(DailyDiary.Create(_user, new DateOnly(2026, 10, 8), Content(mood: Mood.Bad)));
        await ((IUnitOfWork)_db).SaveChangesAsync();

        await using var other = FreshContext();
        var loaded = (await new DailyDiaryRepository(other).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;

        Assert.Equal(Mood.Bad, loaded.Mood);
        Assert.Null(loaded.Body);
        Assert.Null(loaded.Mind);
        Assert.Null(loaded.Highlight);
        Assert.Empty(loaded.Grateful);
        Assert.Empty(loaded.Learned);
        Assert.Empty(loaded.Tasks);
    }

    [Fact]
    public async Task ADiary_IsHandedOutOnlyToItsOwner_AndOnlyForItsDay()
    {
        var repository = new DailyDiaryRepository(_db);
        repository.Add(DailyDiary.Create(_user, new DateOnly(2026, 10, 8), Content(mood: Mood.Ok)));
        await ((IUnitOfWork)_db).SaveChangesAsync();

        Assert.NotNull(await repository.GetByDateAsync(_user, new DateOnly(2026, 10, 8), default));
        Assert.Null(await repository.GetByDateAsync(_user, new DateOnly(2026, 10, 9), default));
        Assert.Null(await repository.GetByDateAsync(await Sql.InsertUserAsync(_db), new DateOnly(2026, 10, 8), default));
    }

    [Fact]
    public async Task ReplacingADiary_WritesTheNewDocument_AndClearsTheOldParts()
    {
        var repository = new DailyDiaryRepository(_db);
        var diary = DailyDiary.Create(_user, new DateOnly(2026, 10, 8),
            Content(Mood.Good, 70, 40, "old", ["a"], ["b"], [DiaryTask.Create("t", false)]));
        repository.Add(diary);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        await using var edit = FreshContext();
        var loaded = (await new DailyDiaryRepository(edit).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;
        loaded.Replace(Content(highlight: "new"));
        await ((IUnitOfWork)edit).SaveChangesAsync();

        await using var check = FreshContext();
        var after = (await new DailyDiaryRepository(check).GetByDateAsync(_user, new DateOnly(2026, 10, 8), default))!;
        Assert.Null(after.Mood);
        Assert.Null(after.Body);
        Assert.Equal("new", after.Highlight);
        Assert.Empty(after.Grateful);
        Assert.Empty(after.Tasks);
    }

    // ---- EF must notice every change of a list - and none that is not one ----

    private async Task<DailyDiary> StoredDiary(DiaryContent content)
    {
        var diary = DailyDiary.Create(_user, new DateOnly(2026, 10, 8), content);
        new DailyDiaryRepository(_db).Add(diary);
        await ((IUnitOfWork)_db).SaveChangesAsync();
        return diary;
    }

    private static async Task<(DailyDiary Diary, ApplicationDbContext Context)> LoadForEdit(TestEnvironment environment, Guid user)
    {
        var context = environment.CreateContext();
        return ((await new DailyDiaryRepository(context).GetByDateAsync(user, new DateOnly(2026, 10, 8), default))!, context);
    }

    [Fact]
    public async Task SavingTheSameContentAgain_IsNotAChange_NoUpdateIsSent()
    {
        await StoredDiary(Content(Mood.Good, highlight: "same", grateful: ["a", "b"], tasks: [DiaryTask.Create("t", true)]));
        var (diary, context) = await LoadForEdit(_environment, _user);
        await using var _ = context;

        diary.Replace(Content(Mood.Good, highlight: "same", grateful: ["a", "b"], tasks: [DiaryTask.Create("t", true)]));
        context.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Unchanged, context.Entry(diary).State);
        Assert.False(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ChangingJustTheOrderOfAList_IsAChange_AndIsWritten()
    {
        await StoredDiary(Content(grateful: ["a", "b", "c"], learned: ["x", "y"], tasks: [DiaryTask.Create("1", false), DiaryTask.Create("2", false)]));
        var (diary, context) = await LoadForEdit(_environment, _user);
        await using (context)
        {
            diary.Replace(Content(grateful: ["c", "b", "a"], learned: ["y", "x"], tasks: [DiaryTask.Create("2", false), DiaryTask.Create("1", false)]));
            context.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Modified, context.Entry(diary).State);
            await ((IUnitOfWork)context).SaveChangesAsync();
        }

        var (reloaded, check) = await LoadForEdit(_environment, _user);
        await using var _ = check;
        Assert.Equal(["c", "b", "a"], reloaded.Grateful);
        Assert.Equal(["y", "x"], reloaded.Learned);
        Assert.Equal(["2", "1"], reloaded.Tasks.Select(t => t.Title));
    }

    [Fact]
    public async Task TickingATaskOff_IsAChange_AndIsWritten()
    {
        await StoredDiary(Content(tasks: [DiaryTask.Create("Call mum", false)]));
        var (diary, context) = await LoadForEdit(_environment, _user);
        await using (context)
        {
            diary.Replace(Content(tasks: [DiaryTask.Create("Call mum", true)]));
            await ((IUnitOfWork)context).SaveChangesAsync();
        }

        var (reloaded, check) = await LoadForEdit(_environment, _user);
        await using var _ = check;
        Assert.True(Assert.Single(reloaded.Tasks).Done);
    }

    // ---- resolutions ----

    [Fact]
    public async Task AResolution_RoundTrips_AndIsHandedOutOnlyForItsOwnerAndYear()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1));
        new HabitRepository(_db).Add(habit);
        var resolution = Resolution.Create(_user, 2026, "Read 12 books", habit.Id, resolutionsInYear: 0);
        resolution.Update("Read 12 books", kept: true, habit.Id);
        var repository = new ResolutionRepository(_db);
        repository.Add(resolution);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        await using var other = FreshContext();
        var reader = new ResolutionRepository(other);
        var loaded = (await reader.GetByIdAsync(_user, 2026, resolution.Id, default))!;

        Assert.Equal("Read 12 books", loaded.Title);
        Assert.True(loaded.Kept);
        Assert.Equal(habit.Id, loaded.HabitId);
        Assert.Null(await reader.GetByIdAsync(_user, 2027, resolution.Id, default));                          // wrong year
        Assert.Null(await reader.GetByIdAsync(await Sql.InsertUserAsync(_db), 2026, resolution.Id, default)); // wrong user
    }

    [Fact]
    public async Task CountForYear_CountsOnlyTheUsersOwnResolutionsOfThatYear()
    {
        var stranger = await Sql.InsertUserAsync(_db);
        await Sql.InsertResolutionAsync(_db, _user, 2026);
        await Sql.InsertResolutionAsync(_db, _user, 2026);
        await Sql.InsertResolutionAsync(_db, _user, 2027);
        await Sql.InsertResolutionAsync(_db, stranger, 2026);
        var repository = new ResolutionRepository(_db);

        Assert.Equal(2, await repository.CountForYearAsync(_user, 2026, default));
        Assert.Equal(1, await repository.CountForYearAsync(_user, 2027, default));
        Assert.Equal(0, await repository.CountForYearAsync(_user, 2028, default));
        Assert.Equal(1, await repository.CountForYearAsync(stranger, 2026, default));
    }

    [Fact]
    public async Task DeletingAHabit_WhileItsResolutionIsLoaded_ClearsTheLinkInMemoryAndInTheDatabase()
    {
        var habit = Habit.Create(_user, HabitTitle.Create("Read"), new DateOnly(2026, 3, 1));
        _db.Habits.Add(habit);
        var resolution = Resolution.Create(_user, 2026, "Read 12 books", habit.Id, resolutionsInYear: 0);
        _db.Resolutions.Add(resolution);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        // Same context: both entities are tracked, so EF itself applies "set null" to the loaded resolution. (The other
        // case - the resolution is not loaded, the database does it - is covered by the schema tests.)
        _db.Habits.Remove(habit);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        Assert.Null(resolution.HabitId);
        await using var check = FreshContext();
        var stored = await check.Resolutions.AsNoTracking().SingleAsync(r => r.Id == resolution.Id);
        Assert.Null(stored.HabitId);
        Assert.Equal("Read 12 books", stored.Title);
    }

    [Fact]
    public async Task RemovingAResolution_DeletesIt()
    {
        var resolution = Resolution.Create(_user, 2026, "Read", null, 0);
        var repository = new ResolutionRepository(_db);
        repository.Add(resolution);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        repository.Remove(resolution);
        await ((IUnitOfWork)_db).SaveChangesAsync();

        Assert.Equal(0, await Sql.CountAsync(_db, "Resolutions", "Id", resolution.Id));
    }
}
