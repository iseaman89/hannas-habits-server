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
            Assert.Equal(("23514", "CK_Habits_ScheduleWeekdays"), Describe(await Sql.RejectedAsync(insert)));
    }

    // The kinds of schedule (0 weekly, 1 every N days, 2 monthly on a weekday, 3 monthly on a date; the quotas 4 and 5
    // follow below) and the facts each
    // one has: a row that says "every 3 days" without a day to count from means nothing and is refused.
    [Theory]
    [InlineData(0, 62, 1, null, null, null)]                    // weekly, every week (Monday to Friday)
    [InlineData(0, 65, 2, "2026-10-05", null, null)]            // every 2nd week, counted from the week of the anchor
    [InlineData(0, 1, 52, "2026-10-05", null, null)]            // the longest weekly interval
    [InlineData(1, 127, 3, "2026-10-10", null, null)]           // every 3 days
    [InlineData(1, 127, 2, "2000-01-01", null, null)]           // ... the shortest, the earliest anchor
    [InlineData(1, 127, 365, "2100-12-31", null, null)]         // ... the longest, the latest anchor
    [InlineData(2, 64, 1, null, 5, null)]                       // the last Saturday
    [InlineData(2, 2, 1, null, 1, null)]                        // the first Monday
    [InlineData(3, 127, 1, null, null, 15)]                     // the 15th
    [InlineData(3, 127, 1, null, null, 31)]                     // the 31st (the last day of a shorter month)
    public async Task Habits_EachKindOfScheduleIsAcceptedWithItsOwnFacts(
        int kind, int weekdays, int interval, string? anchor, int? occurrence, int? dayOfMonth)
    {
        await Sql.InsertHabitAsync(
            _db, _user, schedule: weekdays, kind: kind, interval: interval, anchor: anchor, occurrence: occurrence,
            dayOfMonth: dayOfMonth);
    }

    [Theory]
    [InlineData(6, 127, 1, null, null, null)]                   // no such kind
    [InlineData(-1, 127, 1, null, null, null)]
    [InlineData(0, 62, 0, null, null, null)]                    // weekly: interval 0
    [InlineData(0, 62, 53, "2026-10-05", null, null)]           // ... more than a year of weeks
    [InlineData(0, 62, 2, null, null, null)]                    // ... every 2nd week without a week to count from
    [InlineData(0, 62, 1, "2026-10-05", null, null)]            // ... every week with an anchor (it would mean nothing)
    [InlineData(0, 62, 1, null, 1, null)]                       // ... with an occurrence
    [InlineData(0, 62, 1, null, null, 15)]                      // ... with a day of the month
    [InlineData(1, 127, 1, "2026-10-10", null, null)]           // every N days: 1 is the daily schedule
    [InlineData(1, 127, 366, "2026-10-10", null, null)]         // ... more than a year
    [InlineData(1, 127, 3, null, null, null)]                   // ... without an anchor
    [InlineData(1, 62, 3, "2026-10-10", null, null)]            // ... that depends on a weekday
    [InlineData(2, 127, 1, null, 5, null)]                      // monthly on a weekday: more than one weekday
    [InlineData(2, 64, 1, null, 0, null)]                       // ... occurrence 0
    [InlineData(2, 64, 1, null, 6, null)]                       // ... occurrence 6 (there is no "fifth")
    [InlineData(2, 64, 1, null, null, null)]                    // ... without an occurrence
    [InlineData(2, 64, 2, null, 5, null)]                       // ... with an interval
    [InlineData(2, 64, 1, "2026-10-10", 5, null)]               // ... with an anchor
    [InlineData(3, 127, 1, null, null, 0)]                      // monthly on a date: day 0
    [InlineData(3, 127, 1, null, null, 32)]                     // ... day 32
    [InlineData(3, 127, 1, null, null, null)]                   // ... without a day
    [InlineData(3, 62, 1, null, null, 15)]                      // ... that depends on a weekday
    [InlineData(3, 127, 1, null, 1, 15)]                        // ... with an occurrence
    public async Task Habits_AScheduleWithTheWrongFactsForItsKindIsRefused(
        int kind, int weekdays, int interval, string? anchor, int? occurrence, int? dayOfMonth)
    {
        var insert = () => Sql.InsertHabitAsync(
            _db, _user, schedule: weekdays, kind: kind, interval: interval, anchor: anchor, occurrence: occurrence,
            dayOfMonth: dayOfMonth);

        Assert.Equal(("23514", "CK_Habits_ScheduleRule"), Describe(await Sql.RejectedAsync(insert)));
    }

    // The two quota kinds (4 times a week, 5 times a month) are all weekdays with a count and none of the other facts.
    [Theory]
    [InlineData(4, 1)]
    [InlineData(4, 7)]                                          // every day of the week
    [InlineData(5, 1)]
    [InlineData(5, 28)]                                         // what February can always reach
    public async Task Habits_AQuotaIsAcceptedWithItsCount(int kind, int times)
    {
        await Sql.InsertHabitAsync(_db, _user, schedule: 127, kind: kind, times: times);
    }

    [Theory]
    [InlineData(4, 127, 1, null, null, null, null)]             // times a week without a count
    [InlineData(4, 127, 1, null, null, null, 0)]                // ... 0
    [InlineData(4, 127, 1, null, null, null, 8)]                // ... more than seven in a week
    [InlineData(5, 127, 1, null, null, null, null)]             // times a month without a count
    [InlineData(5, 127, 1, null, null, null, 0)]
    [InlineData(5, 127, 1, null, null, null, 29)]               // ... more than a month can always hold
    [InlineData(4, 62, 1, null, null, null, 3)]                 // ... that depends on a weekday
    [InlineData(4, 127, 2, null, null, null, 3)]                // ... with an interval
    [InlineData(4, 127, 1, "2026-10-05", null, null, 3)]        // ... with an anchor
    [InlineData(5, 127, 1, null, 1, null, 3)]                   // ... with an occurrence
    [InlineData(5, 127, 1, null, null, 15, 3)]                  // ... with a day of the month
    [InlineData(0, 62, 1, null, null, null, 3)]                 // a count on a weekly rule
    [InlineData(1, 127, 3, "2026-10-10", null, null, 3)]        // ... on every N days
    [InlineData(2, 64, 1, null, 5, null, 3)]                    // ... on monthly on a weekday
    [InlineData(3, 127, 1, null, null, 15, 3)]                  // ... on monthly on a date
    public async Task Habits_ACountBelongsToTheQuotaKindsOnly_AndStaysInTheirLimits(
        int kind, int weekdays, int interval, string? anchor, int? occurrence, int? dayOfMonth, int? times)
    {
        var insert = () => Sql.InsertHabitAsync(
            _db, _user, schedule: weekdays, kind: kind, interval: interval, anchor: anchor, occurrence: occurrence,
            dayOfMonth: dayOfMonth, times: times);

        Assert.Equal(("23514", "CK_Habits_ScheduleRule"), Describe(await Sql.RejectedAsync(insert)));
    }

    [Theory]
    [InlineData("2000-01-01", true)]
    [InlineData("2100-12-31", true)]
    [InlineData("1999-12-31", false)]
    [InlineData("2101-01-01", false)]
    public async Task Habits_AnAnchorIsPlausible(string anchor, bool accepted)
    {
        var insert = () => Sql.InsertHabitAsync(_db, _user, schedule: 127, kind: 1, interval: 3, anchor: anchor);

        if (accepted)
            await insert();
        else
            Assert.Equal(("23514", "CK_Habits_ScheduleAnchor"), Describe(await Sql.RejectedAsync(insert)));
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
    public async Task ATag_NeedsItsOwner_ADayEntryItsHabit_AndATaggedDayItsEntryAndItsTag()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var entry = await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08");
        var tag = await Sql.InsertTagAsync(_db, _user);

        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertTagAsync(_db, Guid.NewGuid()))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertDayEntryAsync(_db, Guid.NewGuid(), "2026-10-08"))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertDayTagAsync(_db, Guid.NewGuid(), tag))).SqlState);
        Assert.Equal(SqlState.ForeignKeyViolation, (await Sql.RejectedAsync(() => Sql.InsertDayTagAsync(_db, entry, Guid.NewGuid()))).SqlState);
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

    [Fact]
    public async Task AUserHasOneTagPerName_IgnoringCase_ButAnotherUserMayHaveTheSameName()
    {
        await Sql.InsertTagAsync(_db, _user, "krank");

        // The Domain stores the case-folded name as the key: "Krank" and "krank" collide on it.
        Assert.Equal(SqlState.UniqueViolation, (await Sql.RejectedAsync(() => Sql.InsertTagAsync(_db, _user, "Krank"))).SqlState);

        await Sql.InsertTagAsync(_db, _user, "Urlaub");
        await Sql.InsertTagAsync(_db, await Sql.InsertUserAsync(_db), "krank");
    }

    [Fact]
    public async Task ATagNameIsAtMost30Characters()
    {
        await Sql.InsertTagAsync(_db, _user, new string('a', 30));

        Assert.Equal(SqlState.StringTooLong, (await Sql.RejectedAsync(() => Sql.InsertTagAsync(_db, _user, new string('b', 31)))).SqlState);
    }

    [Fact]
    public async Task AHabitHasOneDayEntryPerDay()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08");

        Assert.Equal(SqlState.UniqueViolation, (await Sql.RejectedAsync(() => Sql.InsertDayEntryAsync(_db, habit, "2026-10-08"))).SqlState);

        await Sql.InsertDayEntryAsync(_db, habit, "2026-10-09");
        await Sql.InsertDayEntryAsync(_db, await Sql.InsertHabitAsync(_db, _user), "2026-10-08");
    }

    [Theory]
    [InlineData("2000-01-01", true)]
    [InlineData("2100-12-31", true)]
    [InlineData("1999-12-31", false)]
    [InlineData("2101-01-01", false)]
    public async Task ADayEntryIsOnAPlausibleDay(string date, bool accepted)
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);

        if (accepted)
            await Sql.InsertDayEntryAsync(_db, habit, date);
        else
            Assert.Equal(SqlState.CheckViolation, (await Sql.RejectedAsync(() => Sql.InsertDayEntryAsync(_db, habit, date))).SqlState);
    }

    [Fact]
    public async Task ATagIsOnADayOnlyOnce_ButOnManyDays()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var entry = await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08");
        var tag = await Sql.InsertTagAsync(_db, _user);
        await Sql.InsertDayTagAsync(_db, entry, tag);

        Assert.Equal(SqlState.UniqueViolation, (await Sql.RejectedAsync(() => Sql.InsertDayTagAsync(_db, entry, tag))).SqlState);

        await Sql.InsertDayTagAsync(_db, await Sql.InsertDayEntryAsync(_db, habit, "2026-10-09"), tag);
        await Sql.InsertDayTagAsync(_db, entry, await Sql.InsertTagAsync(_db, _user, "Urlaub"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(127, true)]
    [InlineData(34, true)]
    [InlineData(0, false)]
    [InlineData(128, false)]
    [InlineData(-1, false)]
    public async Task SubHabits_AreOnAtLeastOneAndAtMostAllSevenWeekdays(int weekdays, bool accepted)
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);

        if (accepted)
            await Sql.InsertSubHabitAsync(_db, habit, weekdays: weekdays);
        else
            Assert.Equal(("23514", "CK_SubHabits_Weekdays"),
                Describe(await Sql.RejectedAsync(() => Sql.InsertSubHabitAsync(_db, habit, weekdays: weekdays))));
    }

    [Fact]
    public async Task ASubHabit_NeedsItsHabit()
    {
        Assert.Equal(SqlState.ForeignKeyViolation,
            (await Sql.RejectedAsync(() => Sql.InsertSubHabitAsync(_db, Guid.NewGuid()))).SqlState);
    }

    [Fact]
    public async Task ASubHabit_IsDoneOnADayOnlyOnce_ButOnManyDays_AndTwoPartsMayShareADay()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        var core = await Sql.InsertSubHabitAsync(_db, habit, "Core");
        await Sql.InsertSubHabitRecordAsync(_db, legs, "2026-10-05");

        Assert.Equal(SqlState.UniqueViolation,
            (await Sql.RejectedAsync(() => Sql.InsertSubHabitRecordAsync(_db, legs, "2026-10-05"))).SqlState);

        await Sql.InsertSubHabitRecordAsync(_db, legs, "2026-10-12");
        await Sql.InsertSubHabitRecordAsync(_db, core, "2026-10-05");
    }

    // ---- cascades ----

    [Fact]
    public async Task DeletingAHabit_RemovesItsSubHabitsAndTheirTicks_ButNotThoseOfAnotherHabit()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var other = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        var arms = await Sql.InsertSubHabitAsync(_db, other, "Arms");
        await Sql.InsertSubHabitRecordAsync(_db, legs, "2026-10-05");
        await Sql.InsertSubHabitRecordAsync(_db, arms, "2026-10-05");

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Habits" WHERE "Id" = {habit}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "SubHabits", "HabitId", habit));
        Assert.Equal(0, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", legs));
        Assert.Equal(1, await Sql.CountAsync(_db, "SubHabits", "HabitId", other));
        Assert.Equal(1, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", arms));
    }

    [Fact]
    public async Task ASubHabitNote_NeedsItsPart()
    {
        Assert.Equal("23503",
            (await Sql.RejectedAsync(() => Sql.InsertSubHabitNoteAsync(_db, Guid.NewGuid(), "2026-10-05"))).SqlState);
    }

    [Fact]
    public async Task ASubHabitHasOneNoteADay_ButOnManyDays_AndTwoPartsMayShareADay()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        var core = await Sql.InsertSubHabitAsync(_db, habit, "Core");
        await Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05");

        Assert.Equal("23505",
            (await Sql.RejectedAsync(() => Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05"))).SqlState);

        await Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-12");
        await Sql.InsertSubHabitNoteAsync(_db, core, "2026-10-05");
    }

    [Theory]
    [InlineData("1999-12-31", false)]
    [InlineData("2000-01-01", true)]
    [InlineData("2100-12-31", true)]
    [InlineData("2101-01-01", false)]
    public async Task ASubHabitNote_IsOnADayInThePlausibleRange(string date, bool accepted)
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");

        if (accepted)
            await Sql.InsertSubHabitNoteAsync(_db, legs, date);
        else
            Assert.Equal(("23514", "CK_SubHabitNotes_Date"),
                Describe(await Sql.RejectedAsync(() => Sql.InsertSubHabitNoteAsync(_db, legs, date))));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("x", true)]
    public async Task ASubHabitNote_SaysSomething(string text, bool accepted)
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");

        if (accepted)
            await Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05", text);
        else
            Assert.Equal(("23514", "CK_SubHabitNotes_Text"),
                Describe(await Sql.RejectedAsync(() => Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05", text))));
    }

    [Fact]
    public async Task ASubHabitNote_IsNotLongerThanTheDomainAllows()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");

        await Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05", new string('a', 500));

        // The column is varchar(500): one more is refused (22001) before the check constraint is even looked at.
        Assert.Equal("22001",
            (await Sql.RejectedAsync(() => Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-06", new string('a', 501)))).SqlState);
    }

    [Fact]
    public async Task DeletingASubHabit_RemovesItsNotes_ButNotThoseOfAnotherPart()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        var core = await Sql.InsertSubHabitAsync(_db, habit, "Core");
        await Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05");
        await Sql.InsertSubHabitNoteAsync(_db, core, "2026-10-05");

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "SubHabits" WHERE "Id" = {legs}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "SubHabitNotes", "SubHabitId", legs));
        Assert.Equal(1, await Sql.CountAsync(_db, "SubHabitNotes", "SubHabitId", core));
    }

    [Fact]
    public async Task DeletingAHabit_RemovesTheNotesOfItsParts()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        await Sql.InsertSubHabitNoteAsync(_db, legs, "2026-10-05");

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Habits" WHERE "Id" = {habit}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "SubHabitNotes", "SubHabitId", legs));
    }

    [Fact]
    public async Task DeletingASubHabit_RemovesItsTicks_ButNotTheHabitsOwnDays()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var legs = await Sql.InsertSubHabitAsync(_db, habit, "Legs");
        var core = await Sql.InsertSubHabitAsync(_db, habit, "Core");
        await Sql.InsertRecordAsync(_db, habit, "2026-10-05");
        await Sql.InsertSubHabitRecordAsync(_db, legs, "2026-10-05");
        await Sql.InsertSubHabitRecordAsync(_db, core, "2026-10-05");

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "SubHabits" WHERE "Id" = {legs}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", legs));
        Assert.Equal(1, await Sql.CountAsync(_db, "SubHabitRecords", "SubHabitId", core));
        Assert.Equal(1, await Sql.CountAsync(_db, "HabitRecords", "HabitId", habit));
    }

    [Fact]
    public async Task DeletingAUser_RemovesTheirSubHabitsAndTicks_AndNothingOfAnybodyElse()
    {
        var other = await Sql.InsertUserAsync(_db);
        foreach (var owner in new[] { _user, other })
        {
            var habit = await Sql.InsertHabitAsync(_db, owner);
            var part = await Sql.InsertSubHabitAsync(_db, habit);
            await Sql.InsertSubHabitRecordAsync(_db, part, "2026-10-05");
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "AspNetUsers" WHERE "Id" = {_user}""");

        const string PartsOf = "SELECT COUNT(*) FROM \"SubHabits\" s JOIN \"Habits\" h ON h.\"Id\" = s.\"HabitId\" WHERE h.\"UserId\" = @p0";
        const string TicksOf = "SELECT COUNT(*) FROM \"SubHabitRecords\" r JOIN \"SubHabits\" s ON s.\"Id\" = r.\"SubHabitId\" JOIN \"Habits\" h ON h.\"Id\" = s.\"HabitId\" WHERE h.\"UserId\" = @p0";
        Assert.Equal(0, await Sql.ScalarAsync(_db, PartsOf, _user));
        Assert.Equal(0, await Sql.ScalarAsync(_db, TicksOf, _user));
        Assert.Equal(1, await Sql.ScalarAsync(_db, PartsOf, other));
        Assert.Equal(1, await Sql.ScalarAsync(_db, TicksOf, other));
    }

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
    public async Task DeletingAUser_RemovesTheirTagsAndTaggedDays_AndNothingOfAnybodyElse()
    {
        var other = await Sql.InsertUserAsync(_db);
        foreach (var owner in new[] { _user, other })
        {
            var habit = await Sql.InsertHabitAsync(_db, owner);
            var entry = await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08");
            await Sql.InsertDayTagAsync(_db, entry, await Sql.InsertTagAsync(_db, owner));
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "AspNetUsers" WHERE "Id" = {_user}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "Tags", "UserId", _user));
        Assert.Equal(1, await Sql.CountAsync(_db, "Tags", "UserId", other));
        Assert.Equal(1, await Sql.ScalarAsync(_db, """
            SELECT COUNT(*) FROM "HabitDayTags" t JOIN "Tags" g ON g."Id" = t."TagId" WHERE g."UserId" = @p0
            """, other));
        Assert.Equal(0, await Sql.ScalarAsync(_db, """
            SELECT COUNT(*) FROM "HabitDayEntries" e JOIN "Habits" h ON h."Id" = e."HabitId" WHERE h."UserId" = @p0
            """, _user));
    }

    [Fact]
    public async Task DeletingATag_TakesItOffItsDays_ButKeepsTheEntriesAndTheOtherTags()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var entry = await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08");
        var sick = await Sql.InsertTagAsync(_db, _user, "krank");
        var holiday = await Sql.InsertTagAsync(_db, _user, "Urlaub");
        await Sql.InsertDayTagAsync(_db, entry, sick);
        await Sql.InsertDayTagAsync(_db, entry, holiday);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Tags" WHERE "Id" = {sick}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "HabitDayTags", "TagId", sick));
        Assert.Equal(1, await Sql.CountAsync(_db, "HabitDayTags", "TagId", holiday));
        Assert.Equal(1, await Sql.CountAsync(_db, "HabitDayEntries", "Id", entry));
    }

    [Fact]
    public async Task DeletingAHabit_RemovesItsTaggedDays_ButNotTheTags()
    {
        var habit = await Sql.InsertHabitAsync(_db, _user);
        var other = await Sql.InsertHabitAsync(_db, _user);
        var tag = await Sql.InsertTagAsync(_db, _user);
        await Sql.InsertDayTagAsync(_db, await Sql.InsertDayEntryAsync(_db, habit, "2026-10-08"), tag);
        await Sql.InsertDayTagAsync(_db, await Sql.InsertDayEntryAsync(_db, other, "2026-10-08"), tag);

        await _db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Habits" WHERE "Id" = {habit}""");

        Assert.Equal(0, await Sql.CountAsync(_db, "HabitDayEntries", "HabitId", habit));
        Assert.Equal(1, await Sql.CountAsync(_db, "HabitDayTags", "TagId", tag)); // only the other habit's day is left
        Assert.Equal(1, await Sql.CountAsync(_db, "Tags", "Id", tag));
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
        Assert.Equal(("integer", "NO"), TypeOf("Habits.ScheduleKind"));
        Assert.Equal(("integer", "NO"), TypeOf("Habits.ScheduleWeekdays"));
        Assert.Equal(("integer", "NO"), TypeOf("Habits.ScheduleInterval"));
        Assert.Equal(("date", "YES"), TypeOf("Habits.ScheduleAnchor"));
        Assert.Equal(("integer", "YES"), TypeOf("Habits.ScheduleOccurrence"));
        Assert.Equal(("integer", "YES"), TypeOf("Habits.ScheduleDayOfMonth"));
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
        Assert.Equal(("character varying", "YES"), TypeOf("AspNetUsers.FirstName"));
        Assert.Equal(("character varying", "YES"), TypeOf("AspNetUsers.LastName"));
        Assert.Equal(("character varying", "NO"), TypeOf("Tags.Name"));
        Assert.Equal(("character varying", "NO"), TypeOf("Tags.NameKey"));
        Assert.Equal(("date", "NO"), TypeOf("HabitDayEntries.Date"));
        Assert.Equal(("uuid", "NO"), TypeOf("HabitDayTags.TagId"));
        Assert.Equal(("character varying", "NO"), TypeOf("SubHabits.Title"));
        Assert.Equal(("integer", "NO"), TypeOf("SubHabits.Weekdays"));
        Assert.Equal(("date", "NO"), TypeOf("SubHabitRecords.Date"));

        // No default for the start date: one that the check constraint rejects (0001-01-01) would be a trap for any insert
        // that forgets the column.
        Assert.False(columns["Habits.StartDate"].HasDefault);

        // The migration filled the columns of the rows that existed and dropped its defaults again: the application
        // writes the whole schedule, and the model (which has no defaults) is what the next migration is compared with.
        Assert.False(columns["Habits.ScheduleKind"].HasDefault);
        Assert.False(columns["Habits.ScheduleInterval"].HasDefault);
    }

    private static (string SqlState, string? Constraint) Describe(PostgresException exception)
        => (exception.SqlState, exception.ConstraintName);
}
