using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;
using HannasHabits.Application.HabitDays.Commands.AttachTag;
using HannasHabits.Application.HabitDays.Commands.DetachTag;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;
using HannasHabits.Application.Resolutions.Commands.AddResolution;
using HannasHabits.Application.SubHabits;
using HannasHabits.Application.SubHabits.Commands.MarkSubHabitCompleted;
using HannasHabits.Application.Tags;
using HannasHabits.Application.Tags.Commands.CreateTag;
using HannasHabits.Domain.Enums;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Infrastructure.Queries;
using HannasHabits.Infrastructure.Repositories;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HannasHabits.Integration.Tests.Persistence;

// Concurrency that is forced rather than hoped for. Two requests ("scopes", each with a DbContext of its own) both load
// their data; only then does either of them save (RaceUnitOfWork waits at a Barrier). So the interleaving "both looked,
// both write" happens every single time - and the test shows what the database and the handler make of it.
[Collection(IntegrationCollection.Name)]
public class RaceTests : IAsyncLifetime
{
    private const int Rounds = 10;
    private static readonly DateOnly Day = new(2026, 10, 8);

    private readonly TestEnvironment _environment;
    private ServiceProvider _provider = null!;
    private ApplicationDbContext _db = null!;
    private Guid _user;

    public RaceTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _provider = TestServices.Build(_environment.SharedConnectionString);
        _db = _environment.CreateContext();
        _user = await Sql.InsertUserAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private static async Task<Exception?> Outcome(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>One request: its own scope, so its own DbContext, with the unit of work the test controls.</summary>
    private async Task<T> Request<T>(Func<IServiceScope, ApplicationDbContext, Task<T>> body)
    {
        using var scope = _provider.CreateScope();
        return await body(scope, scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    // ---- marking a day: two requests, one unique index ----

    [Fact]
    public async Task MarkingTheSameDayTwiceAtOnce_EndsInOneRecord_AndBothCallersGetIt()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            var barrier = new Barrier(2);
            var unitsOfWork = new List<RaceUnitOfWork>();

            Task<HabitRecordDto> Mark() => Request(async (_, db) =>
            {
                var uow = new RaceUnitOfWork(db, barrier);
                lock (unitsOfWork) unitsOfWork.Add(uow);
                var handler = new MarkCompletedCommandHandler(new HabitRepository(db), new HabitQueries(db), uow, new FakeCurrentUser(_user), TestMapper.Instance);
                return await handler.Handle(new MarkCompletedCommand(habit, Day), default);
            });

            var results = await Task.WhenAll(Task.Run(Mark), Task.Run(Mark));

            Assert.Equal(results[0].Id, results[1].Id);                                      // the same record for both
            Assert.Equal(1, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit));    // and only one row
            Assert.Single(unitsOfWork, uow => uow.SaveFailure is DuplicateEntryException);   // the unique index really fired
        }
    }

    [Fact]
    public async Task UnmarkingTheSameDayTwiceAtOnce_OneSucceeds_TheOtherIsToldToRetry()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            await Sql.InsertRecordAsync(_db, habit, "2026-10-08");
            var barrier = new Barrier(2);

            Task Unmark() => Request(async (_, db) =>
            {
                var handler = new UnmarkCompletedCommandHandler(new HabitRepository(db), new RaceUnitOfWork(db, barrier), new FakeCurrentUser(_user));
                await handler.Handle(new UnmarkCompletedCommand(habit, Day), default);
                return 0;
            });

            var outcomes = await Task.WhenAll(Outcome(Unmark), Outcome(Unmark));

