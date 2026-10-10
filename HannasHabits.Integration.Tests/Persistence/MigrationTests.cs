using HannasHabits.Domain.Enums;
using HannasHabits.Domain.ValueObjects;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace HannasHabits.Integration.Tests.Persistence;

// The migrations are the only way the schema changes in production, so they are tested like code: against a database
// that starts out empty, and - for the ones that move data - against rows that exist before they run.
[Collection(IntegrationCollection.Name)]
public class MigrationTests
{
    private const string AddHabitSchedule = "20261008084030_AddHabitSchedule";
    private const string AddDisplayName = "20261008093012_AddDisplayName";
    private const string AddResolutions = "20261008095909_AddResolutions";
    private const string AddHabitStartDate = "20261008100741_AddHabitStartDate";
    private const string SplitDisplayName = "20261009071927_SplitDisplayName";
    private const string AddTags = "20261010112830_AddTags";

    private readonly TestEnvironment _environment;

    public MigrationTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    private async Task<ApplicationDbContext> BlankDatabaseAsync() => _environment.CreateContext(await _environment.CreateBlankDatabaseAsync());

    private static Task MigrateTo(ApplicationDbContext db, string? target = null) => db.GetService<IMigrator>().MigrateAsync(target);

    private static async Task<bool> ColumnExistsAsync(ApplicationDbContext db, string table, string column)
        => await Sql.ScalarAsync(db, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @p0 AND column_name = @p1", table, column) == 1;

    // ---- from scratch ----

    [Fact]
    public async Task AllMigrations_ApplyToAnEmptyDatabase_AndTheyAreRecordedInOrder()
    {
        await using var db = await BlankDatabaseAsync();

        await db.Database.MigrateAsync();

        var all = db.Database.GetMigrations().ToList();
        Assert.True(all.Count >= 11);
        Assert.Equal(all, await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(all.Order(), all); // the ids sort by time: the order they run in
    }

    [Fact]
    public async Task MigratingAnUpToDateDatabase_ChangesNothing()
    {
        await using var db = await BlankDatabaseAsync();
        await db.Database.MigrateAsync();
        var before = await db.Database.GetAppliedMigrationsAsync();

        await db.Database.MigrateAsync();

        Assert.Equal(before, await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task TheMigratedSchemaIsUsableByTheModel()
    {
        await using var db = await BlankDatabaseAsync();
        await db.Database.MigrateAsync();

        // Every table of the model can be queried (a column the model expects but the migrations never created fails here).
        Assert.Equal(0, await db.Habits.CountAsync());
        Assert.Equal(0, await db.HabitRecords.CountAsync());
        Assert.Equal(0, await db.DailyDiaries.CountAsync());
        Assert.Equal(0, await db.Resolutions.CountAsync());
        Assert.Equal(0, await db.RefreshTokens.CountAsync());
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task TheModelAndTheMigrationsAgree_NoMigrationIsMissing()
    {
        // The test version of "dotnet ef migrations has-pending-model-changes": diff the snapshot of the last migration
        // against the model as the code defines it today.
        await using var db = _environment.CreateContext();
        var services = ((IInfrastructure<IServiceProvider>)db).Instance;

        var snapshot = services.GetRequiredService<IMigrationsAssembly>().ModelSnapshot?.Model;
        Assert.NotNull(snapshot);
        if (snapshot is Microsoft.EntityFrameworkCore.Metadata.IMutableModel mutable)
            snapshot = mutable.FinalizeModel();
        snapshot = services.GetRequiredService<IModelRuntimeInitializer>().Initialize(snapshot);

        var current = services.GetRequiredService<IDesignTimeModel>().Model;
        var differences = services.GetRequiredService<IMigrationsModelDiffer>()
            .GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel());

        Assert.True(differences.Count == 0,
            "The model changed without a migration: " + string.Join(", ", differences.Select(d => d.GetType().Name)));
    }

    [Fact]
    public async Task EveryMigrationCanBeRolledBack_AndAppliedAgain()
    {
        await using var db = await BlankDatabaseAsync();
        await db.Database.MigrateAsync();
        var all = db.Database.GetMigrations().ToList();

        // One step at a time, so a broken Down names the migration that is at fault.
        for (var i = all.Count - 1; i >= 0; i--)
        {
            var target = i == 0 ? "0" : all[i - 1];
            await MigrateTo(db, target);
            Assert.Equal(all.Take(i), await db.Database.GetAppliedMigrationsAsync());
        }

        await db.Database.MigrateAsync();

        Assert.Equal(all, await db.Database.GetAppliedMigrationsAsync());
    }

    // ---- migrations that move data ----

    [Fact]
    public async Task AddHabitStartDate_GivesExistingHabitsTheUtcDayTheyWereCreatedOn_AndLeavesNoDefaultBehind()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, AddResolutions);
        var user = await Sql.InsertUserAsync(db);
        var beforeMidnight = (Id: Guid.NewGuid(), At: new DateTime(2026, 10, 7, 23, 30, 0, DateTimeKind.Utc));
        var afterMidnight = (Id: Guid.NewGuid(), At: new DateTime(2026, 10, 8, 0, 30, 0, DateTimeKind.Utc));
        var otherZone = (Id: Guid.NewGuid(), At: new DateTimeOffset(2026, 10, 7, 23, 30, 0, TimeSpan.FromHours(-5)).UtcDateTime); // = 04:30 UTC on the 8th

        foreach (var (id, at) in new[] { beforeMidnight, afterMidnight, otherZone })
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO "Habits" ("Id", "CreatedAt", "UserId", "Title", "Schedule") VALUES ({id}, {at}, {user}, 'Old habit', 127)""");

        await MigrateTo(db);

        async Task<DateOnly> StartOf(Guid id) => (await db.Database.SqlQuery<DateOnly>($"""SELECT "StartDate" AS "Value" FROM "Habits" WHERE "Id" = {id}""").ToListAsync()).Single();
        Assert.Equal(new DateOnly(2026, 10, 7), await StartOf(beforeMidnight.Id));
        Assert.Equal(new DateOnly(2026, 10, 8), await StartOf(afterMidnight.Id));
        Assert.Equal(new DateOnly(2026, 10, 8), await StartOf(otherZone.Id));

        // The default only existed to add the NOT NULL column; an insert that forgets the date must fail, not silently get 0001-01-01.
        var forgotten = await Sql.RejectedAsync(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Habits" ("Id", "CreatedAt", "UserId", "Title", "ScheduleKind", "ScheduleWeekdays", "ScheduleInterval")
            VALUES ({Guid.NewGuid()}, {DateTime.UtcNow}, {user}, 'x', 0, 127, 1)
            """));
        Assert.Equal("23502", forgotten.SqlState); // not_null_violation
    }

