using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HannasHabits.Integration.Tests.Persistence;

// What the database itself guarantees, whatever the application does: check constraints, foreign keys, unique indexes,
// cascades, column types. Rows are written with plain SQL on purpose - the Domain would refuse most of these first.
[Collection(IntegrationCollection.Name)]
public class SchemaTests : IAsyncLifetime
{
    private readonly TestEnvironment _environment;
    private ApplicationDbContext _db = null!;
    private Guid _user;

    public SchemaTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public async Task InitializeAsync()
    {
        _db = _environment.CreateContext();
        _user = await Sql.InsertUserAsync(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ---- check constraints ----

    [Theory]
    [InlineData(1, true)]
    [InlineData(127, true)]
    [InlineData(62, true)]
    [InlineData(0, false)]
    [InlineData(128, false)]
    [InlineData(-1, false)]
    public async Task Habits_AScheduleIsAtLeastOneAndAtMostAllSevenDays(int schedule, bool accepted)
    {
        var insert = () => Sql.InsertHabitAsync(_db, _user, schedule: schedule);

        if (accepted)
            await insert();
        else
            Assert.Equal(("23514", "CK_Habits_Schedule"), Describe(await Sql.RejectedAsync(insert)));
    }

    [Theory]
    [InlineData("2000-01-01", true)]
    [InlineData("2100-12-31", true)]
    [InlineData("1999-12-31", false)]
    [InlineData("2101-01-01", false)]
    [InlineData("0001-01-01", false)]
    public async Task Habits_TheStartDateIsPlausible(string startDate, bool accepted)
    {
        var insert = () => Sql.InsertHabitAsync(_db, _user, startDate: startDate);

        if (accepted)
            await insert();
        else
            Assert.Equal(("23514", "CK_Habits_StartDate"), Describe(await Sql.RejectedAsync(insert)));
    }

    [Theory]
    [InlineData("Mood", 1, true)]
    [InlineData("Mood", 5, true)]
    [InlineData("Mood", 0, false)]
    [InlineData("Mood", 6, false)]
    [InlineData("Body", 0, true)]
    [InlineData("Body", 100, true)]
    [InlineData("Body", -1, false)]
    [InlineData("Body", 101, false)]
    [InlineData("Mind", 0, true)]
    [InlineData("Mind", 100, true)]
    [InlineData("Mind", -1, false)]
    [InlineData("Mind", 101, false)]
    public async Task Diary_MoodBodyAndMindStayInTheirRange(string column, int value, bool accepted)
    {
        var date = new DateOnly(2026, 1, 1).AddDays(Random.Shared.Next(0, 3000)).ToString("yyyy-MM-dd");
        Func<Task> insert = column switch
        {
            "Mood" => () => Sql.InsertDiaryAsync(_db, _user, date, mood: value),
            "Body" => () => Sql.InsertDiaryAsync(_db, _user, date, mood: null, body: value),
            _ => () => Sql.InsertDiaryAsync(_db, _user, date, mood: null, mind: value)
        };

        if (accepted)
            await insert();
        else
            Assert.Equal(("23514", $"CK_DailyDiary_{column}"), Describe(await Sql.RejectedAsync(insert)));
    }

    [Fact]
    public async Task Diary_AllOptionalValuesMayBeNull()
    {
        await Sql.InsertDiaryAsync(_db, _user, "2026-10-08", mood: null, body: null, mind: null);
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2100, true)]
    [InlineData(1999, false)]
    [InlineData(2101, false)]
    public async Task Resolutions_TheYearIsPlausible(int year, bool accepted)
    {
        var insert = () => Sql.InsertResolutionAsync(_db, _user, year);

        if (accepted)
            await insert();
        else
            Assert.Equal(("23514", "CK_Resolutions_Year"), Describe(await Sql.RejectedAsync(insert)));
    }

    // ---- text lengths ----

    [Fact]
    public async Task TextColumns_RefuseWhatIsLongerThanTheDomainAllows()
    {
        Assert.Equal(SqlState.StringTooLong, (await Sql.RejectedAsync(() => Sql.InsertHabitAsync(_db, _user, title: new string('a', 151)))).SqlState);
        Assert.Equal(SqlState.StringTooLong, (await Sql.RejectedAsync(() => Sql.InsertResolutionAsync(_db, _user, title: new string('a', 201)))).SqlState);
        Assert.Equal(SqlState.StringTooLong, (await Sql.RejectedAsync(() => _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DailyDiary" ("Id", "CreatedAt", "UserId", "Date", "Highlight", "Grateful", "Learned", "Tasks")
            VALUES ({Guid.NewGuid()}, {DateTime.UtcNow}, {_user}, {new DateOnly(2026, 10, 8)}, {new string('h', 5001)}, ARRAY[]::text[], ARRAY[]::text[], '[]'::jsonb)
            """))).SqlState);

        // ...and exactly the limit is fine.
        await Sql.InsertHabitAsync(_db, _user, title: new string('a', 150));
        await Sql.InsertResolutionAsync(_db, _user, title: new string('a', 200));
    }

    // ---- foreign keys ----

    [Fact]
    public async Task EveryTableRefusesARowWithoutItsOwner()
    {
        var nobody = Guid.NewGuid();

        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertHabitAsync(_db, nobody))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertDiaryAsync(_db, nobody, "2026-10-08"))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertResolutionAsync(_db, nobody))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertRefreshTokenAsync(_db, nobody))).SqlState);
    }

    [Fact]
    public async Task ARecord_NeedsItsHabit_AndAResolutionNeedsAnExistingHabitIfItNamesOne()
    {
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertRecordAsync(_db, Guid.NewGuid(), "2026-10-08"))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertResolutionAsync(_db, _user, habitId: Guid.NewGuid()))).SqlState);
    }

    // ---- unique indexes ----

    [Fact]
    public async Task ADayCanBeCompletedOnlyOncePerHabit()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        await Sql.InsertRecordAsync(_db, habit, "2026-10-08");

        var rejected = await Sql.RejectedAsync(() => Sql.InsertRecordAsync(_db, habit, "2026-10-08"));

        Assert.Equal(SqlState.UniqueViolation, rejected.SqlState);
        await Sql.InsertRecordAsync(_db, habit, "2026-10-09"); // another day is fine
        await Sql.InsertRecordAsync(_db, await Sql.InsertHabitAsync(_db, _user), "2026-10-08"); // another habit too
    }

    [Fact]
    public async Task AUserHasOneDiaryEntryPerDay()
    {
        await Sql.InsertDiaryAsync(_db, _user, "2026-10-08");

        Assert.Equal(SqlState.UniqueViolation, (await Sql.RejectedAsync(() => Sql.InsertDiaryAsync(_db, _user, "2026-10-08"))).SqlState);

        await Sql.InsertDiaryAsync(_db, _user, "2026-10-09");
        await Sql.InsertDiaryAsync(_db, await Sql.InsertUserAsync(_db), "2026-10-08"); // another user may write the same day
    }

    [Fact]
    public async Task ARefreshTokenHashExistsOnlyOnce()
    {
        var hash = new string('a', 64);
        await Sql.InsertRefreshTokenAsync(_db, _user, hash);

        Assert.Equal(SqlState.UniqueViolation, (await Sql.RejectedAsync(() => Sql.InsertRefreshTokenAsync(_db, _user, hash))).SqlState);
    }

    [Fact]
    public async Task ResolutionsMayRepeatTheSameTitle()
    {
        await Sql.InsertResolutionAsync(_db, _user, title: "Read");
        await Sql.InsertResolutionAsync(_db, _user, title: "Read");
    }

    // ---- cascades ----

    [Fact]
    public async Task DeletingAUser_RemovesEverythingTheyOwn_AndNothingOfAnybodyElse()
    {
        var other = await Sql.InsertUserAsync(_db);
        foreach (var owner in new[] { _user, other })
        {
            var habit = await Sql.InsertHabitAsync(_db, owner);
            await Sql.InsertRecordAsync(_db, habit, "2026-10-08");
            await Sql.InsertDiaryAsync(_db, owner, "2026-10-08");
            await Sql.InsertResolutionAsync(_db, owner, habitId: habit);
            await Sql.InsertRefreshTokenAsync(_db, owner);
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "AspNetUsers" WHERE "Id" = {_user}""");

        foreach (var (table, column) in new[] { ("Habits", "UserId"), ("DailyDiary", "UserId"), ("Resolutions", "UserId"), ("RefreshTokens", "UserId") })
        {
            Assert.Equal(0, await Sql.CountAsync(_db, table, column, _user));
            Assert.Equal(1, await Sql.CountAsync(_db, table, column, other));
        }

        var recordsOfOther = "SELECT COUNT(*) FROM \"HabitRecords\" WHERE \"HabitId\" IN (SELECT \"Id\" FROM \"Habits\" WHERE \"UserId\" = @p0)";
        Assert.Equal(1, await Sql.ScalarAsync(_db, recordsOfOther, other));
    }

    [Fact]
    public async Task DeletingAHabit_RemovesItsRecords_ButOnlyClearsTheLinkOfItsResolutions()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        await Sql.InsertRecordAsync(_db, habit, "2026-10-08");
        await Sql.InsertRecordAsync(_db, habit, "2026-10-09");
        var resolution = await Sql.InsertResolutionAsync(_db, _user, habitId: habit);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Habits" WHERE "Id" = {habit}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit));
        Assert.Equal(1, await Sql.ScalarAsync(_db, "SELECT COUNT(*) FROM \"Resolutions\" WHERE \"Id\" = @p0 AND \"HabitId\" IS NULL", resolution));
    }

    // ---- column types ----

    [Fact]
    public async Task TheColumns_HaveTheTypesTheDomainRelies_On()
    {
        var columns = new Dictionary<string, (string Type, string Nullable, bool HasDefault)>();
        await _db.Database.OpenConnectionAsync();
        await using var command = _db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT table_name || '.' || column_name, data_type, is_nullable, column_default IS NOT NULL
            FROM information_schema.columns WHERE table_schema = 'public'
            """;
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                columns[reader.GetString(0)] = (reader.GetString(1), reader.GetString(2), reader.GetBoolean(3));

        (string, string) TypeOf(string column) => (columns[column].Type, columns[column].Nullable);

        Assert.Equal(("date", "NO"), TypeOf("Habits.StartDate"));
        Assert.Equal(("integer", "NO"), TypeOf("Habits.Schedule"));
        Assert.Equal(("character varying", "NO"), TypeOf("Habits.Title"));
        Assert.Equal(("character varying", "YES"), TypeOf("Habits.Description"));
        Assert.Equal(("date", "NO"), TypeOf("HabitRecords.Date"));
        Assert.Equal(("date", "NO"), TypeOf("DailyDiary.Date"));
        Assert.Equal(("integer", "YES"), TypeOf("DailyDiary.Mood"));
        Assert.Equal(("integer", "YES"), TypeOf("DailyDiary.Body"));
        Assert.Equal(("integer", "YES"), TypeOf("DailyDiary.Mind"));
        Assert.Equal(("ARRAY", "NO"), TypeOf("DailyDiary.Grateful")); // a native text[]
        Assert.Equal(("ARRAY", "NO"), TypeOf("DailyDiary.Learned"));
        Assert.Equal(("jsonb", "NO"), TypeOf("DailyDiary.Tasks"));
        Assert.Equal(("character varying", "YES"), TypeOf("DailyDiary.Highlight"));
        Assert.Equal(("integer", "NO"), TypeOf("Resolutions.Year"));
        Assert.Equal(("uuid", "YES"), TypeOf("Resolutions.HabitId"));
        Assert.Equal(("character varying", "NO"), TypeOf("RefreshTokens.TokenHash"));
        Assert.Equal(("character varying", "YES"), TypeOf("AspNetUsers.DisplayName"));

        // No default for the start date: one that the check constraint rejects (0001-01-01) would be a trap for any insert
        // that forgets the column.
        Assert.False(columns["Habits.StartDate"].HasDefault);
    }

    private static (string SqlState, string? Constraint) Describe(PostgresException exception)
        => (exception.SqlState, exception.ConstraintName);
}
