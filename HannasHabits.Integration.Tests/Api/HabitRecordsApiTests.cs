using System.Net;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class HabitRecordsApiTests
{
    private readonly ApiFactory _api;

    public HabitRecordsApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    [Fact]
    public async Task Mark_AnswersWithTheRecord()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        var response = await user.Client.PutAsync($"/api/habits/{habit}/records/2026-10-08");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal(habit, body.GetProperty("habitId").GetGuid());
        Assert.Equal("2026-10-08", body.GetProperty("date").GetString());
    }

    [Fact]
    public async Task Mark_IsIdempotent_AMarkedDayStaysOneRecord()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        var first = await (await user.Client.PutAsync($"/api/habits/{habit}/records/2026-10-08")).ReadJsonAsync();
        var second = await user.Client.PutAsync($"/api/habits/{habit}/records/2026-10-08");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(first.GetProperty("id").GetGuid(), (await second.ReadJsonAsync()).GetProperty("id").GetGuid());
        Assert.Equal(["2026-10-08"], await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Mark_TenParallelRequestsForTheSameDay_AllSucceedWithTheSameRecord()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => user.Client.PutAsync($"/api/habits/{habit}/records/2026-10-08")));

        Assert.True(responses.All(r => r.StatusCode == HttpStatusCode.OK), string.Join(Environment.NewLine, _api.Logs.Errors.Where(e => e.Contains("GlobalExceptionHandler")).Take(2)));
        var ids = await Task.WhenAll(responses.Select(async r => (await r.ReadJsonAsync()).GetProperty("id").GetGuid()));
        Assert.Single(ids.Distinct());
        Assert.Equal(["2026-10-08"], await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Mark_AllowsFutureAndUnscheduledDays_TheServerStaysPermissive()
    {
        var user = await NewUser();
        var mondaysOnly = await user.CreateHabitAsync("Gym", [1]);

        Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsync($"/api/habits/{mondaysOnly}/records/2026-10-08")).StatusCode); // a Thursday
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsync($"/api/habits/{mondaysOnly}/records/2099-01-01")).StatusCode); // the future
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("tomorrow")]
    public async Task Mark_ABrokenDate_IsA400_NotA500(string date)
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.PutAsync($"/api/habits/{habit}/records/{date}")).StatusCode);
        Assert.Empty(await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Mark_AnUnknownHabit_IsA404()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PutAsync($"/api/habits/{Guid.NewGuid()}/records/2026-10-08")).StatusCode);
    }

    // ---- list ----

    [Fact]
    public async Task List_ReturnsTheRecordsOldestFirst()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        await user.MarkAsync(habit, "2026-10-09", "2026-10-07", "2026-10-08");

        Assert.Equal(["2026-10-07", "2026-10-08", "2026-10-09"], await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task List_TheRangeIsInclusive_AndEachEndIsOptional()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        await user.MarkAsync(habit, ApiExtensions.Days(2026, 10, 5, 6, 7, 8, 9));

        Assert.Equal(["2026-10-06", "2026-10-07", "2026-10-08"], await user.RecordDatesAsync(habit, "?from=2026-10-06&to=2026-10-08"));
        Assert.Equal(["2026-10-08", "2026-10-09"], await user.RecordDatesAsync(habit, "?from=2026-10-08"));
        Assert.Equal(["2026-10-05", "2026-10-06"], await user.RecordDatesAsync(habit, "?to=2026-10-06"));
        Assert.Equal(["2026-10-07"], await user.RecordDatesAsync(habit, "?from=2026-10-07&to=2026-10-07"));
        Assert.Empty(await user.RecordDatesAsync(habit, "?from=2027-01-01"));
    }

    [Fact]
    public async Task List_ABackwardsRange_IsA400OnTo()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        var response = await user.Client.GetAsync($"/api/habits/{habit}/records?from=2026-10-08&to=2026-10-07");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("to", await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task List_AHabitWithoutRecords_IsAnEmptyList_AnUnknownHabitIsA404()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        Assert.Empty(await user.RecordDatesAsync(habit));
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/habits/{Guid.NewGuid()}/records")).StatusCode);
    }

    // ---- unmark ----

    [Fact]
    public async Task Unmark_TakesTheDayBack_OnlyThatDay()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        await user.MarkAsync(habit, "2026-10-07", "2026-10-08");

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-08")).StatusCode);

        Assert.Equal(["2026-10-07"], await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Unmark_ADayThatIsNotMarked_IsA404()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-08")).StatusCode);

        await user.MarkAsync(habit, "2026-10-08");
        await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-08");
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-08")).StatusCode);
    }

    [Fact]
    public async Task Unmark_ADayCanBeMarkedAgainAfterwards()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        await user.MarkAsync(habit, "2026-10-08");
        await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-08");

        await user.MarkAsync(habit, "2026-10-08");

        Assert.Equal(["2026-10-08"], await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Unmark_EightParallelRequests_OneSucceeds_TheOthersAreToldNoOrRetry()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        await user.MarkAsync(habit, "2026-10-08");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-08")));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        // 404: loaded after the winner committed. 409: loaded before, deleted nothing - "changed by another request".
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.NoContent),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict }));
        Assert.Empty(await user.RecordDatesAsync(habit));
    }
}

