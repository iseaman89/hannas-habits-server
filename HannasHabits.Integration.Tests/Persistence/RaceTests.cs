using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Common.Interfaces;
using HannasHabits.Application.DailyDiaries;
using HannasHabits.Application.DailyDiaries.Commands.SaveDailyDiary;
using HannasHabits.Application.HabitRecords.Commands.MarkCompleted;
using HannasHabits.Application.HabitRecords.Commands.UnmarkCompleted;
using HannasHabits.Application.Resolutions.Commands.AddResolution;
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
}
