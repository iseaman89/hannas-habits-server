using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

/// <summary>
/// <c>GET /api/habits/statistics</c> end to end: what was planned and done in a range, as of a day, per habit and
/// per part, the day it was last done on, the data isolation between two users and the 400s.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class HabitStatisticsApiTests
{
    // October 2026: Thu 1, Mon 5, Wed 7, Fri 9, Mon 12, Wed 14, Fri 16 ... September 2026 began on a Tuesday.
    private const int MondayDay = 1, WednesdayDay = 3, FridayDay = 5;

    private readonly ApiFactory _api;

    public HabitStatisticsApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    private static async Task<JsonElement> StatisticsAsync(TestUser user, string from, string to, string? asOf = null)
    {
        var response = await user.Client.GetAsync(
            $"/api/habits/statistics?from={from}&to={to}" + (asOf is null ? "" : $"&asOf={asOf}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.ReadJsonAsync();
    }

    private static JsonElement ItemOf(JsonElement statistics, Guid habitId)
        => statistics.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == habitId);

    /// <summary>Sport, due on Mondays, Wednesdays and Fridays since 1 September.</summary>
    private static Task<Guid> NewSport(TestUser user)
        => user.CreateHabitAsync("Sport", weekdays: [MondayDay, WednesdayDay, FridayDay], startDate: "2026-09-01");

    [Fact]
    public async Task CountsThePlannedAndDoneDays_AsOfTheGivenDay_AndSaysWhenItWasLastDone()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        await user.MarkAsync(habit, "2026-10-05", "2026-10-07", "2026-10-12");

        // Due in October up to Friday 16 (before it): Fri 2, Mon 5, Wed 7, Fri 9, Mon 12, Wed 14.
        var item = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-16"), habit);

        Assert.Equal("Sport", item.GetProperty("title").GetString());
        Assert.Equal("days", item.GetProperty("unit").GetString());
        Assert.Equal(6, item.GetProperty("planned").GetInt32());
        Assert.Equal(3, item.GetProperty("done").GetInt32());
        Assert.Equal("2026-10-12", item.GetProperty("lastDoneOn").GetString());
        Assert.Equal("2026-09-01", item.GetProperty("startDate").GetString());
        Assert.Equal("weekly", item.GetProperty("recurrence").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task AnOpenDayOfToday_IsNotAMiss_ButADoneOneIsCounted()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var open = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-05"), habit);
        await user.MarkAsync(habit, "2026-10-05");
        var done = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-05"), habit);

        Assert.Equal((1, 0), (open.GetProperty("planned").GetInt32(), open.GetProperty("done").GetInt32())); // Fri 2
        Assert.Equal((2, 1), (done.GetProperty("planned").GetInt32(), done.GetProperty("done").GetInt32()));
    }

    [Fact]
    public async Task LastDoneOn_ReachesOutsideTheRange_AndIgnoresTheFuture()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        await user.MarkAsync(habit, "2026-09-02", "2026-10-05", "2026-10-21");

        // September's statistic, as of 14 October: the last time was in October; the 21st is "tomorrow's" news.
        var item = ItemOf(await StatisticsAsync(user, "2026-09-01", "2026-09-30", asOf: "2026-10-14"), habit);

        Assert.Equal("2026-10-05", item.GetProperty("lastDoneOn").GetString());
        Assert.Equal(13, item.GetProperty("planned").GetInt32()); // every Mon/Wed/Fri of September
        Assert.Equal(1, item.GetProperty("done").GetInt32());
    }

    [Fact]
    public async Task AHabitNeverDone_HasNoLastDay()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var item = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-31"), habit);

        Assert.Equal(JsonValueKind.Null, item.GetProperty("lastDoneOn").ValueKind);
        Assert.Equal(0, item.GetProperty("done").GetInt32());
        Assert.Equal(0, item.GetProperty("currentStreak").GetInt32());
        Assert.Equal(0, item.GetProperty("longestStreak").GetInt32());
    }

    [Fact]
    public async Task TheStreaks_AreAsOfTheGivenDay_AndDoNotDependOnTheRange()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        // Due Mon / Wed / Fri: Mon 5, Wed 7, Fri 9 done (a run of three), Mon 12 missed, Wed 14 done.
        await user.MarkAsync(habit, "2026-10-05", "2026-10-07", "2026-10-09", "2026-10-14");

        var october = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-14"), habit);
        var september = ItemOf(await StatisticsAsync(user, "2026-09-01", "2026-09-30", asOf: "2026-10-14"), habit);

        foreach (var item in new[] { october, september })
        {
            Assert.Equal(1, item.GetProperty("currentStreak").GetInt32());
            Assert.Equal(3, item.GetProperty("longestStreak").GetInt32());
        }
    }

    [Fact]
    public async Task TheStreaks_OfTheOverview_AndTheStatistics_AreTheSameNumber()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        await user.MarkAsync(habit, "2026-10-05", "2026-10-07", "2026-10-09", "2026-10-12");

        var statistics = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-13"), habit);
        var overview = await user.Client.GetAsync("/api/habits/overview?from=2026-10-01&to=2026-10-31&asOf=2026-10-13");
        var overviewItem = ItemOf(await overview.ReadJsonAsync(), habit);

        Assert.Equal(4, statistics.GetProperty("currentStreak").GetInt32());
        Assert.Equal(
            overviewItem.GetProperty("currentStreak").GetInt32(), statistics.GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task AQuotaHabit_IsCountedInWeeks()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Run", startDate: "2026-09-01",
            recurrence: new { kind = "timesPerWeek", times = 2 });
        await user.MarkAsync(habit, "2026-10-06", "2026-10-08", "2026-10-13");

        // Weeks ending in October by Thursday 15: 28 Sep-4 Oct (nothing), 5-11 (two, met); 12-18 is running with one.
        var item = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-15"), habit);

        Assert.Equal("weeks", item.GetProperty("unit").GetString());
        Assert.Equal(2, item.GetProperty("planned").GetInt32());
        Assert.Equal(1, item.GetProperty("done").GetInt32());
        Assert.Equal("2026-10-13", item.GetProperty("lastDoneOn").GetString());
    }

    [Fact]
    public async Task AQuotaHabit_HasItsStreaksInWeeks()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Run", startDate: "2026-09-01",
            recurrence: new { kind = "timesPerWeek", times = 2 });
        // Three met weeks in September, a week without, then the weeks of 28 Sep and 5 Oct; the running week has one.
        await user.MarkAsync(habit, "2026-09-01", "2026-09-02", "2026-09-08", "2026-09-09", "2026-09-15", "2026-09-16",
            "2026-09-29", "2026-10-01", "2026-10-06", "2026-10-08", "2026-10-13");

        var item = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-15"), habit);

        Assert.Equal("weeks", item.GetProperty("unit").GetString());
        Assert.Equal(2, item.GetProperty("currentStreak").GetInt32());
        Assert.Equal(3, item.GetProperty("longestStreak").GetInt32());
    }

    [Fact]
    public async Task APartHasItsOwnStreaks_OnItsOwnWeekdays()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        var arms = await user.AddSubHabitAsync(habit, "Arms", WednesdayDay);
        await user.TickSubHabitAsync(habit, "2026-10-05", legs);
        await user.TickSubHabitAsync(habit, "2026-10-12", legs);
        await user.TickSubHabitAsync(habit, "2026-10-07", arms);

        var statistics = await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-14");
        var parts = ItemOf(statistics, habit).GetProperty("subHabits").EnumerateArray().ToArray();

        // Legs on Mondays: 5 and 12 ticked, 28 September was not. Arms on Wednesdays: 7 ticked, the 14th is still open.
        Assert.Equal((2, 2), (parts[0].GetProperty("currentStreak").GetInt32(), parts[0].GetProperty("longestStreak").GetInt32()));
        Assert.Equal((1, 1), (parts[1].GetProperty("currentStreak").GetInt32(), parts[1].GetProperty("longestStreak").GetInt32()));
    }

    [Fact]
    public async Task APartIsCountedOnItsOwnWeekdays_AndHasItsOwnLastDay()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        var arms = await user.AddSubHabitAsync(habit, "Arms", WednesdayDay);
        await user.TickSubHabitAsync(habit, "2026-10-05", legs);
        await user.TickSubHabitAsync(habit, "2026-10-07", arms);

        var statistics = await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-14");
        var parts = ItemOf(statistics, habit).GetProperty("subHabits").EnumerateArray().ToArray();

        // Legs on Mondays: 5 (ticked), 12 (not). Arms on Wednesdays: 7 (ticked); the 14th is today and still open.
        Assert.Equal(["Legs", "Arms"], parts.Select(p => p.GetProperty("title").GetString()));
        Assert.Equal([MondayDay], parts[0].GetProperty("weekdays").EnumerateArray().Select(d => d.GetInt32()));
        Assert.Equal((2, 1), (parts[0].GetProperty("planned").GetInt32(), parts[0].GetProperty("done").GetInt32()));
        Assert.Equal("2026-10-05", parts[0].GetProperty("lastDoneOn").GetString());
        Assert.Equal((1, 1), (parts[1].GetProperty("planned").GetInt32(), parts[1].GetProperty("done").GetInt32()));
        Assert.Equal("2026-10-07", parts[1].GetProperty("lastDoneOn").GetString());
    }

    [Fact]
    public async Task AHabitWithoutParts_HasAnEmptyList()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var item = ItemOf(await StatisticsAsync(user, "2026-10-01", "2026-10-31", asOf: "2026-10-14"), habit);

        Assert.Empty(item.GetProperty("subHabits").EnumerateArray());
    }

    [Fact]
    public async Task WithoutAsOf_TheServersDateIsToday()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        // A range far in the past: everything of it is over whatever "today" is.
        var item = ItemOf(await StatisticsAsync(user, "2026-09-01", "2026-09-30"), habit);

        Assert.Equal(13, item.GetProperty("planned").GetInt32());
    }

    // ---- isolation ----

    [Fact]
    public async Task AnotherUsersHabits_AreNeverCounted()
    {
        var owner = await NewUser();
        var intruder = await NewUser();
        var habit = await NewSport(owner);
        await owner.MarkAsync(habit, "2026-10-05");
        await owner.AddSubHabitAsync(habit, "Legs", MondayDay);

        var ownerSees = await StatisticsAsync(owner, "2026-10-01", "2026-10-31", asOf: "2026-10-14");
        var intruderSees = await StatisticsAsync(intruder, "2026-10-01", "2026-10-31", asOf: "2026-10-14");

        Assert.Single(ownerSees.EnumerateArray());
        Assert.Empty(intruderSees.EnumerateArray());
    }

    // ---- validation ----

    [Theory]
    [InlineData("", "to=2026-10-31", "from")]
    [InlineData("from=2026-10-01", "", "to")]
    public async Task BothEndsOfTheRange_AreRequired(string from, string to, string field)
    {
        var user = await NewUser();
        var query = string.Join("&", new[] { from, to }.Where(part => part.Length > 0));

        var response = await user.Client.GetAsync($"/api/habits/statistics?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"no error for '{field}'");
    }

    [Fact]
    public async Task ARangeLongerThanALeapYear_IsRefused()
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync("/api/habits/statistics?from=2026-01-01&to=2027-06-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("to", out _));
    }

    [Fact]
    public async Task ARangeTurnedRound_IsRefused()
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync("/api/habits/statistics?from=2026-10-31&to=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