            Assert.Single(outcomes, outcome => outcome is null);
            Assert.Single(outcomes, outcome => outcome is ConflictException);  // 0 rows deleted: "changed by another request" (409)
            Assert.Equal(0, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit));
        }
    }

    // ---- sub-habits: the part's tick and the habit's day ----

    private Task<(HabitDayDto? Result, Exception? Failure)> TickPart(
        Barrier barrier, List<RaceUnitOfWork> units, Guid habit, Guid part)
        => Race(barrier, units, (db, uow) => new MarkSubHabitCompletedCommandHandler(
                new HabitRepository(db), new HabitQueries(db), uow, new FakeCurrentUser(_user))
            .Handle(new MarkSubHabitCompletedCommand(habit, part, Day), default));

    [Fact]
    public async Task TickingTheSamePartTwiceAtOnce_EndsInOneTick_AndBothCallersGetTheCompletedDay()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
            var barrier = new Barrier(2);
            var units = new List<RaceUnitOfWork>();

            var results = await Task.WhenAll(
                Task.Run(() => TickPart(barrier, units, habit, legs)),
                Task.Run(() => TickPart(barrier, units, habit, legs)));

            Assert.All(results, result => Assert.Null(result.Failure));
            Assert.All(results, result => Assert.True(result.Result!.HabitCompleted));        // both see the day done
            Assert.All(results, result => Assert.Equal([legs], result.Result!.CompletedSubHabitIds));
            Assert.Equal(1, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", legs)); // one tick...
            Assert.Equal(1, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit));      // ...and one day
            Assert.Single(units, uow => uow.SaveFailure is DuplicateEntryException);          // the unique index really fired
        }
    }

    // KNOWN LIMIT, pinned here so that it is not forgotten: the habit's day follows its parts when a part is ticked,
    // by looking at the parts this request loaded. Two requests that tick the last two open parts at the same moment
    // each see the other one still open, neither completes the day, and nothing in the database objects (no unique
    // index is touched). Both ticks are kept; the habit's day stays open until a tick or the habit's own tick touches
    // the day again. One person on one device cannot do this (the UI ticks one after the other); it takes two devices
    // within the same few milliseconds. Closing it would need a row version on the habit or a re-check after the save.
    [Fact]
    public async Task TickingTheTwoLastOpenPartsAtOnce_KeepsBothTicks_ButCannotCompleteTheDay()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        var core = await Sql.InsertSubHabitAsync(_db, habit, "Core");
        var barrier = new Barrier(2);
        var units = new List<RaceUnitOfWork>();

        var results = await Task.WhenAll(
            Task.Run(() => TickPart(barrier, units, habit, legs)),
            Task.Run(() => TickPart(barrier, units, habit, core)));

        Assert.All(results, result => Assert.Null(result.Failure));
        Assert.All(units, uow => Assert.Null(uow.SaveFailure));
        Assert.Equal(1, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", legs));
        Assert.Equal(1, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", core));
        Assert.Equal(0, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit)); // the limit
    }

    // ---- the diary: last write wins, as a whole ----

    private static SaveDailyDiaryCommand Entry(string highlight, Mood mood, params string[] grateful)
        => new(Day, mood, null, null, highlight, grateful, null, null);

    private static SaveDailyDiaryCommand Empty() => new(Day, null, null, null, null, null, null, null);

    private Task<(RaceUnitOfWork Unit, Exception? Failure)> SaveDiary(Barrier? barrier, SaveDailyDiaryCommand command, Func<Task>? beforeSave = null)
        => Request(async (_, db) =>
        {
            var uow = new RaceUnitOfWork(db, barrier, beforeSave);
            var handler = new SaveDailyDiaryCommandHandler(new DailyDiaryRepository(db), uow, new FakeCurrentUser(_user));
            var failure = await Outcome(() => handler.Handle(command, default));
            return (uow, failure);
        });

    [Fact]
    public async Task TheFirstSaveOfADayTwiceAtOnce_EndsInOneWholeDocument_TheLaterWriterWins()
    {
        for (var round = 0; round < Rounds; round++)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "DailyDiary" WHERE "UserId" = {_user}""");
            var barrier = new Barrier(2);

            var results = await Task.WhenAll(
                Task.Run(() => SaveDiary(barrier, Entry("first writer", Mood.Terrible, "a1", "a2"))),
                Task.Run(() => SaveDiary(barrier, Entry("second writer", Mood.Excellent, "b1"))));

            Assert.All(results, result => Assert.Null(result.Failure));                       // both saves report success
            var loser = Assert.Single(results, result => result.Unit.SaveFailure is DuplicateEntryException); // one hit the unique index

            await using var check = _environment.CreateContext();
            var stored = await new DailyDiaryQueries(check).GetByDateAsync(_user, Day, default);
            Assert.Equal(1, await Sql.CountAsync(_db, "DailyDiary", "UserId", _user));
            Assert.NotNull(stored);

            // The one who lost the insert race wrote second (onto the winner's row), so its document is the stored one -
            // complete, never a mixture of the two.
            var loserWrote = results[0] == loser ? Entry("first writer", Mood.Terrible, "a1", "a2") : Entry("second writer", Mood.Excellent, "b1");
            Assert.Equal(loserWrote.Highlight, stored.Highlight);
            Assert.Equal(loserWrote.Mood, stored.Mood);
            Assert.Equal(loserWrote.Grateful, stored.Grateful);
        }
    }

    [Fact]
    public async Task ErasingTheSameDayTwiceAtOnce_BothSucceed_AndTheDayIsEmpty()
    {
        for (var round = 0; round < Rounds; round++)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "DailyDiary" WHERE "UserId" = {_user}""");
            await Sql.InsertDiaryAsync(_db, _user, "2026-10-08");
            var barrier = new Barrier(2);

            var results = await Task.WhenAll(Task.Run(() => SaveDiary(barrier, Empty())), Task.Run(() => SaveDiary(barrier, Empty())));

            Assert.All(results, result => Assert.Null(result.Failure));                       // asked for "empty", got "empty"
            Assert.Single(results, result => result.Unit.SaveFailure is ConflictException);   // the lost delete was swallowed
            Assert.Equal(0, await Sql.CountAsync(_db, "DailyDiary", "UserId", _user));
        }
    }

    [Fact]
    public async Task SavingContent_WhileAnotherRequestDeletesTheDay_IsAConflictTheClientCanRetry()
    {
        await Sql.InsertDiaryAsync(_db, _user, "2026-10-08");

        var (_, failure) = await SaveDiary(barrier: null, Entry("my edit", Mood.Good), beforeSave: async () =>
        {
            // Another request removes the entry after we looked it up and before we write.
            await using var other = _environment.CreateContext();
            await other.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "DailyDiary" WHERE "UserId" = {_user}""");
        });

        Assert.IsAssignableFrom<ConflictException>(failure);
        Assert.Equal(0, await Sql.CountAsync(_db, "DailyDiary", "UserId", _user));
    }

    // ---- a resolution whose habit disappears while it is being saved ----

    [Fact]
    public async Task LinkingAHabitThatIsDeletedJustBeforeTheSave_IsAConflict_AndStoresNothing()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user, "Read every day");

        var failure = await Outcome(() => Request(async (_, db) =>
        {
            var uow = new RaceUnitOfWork(db, barrier: null, beforeFirstSave: async () =>
            {
                await using var other = _environment.CreateContext();
                await other.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Habits" WHERE "Id" = {habit}""");
            });
            var handler = new AddResolutionCommandHandler(new ResolutionRepository(db), new HabitRepository(db), uow, new FakeCurrentUser(_user));
            return await handler.Handle(new AddResolutionCommand(2026, "Read 12 books", habit), default);
        }));

        // Without the translation of the foreign key violation this would be a raw DbUpdateException, i.e. a 500.
        Assert.IsAssignableFrom<ConflictException>(failure);
        Assert.Equal(0, await Sql.CountAsync(_db, "Resolutions", "UserId", _user));
    }

    // ---- tags ----

    /// <summary>One request that must not throw out of the test: its result or the exception it ended with.</summary>
    private Task<(T? Result, Exception? Failure)> Race<T>(
        Barrier barrier, List<RaceUnitOfWork> units, Func<ApplicationDbContext, IUnitOfWork, Task<T>> body)
        => Request(async (_, db) =>
        {
            var uow = new RaceUnitOfWork(db, barrier);
            lock (units) units.Add(uow);

            try
            {
                return ((T?)await body(db, uow), (Exception?)null);
            }
            catch (Exception exception)
            {
                return (default(T), exception);
            }
        });

    private Task<(CreateTagResult? Result, Exception? Failure)> CreateTag(
        Barrier barrier, List<RaceUnitOfWork> units, string name)
        => Race(barrier, units, (db, uow) => new CreateTagCommandHandler(new TagRepository(db), uow, new FakeCurrentUser(_user))
            .Handle(new CreateTagCommand(name), default));

    private Task<(TaggedDayDto? Result, Exception? Failure)> Attach(
        Barrier barrier, List<RaceUnitOfWork> units, Guid habit, Guid tag)
        => Race(barrier, units, (db, uow) => new AttachTagCommandHandler(
                new HabitRepository(db), new TagRepository(db), new HabitDayEntryRepository(db), new TagQueries(db), uow,
                new FakeCurrentUser(_user))
            .Handle(new AttachTagCommand(habit, Day, tag), default));

    private Task<(TaggedDayDto? Result, Exception? Failure)> Detach(
        Barrier barrier, List<RaceUnitOfWork> units, Guid habit, Guid tag)
        => Race(barrier, units, (db, uow) => new DetachTagCommandHandler(
                new HabitRepository(db), new TagRepository(db), new HabitDayEntryRepository(db), new TagQueries(db), uow,
                new FakeCurrentUser(_user))
            .Handle(new DetachTagCommand(habit, Day, tag), default));

    private Task<long> TagRows(Guid habit)
        => Sql.ScalarAsync(_db, """
            SELECT COUNT(*) FROM "HabitDayTags" t JOIN "HabitDayEntries" e ON e."Id" = t."HabitDayEntryId" WHERE e."HabitId" = @p0
            """, habit);

    [Fact]
    public async Task CreatingTheSameTagTwiceAtOnce_EndsInOneTag_AndBothCallersGetIt()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var barrier = new Barrier(2);
            var units = new List<RaceUnitOfWork>();

            // Not even the spelling has to match: the unique index is on the case-folded name.
            var results = await Task.WhenAll(
                Task.Run(() => CreateTag(barrier, units, $"Krank {round}")),
                Task.Run(() => CreateTag(barrier, units, $"KRANK {round}")));

            Assert.All(results, result => Assert.Null(result.Failure));
            Assert.Equal(results[0].Result!.Tag.Id, results[1].Result!.Tag.Id);               // the same tag for both
            Assert.Single(results, result => result.Result!.Created);                         // one created it, one found it
            Assert.Equal(results[0].Result!.Tag.Name, results[1].Result!.Tag.Name);           // and both see ITS spelling
            Assert.Equal(1, await Sql.ScalarAsync(_db,
                "SELECT COUNT(*) FROM \"Tags\" WHERE \"UserId\" = @p0 AND \"NameKey\" = @p1", _user, $"KRANK {round}"));
            Assert.Single(units, uow => uow.SaveFailure is DuplicateEntryException);          // the unique index really fired
        }
    }

    [Fact]
    public async Task TaggingTheSameNewDayWithTwoTagsAtOnce_EndsInOneEntryWithBothTags()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            var first = await Sql.InsertTagAsync(_db, _user, $"first {round}");
            var second = await Sql.InsertTagAsync(_db, _user, $"second {round}");
            var barrier = new Barrier(2);
            var units = new List<RaceUnitOfWork>();

            var results = await Task.WhenAll(
                Task.Run(() => Attach(barrier, units, habit, first)),
                Task.Run(() => Attach(barrier, units, habit, second)));

            Assert.All(results, result => Assert.Null(result.Failure));
            Assert.Equal(1, await Sql.CountAsync(_db, "HabitDayEntries", "HabitId", habit)); // one entry for the day...
            Assert.Equal(2, await TagRows(habit));                                            // ...with both tags
            Assert.Single(units, uow => uow.SaveFailure is DuplicateEntryException);          // the unique index really fired
            Assert.Contains(results, result => result.Result!.Tags.Count == 2);               // the loser answers with the whole day
        }
    }

    [Fact]
    public async Task PuttingTheSameTagOnTheSameNewDayTwiceAtOnce_EndsInOneRow_AndBothSucceed()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            var tag = await Sql.InsertTagAsync(_db, _user, $"tag {round}");
            var barrier = new Barrier(2);
            var units = new List<RaceUnitOfWork>();

            var results = await Task.WhenAll(
                Task.Run(() => Attach(barrier, units, habit, tag)),
                Task.Run(() => Attach(barrier, units, habit, tag)));

            Assert.All(results, result => Assert.Null(result.Failure));
            Assert.All(results, result => Assert.Equal([tag], result.Result!.Tags.Select(t => t.Id)));
            Assert.Equal(1, await TagRows(habit));
            Assert.Single(units, uow => uow.SaveFailure is DuplicateEntryException);
        }
    }

    [Fact]
    public async Task PuttingTheSameTagOnAnExistingDayTwiceAtOnce_EndsInOneRow_AndBothSucceed()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            var already = await Sql.InsertTagAsync(_db, _user, $"already {round}");
            var tag = await Sql.InsertTagAsync(_db, _user, $"tag {round}");
            await Sql.InsertDayTagAsync(_db, await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08"), already);
            var barrier = new Barrier(2);
            var units = new List<RaceUnitOfWork>();

            var results = await Task.WhenAll(
                Task.Run(() => Attach(barrier, units, habit, tag)),
                Task.Run(() => Attach(barrier, units, habit, tag)));

            Assert.All(results, result => Assert.Null(result.Failure));
            Assert.All(results, result => Assert.Equal(2, result.Result!.Tags.Count));
            Assert.Equal(2, await TagRows(habit));
            Assert.Equal(1, await Sql.CountAsync(_db, "HabitDayEntries", "HabitId", habit));
            Assert.Single(units, uow => uow.SaveFailure is DuplicateEntryException);
        }
    }

    [Theory]
    [InlineData(false)] // the day keeps another tag
    [InlineData(true)]  // the tag is the day's last: the entry goes with it, from both requests
    public async Task TakingTheSameTagOffTheSameDayTwiceAtOnce_BothSucceed_AndTheDayEndsWithoutIt(bool lastTag)
    {
        for (var round = 0; round < Rounds; round++)
        {
            var habit = await Sql.InsertHabitAsync(_db, _user);
            var tag = await Sql.InsertTagAsync(_db, _user, $"tag {round}");
            var entry = await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08");
            await Sql.InsertDayTagAsync(_db, entry, tag);
            if (!lastTag)
                await Sql.InsertDayTagAsync(_db, entry, await Sql.InsertTagAsync(_db, _user, $"other {round}"));
            var barrier = new Barrier(2);
            var units = new List<RaceUnitOfWork>();

            var results = await Task.WhenAll(
                Task.Run(() => Detach(barrier, units, habit, tag)),
                Task.Run(() => Detach(barrier, units, habit, tag)));

            Assert.All(results, result => Assert.Null(result.Failure));                       // the second one is not told off
            Assert.Single(units, uow => uow.SaveFailure is ConflictException);                // 0 rows deleted: it really raced
            Assert.Equal(lastTag ? 0 : 1, await TagRows(habit));
            Assert.Equal(lastTag ? 0 : 1, await Sql.CountAsync(_db, "HabitDayEntries", "HabitId", habit));
        }
    }
}