    [Fact]
    public async Task AddHabitStartDate_CanBeRolledBackAndAppliedAgain_WithTheDataFilledInAgain()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, AddResolutions);
        var user = await Sql.InsertUserAsync(db);
        var habit = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "Habits" ("Id", "CreatedAt", "UserId", "Title", "Schedule") VALUES ({habit}, {new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)}, {user}, 'Old habit', 127)""");
        await MigrateTo(db);

        await MigrateTo(db, AddResolutions);
        Assert.False(await ColumnExistsAsync(db, "Habits", "StartDate"));
        Assert.Equal(1, await Sql.CountAsync(db, "Habits", "Id", habit)); // the habit itself survives the rollback

        await MigrateTo(db);
        var start = (await db.Database.SqlQuery<DateOnly>($"""SELECT "StartDate" AS "Value" FROM "Habits" WHERE "Id" = {habit}""").ToListAsync()).Single();
        Assert.Equal(new DateOnly(2026, 3, 1), start);
    }

    [Fact]
    public async Task AddRecurrenceRules_MakesEveryExistingHabitWeeklyEveryWeek_OnTheDaysItAlreadyHad()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, SplitDisplayName);
        var user = await Sql.InsertUserAsync(db);
        var everyDay = Guid.NewGuid();
        var weekdays = Guid.NewGuid();
        var weekend = Guid.NewGuid();
        var sundayOnly = Guid.NewGuid();

        foreach (var (id, mask) in new[] { (everyDay, 127), (weekdays, 62), (weekend, 65), (sundayOnly, 1) })
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Habits" ("Id", "CreatedAt", "UserId", "Title", "Schedule", "StartDate")
                VALUES ({id}, {DateTime.UtcNow}, {user}, 'Old habit', {mask}, {new DateOnly(2026, 3, 1)})
                """);

        await MigrateTo(db);

        // Read through the model: the rule as the application sees it.
        var habits = await db.Habits.AsNoTracking().ToDictionaryAsync(h => h.Id, h => h.Schedule);
        Assert.Equal(HabitSchedule.Daily, habits[everyDay]);
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]), habits[weekdays]);
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Saturday, DayOfWeek.Sunday]), habits[weekend]);
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Sunday]), habits[sundayOnly]);
        Assert.All(habits.Values, schedule =>
        {
            Assert.Equal(ScheduleKind.Weekly, schedule.Kind);
            Assert.Equal(1, schedule.Interval);
            Assert.Null(schedule.Anchor);
        });

        // The weekday bits were moved by renaming the column, not copied: the numbers are the same.
        Assert.False(await ColumnExistsAsync(db, "Habits", "Schedule"));
        Assert.Equal(62, await Sql.ScalarAsync(db, "SELECT \"ScheduleWeekdays\" FROM \"Habits\" WHERE \"Id\" = @p0", weekdays));
    }

    [Fact]
    public async Task AddRecurrenceRules_CanBeRolledBackAndAppliedAgain_ARuleBecomesItsPossibleWeekdays()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, SplitDisplayName);
        var user = await Sql.InsertUserAsync(db);
        await MigrateTo(db);
        var weekly = await Sql.InsertHabitAsync(db, user, schedule: 62);
        var everyThreeDays = await Sql.InsertHabitAsync(db, user, schedule: 127, kind: 1, interval: 3, anchor: "2026-10-10");
        var lastSaturday = await Sql.InsertHabitAsync(db, user, schedule: 64, kind: 2, occurrence: 5);
        var the15th = await Sql.InsertHabitAsync(db, user, schedule: 127, kind: 3, dayOfMonth: 15);

        await MigrateTo(db, SplitDisplayName);

        // Back to the old schema: only weekday sets exist, and the check that guards them is back too.
        Assert.False(await ColumnExistsAsync(db, "Habits", "ScheduleKind"));
        Assert.False(await ColumnExistsAsync(db, "Habits", "ScheduleAnchor"));
        Assert.True(await ColumnExistsAsync(db, "Habits", "Schedule"));
        async Task<long> Mask(Guid id) => await Sql.ScalarAsync(db, "SELECT \"Schedule\" FROM \"Habits\" WHERE \"Id\" = @p0", id);
        Assert.Equal(62, await Mask(weekly));
        Assert.Equal(127, await Mask(everyThreeDays));
        Assert.Equal(64, await Mask(lastSaturday));
        Assert.Equal(127, await Mask(the15th));
        var invalid = await Sql.RejectedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Habits\" SET \"Schedule\" = 0 WHERE \"Id\" = {0}", weekly));
        Assert.Equal("CK_Habits_Schedule", invalid.ConstraintName);

        await MigrateTo(db);

        var habits = await db.Habits.AsNoTracking().ToDictionaryAsync(h => h.Id, h => h.Schedule);
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]), habits[weekly]);
        Assert.Equal(HabitSchedule.Daily, habits[everyThreeDays]); // the rule did not survive the round trip, the days did
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Saturday]), habits[lastSaturday]);
        Assert.Equal(HabitSchedule.Daily, habits[the15th]);
    }

    [Fact]
    public async Task AddQuotaRules_LeavesEveryExistingHabitAsItWas_WithoutACount()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, AddTags);
        var user = await Sql.InsertUserAsync(db);
        Assert.False(await ColumnExistsAsync(db, "Habits", "ScheduleTimes"));

        // Rows made with the schema before the quota: they must pass the widened check and read back unchanged.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Habits" ("Id", "CreatedAt", "UserId", "Title", "StartDate",
                                  "ScheduleKind", "ScheduleWeekdays", "ScheduleInterval", "ScheduleAnchor")
            VALUES ({Guid.NewGuid()}, {DateTime.UtcNow}, {user}, 'Every 3 days', {new DateOnly(2026, 3, 1)}, 1, 127, 3, {new DateOnly(2026, 3, 1)}),
                   ({Guid.NewGuid()}, {DateTime.UtcNow}, {user}, 'Mondays', {new DateOnly(2026, 3, 1)}, 0, 2, 1, NULL)
            """);

        await MigrateTo(db);

        Assert.True(await ColumnExistsAsync(db, "Habits", "ScheduleTimes"));
        var habits = await db.Habits.AsNoTracking().ToListAsync();
        Assert.Equal(2, habits.Count);
        Assert.All(habits, habit => Assert.Null(habit.Schedule.Times));
        Assert.Equal(HabitSchedule.EveryNDays(3, new DateOnly(2026, 3, 1)), habits.Single(h => h.Title.Value == "Every 3 days").Schedule);
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Monday]), habits.Single(h => h.Title.Value == "Mondays").Schedule);
    }

    [Fact]
    public async Task AddQuotaRules_CanBeRolledBackAndAppliedAgain_AQuotaBecomesADailyHabit()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db);
        var user = await Sql.InsertUserAsync(db);
        var perWeek = await Sql.InsertHabitAsync(db, user, schedule: 127, kind: 4, times: 3);
        var perMonth = await Sql.InsertHabitAsync(db, user, schedule: 127, kind: 5, times: 8);
        var weekly = await Sql.InsertHabitAsync(db, user, schedule: 62);

        await MigrateTo(db, AddTags);

        // The old schema has no quota: both are "every day" now, the count is gone, the other habit is untouched.
        Assert.False(await ColumnExistsAsync(db, "Habits", "ScheduleTimes"));
        async Task<long> Kind(Guid id) => await Sql.ScalarAsync(db, "SELECT \"ScheduleKind\" FROM \"Habits\" WHERE \"Id\" = @p0", id);
        async Task<long> Mask(Guid id) => await Sql.ScalarAsync(db, "SELECT \"ScheduleWeekdays\" FROM \"Habits\" WHERE \"Id\" = @p0", id);
        Assert.Equal((0, 127), (await Kind(perWeek), await Mask(perWeek)));
        Assert.Equal((0, 127), (await Kind(perMonth), await Mask(perMonth)));
        Assert.Equal((0, 62), (await Kind(weekly), await Mask(weekly)));
        var rejected = await Sql.RejectedAsync(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Habits\" SET \"ScheduleKind\" = 4 WHERE \"Id\" = {0}", weekly));
        Assert.Equal("CK_Habits_ScheduleRule", rejected.ConstraintName); // the narrower check is back

        await MigrateTo(db);

        var habits = await db.Habits.AsNoTracking().ToDictionaryAsync(h => h.Id, h => h.Schedule);
        Assert.Equal(HabitSchedule.Daily, habits[perWeek]);
        Assert.Equal(HabitSchedule.Daily, habits[perMonth]);
        Assert.Equal(HabitSchedule.Weekly([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]), habits[weekly]);
    }

    [Fact]
    public async Task ExtendDailyDiary_KeepsTheTextOfRealUsers_AsTheHighlight_AndDropsOrphans()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, AddDisplayName);
        var user = await Sql.InsertUserAsync(db);
        var kept = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "DailyDiary" ("Id", "CreatedAt", "UserId", "Date", "Text") VALUES ({kept}, {DateTime.UtcNow}, {user}, {new DateOnly(2026, 10, 1)}, 'a real entry')""");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO "DailyDiary" ("Id", "CreatedAt", "UserId", "Date", "Text") VALUES ({orphan}, {DateTime.UtcNow}, {Guid.NewGuid()}, {new DateOnly(2026, 10, 1)}, 'some text')""");

        await MigrateTo(db);

        Assert.Equal(0, await Sql.CountAsync(db, "DailyDiary", "Id", orphan)); // without this the new foreign key could not be created
        var entry = (await db.DailyDiaries.AsNoTracking().ToListAsync()).Single(d => d.Id == kept);
        Assert.Equal("a real entry", entry.Highlight);
        Assert.Null(entry.Mood);
        Assert.Null(entry.Body);
        Assert.Empty(entry.Grateful);
        Assert.Empty(entry.Learned);
        Assert.Empty(entry.Tasks);
    }

    [Fact]
    public async Task SplitDisplayName_TakesTheFirstWordAsTheFirstName_AndTheRestAsTheLastName_AndRollbackJoinsThemAgain()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, AddHabitStartDate);
        var single = await Sql.InsertUserAsync(db);
        var two = await Sql.InsertUserAsync(db);
        var many = await Sql.InsertUserAsync(db);
        var tab = await Sql.InsertUserAsync(db);
        var nameless = await Sql.InsertUserAsync(db);
        await SetDisplayName(db, single, "Hanna");
        await SetDisplayName(db, two, "Hanna Müller");
        await SetDisplayName(db, many, "Anna Maria  von Schmidt");
        await SetDisplayName(db, tab, "Jörg\tMüller");

        await MigrateTo(db);

        var names = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => (u.FirstName, u.LastName));
        Assert.Equal(("Hanna", null), names[single]);                   // one word: only a first name
        Assert.Equal(("Hanna", "Müller"), names[two]);
        Assert.Equal(("Anna", "Maria  von Schmidt"), names[many]);      // the rest, as typed
        Assert.Equal(("Jörg", "Müller"), names[tab]);                   // any white space separates, not only a blank
        Assert.Equal((null, null), names[nameless]);                    // no name stays no name
        Assert.False(await ColumnExistsAsync(db, "AspNetUsers", "DisplayName"));

        await MigrateTo(db, AddHabitStartDate);

        Assert.False(await ColumnExistsAsync(db, "AspNetUsers", "FirstName"));
        Assert.Equal("Hanna", await DisplayNameOf(db, single));
        Assert.Equal("Hanna Müller", await DisplayNameOf(db, two));
        Assert.Equal("Anna Maria  von Schmidt", await DisplayNameOf(db, many));
        Assert.Equal("Jörg Müller", await DisplayNameOf(db, tab));
        Assert.Equal("<none>", await DisplayNameOf(db, nameless));
    }

    private static Task SetDisplayName(ApplicationDbContext db, Guid user, string name)
        => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "AspNetUsers" SET "DisplayName" = {name} WHERE "Id" = {user}""");

    private static async Task<string> DisplayNameOf(ApplicationDbContext db, Guid user)
        => (await db.Database.SqlQuery<string>($"""SELECT COALESCE("DisplayName", '<none>') AS "Value" FROM "AspNetUsers" WHERE "Id" = {user}""").ToListAsync()).Single();

    [Fact]
    public async Task HashRefreshTokens_EndsEveryPlainTextSession_TheyCannotBeConvertedToHashes()
    {
        await using var db = await BlankDatabaseAsync();
        await MigrateTo(db, AddHabitSchedule);
        var user = await Sql.InsertUserAsync(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RefreshTokens" ("Id", "CreatedAt", "ExpiresAt", "IsRevoked", "Token", "UserId")
            VALUES ({Guid.NewGuid()}, {DateTime.UtcNow}, {DateTime.UtcNow.AddDays(30)}, false, 'plain-text-token', {user})
            """);

        await MigrateTo(db);

        Assert.Equal(0, await db.RefreshTokens.CountAsync());
        Assert.True(await ColumnExistsAsync(db, "RefreshTokens", "TokenHash"));
        Assert.False(await ColumnExistsAsync(db, "RefreshTokens", "Token")); // the plain-text column is gone for good
    }
}