[Collection(IntegrationCollection.Name)]
public class HabitOverviewApiTests
{
    private readonly ApiFactory _api;

    public HabitOverviewApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    [Fact]
    public async Task TheRouteIsNotSwallowedByTheHabitIdRoute()
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync("/api/habits/overview?from=2026-10-01&to=2026-10-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Theory]
    [InlineData("?to=2026-10-31", "from")]
    [InlineData("?from=2026-10-01", "to")]
    [InlineData("?from=2026-10-31&to=2026-10-01", "to")]
    public async Task FromAndTo_AreRequired_AndMustNotRunBackwards(string query, string errorKey)
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync("/api/habits/overview" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errorKey, await response.ErrorKeysAsync());
    }

    [Theory]
    [InlineData("?from=garbage&to=2026-10-31")]
    [InlineData("?from=2026-10-01&to=2026-10-31&asOf=garbage")]
    [InlineData("?from=2026-02-30&to=2026-10-31")]
    public async Task ABrokenDate_IsA400_NotA500(string query)
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/habits/overview" + query)).StatusCode);
    }

    [Fact]
    public async Task EachHabit_CarriesItsPlan_ItsCompletedDays_AndTheCurrentStreak()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read", [1, 2, 3, 4, 5], "2026-09-01");
        await user.MarkAsync(habit, "2026-10-07", "2026-10-06", "2026-10-05");

        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-08");

        var item = Assert.Single(overview.EnumerateArray());
        Assert.Equal(habit, item.GetProperty("id").GetGuid());
        Assert.Equal("Read", item.GetProperty("title").GetString());
        Assert.Equal([1, 2, 3, 4, 5], item.GetProperty("schedule").EnumerateArray().Select(day => day.GetInt32()));
        Assert.Equal("2026-09-01", item.GetProperty("startDate").GetString());
        Assert.Equal(["2026-10-05", "2026-10-06", "2026-10-07"], item.GetProperty("completedDates").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(3, item.GetProperty("currentStreak").GetInt32()); // Thursday 8th is still open
    }

    [Fact]
    public async Task TheStreakSpansTheMonthBoundary_WhateverTheRequestedRange()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read", startDate: "2026-09-01");
        await user.MarkAsync(habit, Enumerable.Range(28, 3).Select(d => $"2026-09-{d}")
            .Concat(Enumerable.Range(1, 8).Select(d => $"2026-10-{d:00}")).ToArray()); // 28 Sep .. 8 Oct = 11 days

        var item = Assert.Single((await user.OverviewAsync("2026-10-08", "2026-10-08", asOf: "2026-10-08")).EnumerateArray());

        Assert.Equal(["2026-10-08"], item.GetProperty("completedDates").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(11, item.GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task TheClientsAsOfDecidesWhatTodayIs()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read", startDate: "2026-10-01");
        await user.MarkAsync(habit, "2026-10-06", "2026-10-07");

        var sameDay = Assert.Single((await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-08")).EnumerateArray());
        var nextDay = Assert.Single((await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-09")).EnumerateArray());

        Assert.Equal(2, sameDay.GetProperty("currentStreak").GetInt32()); // the 8th is open and skipped
        Assert.Equal(0, nextDay.GetProperty("currentStreak").GetInt32());  // for the client the 8th is over and was missed
    }

    [Fact]
    public async Task WithoutAsOf_TheServersDateIsUsed()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read", startDate: "2020-01-01");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await user.MarkAsync(habit, today.ToString("yyyy-MM-dd"), today.AddDays(-1).ToString("yyyy-MM-dd"));

        var item = Assert.Single((await user.OverviewAsync("2020-01-01", "2099-12-31")).EnumerateArray());

        Assert.InRange(item.GetProperty("currentStreak").GetInt32(), 1, 2); // a midnight may pass during the test
    }

    [Fact]
    public async Task TheStartDateEndsTheStreak_RecordsBeforeItAreListedButNotCounted()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read", startDate: "2026-10-07");
        await user.MarkAsync(habit, "2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08");

        var item = Assert.Single((await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-08")).EnumerateArray());

        Assert.Equal(4, item.GetProperty("completedDates").GetArrayLength());
        Assert.Equal(2, item.GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task ARecordOnAnUnscheduledDay_IsListed_ButDoesNotCountForTheStreak()
    {
        var user = await NewUser();
        var weekdays = await user.CreateHabitAsync("Work", [1, 2, 3, 4, 5], "2026-10-01");
        await user.MarkAsync(weekdays, "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04"); // Thu, Fri, then Sat + Sun

        var item = Assert.Single((await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-05")).EnumerateArray());

        Assert.Equal(4, item.GetProperty("completedDates").GetArrayLength());
        Assert.Equal(2, item.GetProperty("currentStreak").GetInt32()); // Mon 5th open; Fri + Thu count, the weekend does not
    }

    [Fact]
    public async Task ChangingTheSchedule_ReJudgesTheStreak_ThereIsNoScheduleHistory()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Gym", [0, 1, 2, 3, 4, 5, 6], "2026-10-01");
        await user.MarkAsync(habit, "2026-10-06", "2026-10-07"); // Tue + Wed
        Assert.Equal(2, Assert.Single((await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-08")).EnumerateArray()).GetProperty("currentStreak").GetInt32());

        await user.Client.PutAsync($"/api/habits/{habit}", new { title = "Gym", schedule = new[] { 6, 0 } }); // weekends only

        // Tue + Wed are no longer planned days, so they do not count - and no planned day has been missed either.
        Assert.Equal(0, Assert.Single((await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-08")).EnumerateArray()).GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task HabitsComeInCreationOrder_OfTheCurrentUserOnly()
    {
        var user = await NewUser();
        var other = await NewUser();
        var first = await user.CreateHabitAsync("First");
        var second = await user.CreateHabitAsync("Second");
        var third = await user.CreateHabitAsync("Third");
        await other.CreateHabitAsync("Not mine");

        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-08");

        Assert.Equal([first, second, third], overview.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task ARangeWithoutCompletions_GivesAnEmptyList_AndAZeroStreak()
    {
        var user = await NewUser();
        await user.CreateHabitAsync("Read", startDate: "2026-10-01");

        var item = Assert.Single((await user.OverviewAsync("2026-10-08", "2026-10-08", "2026-10-08")).EnumerateArray());

        Assert.Empty(item.GetProperty("completedDates").EnumerateArray());
        Assert.Equal(0, item.GetProperty("currentStreak").GetInt32());
    }

    [Fact]
    public async Task ExtremeDates_DoNotBreakTheStreakWalk()
    {
        var user = await NewUser();
        await user.CreateHabitAsync("Read", startDate: "2026-10-01");

        Assert.Single((await user.OverviewAsync("0001-01-01", "9999-12-31", "9999-12-31")).EnumerateArray());
        Assert.Single((await user.OverviewAsync("0001-01-01", "9999-12-31", "0001-01-01")).EnumerateArray());
    }
}
