using System.Net;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class HabitsApiTests
{
    private readonly ApiFactory _api;

    public HabitsApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    // ---- create ----

    [Fact]
    public async Task Create_Answers201WithLocation_AndTheNewHabit()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/habits", new
        {
            title = "Read", description = "20 pages", schedule = new[] { 1, 2, 3 }, startDate = "2026-03-01"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Read", body.GetProperty("title").GetString());
        Assert.Equal([1, 2, 3], body.GetProperty("schedule").EnumerateArray().Select(day => day.GetInt32()));
        Assert.Equal("2026-03-01", body.GetProperty("startDate").GetString());
        Assert.EndsWith($"/api/habits/{id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_TheLocationLeadsToTheDetails()
    {
        var user = await NewUser();
        var created = await user.Client.PostAsync("/api/habits", new { title = "Read", description = "20 pages", startDate = "2026-03-01" });

        var details = await user.Client.GetAsync(created.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var body = await details.ReadJsonAsync();
        Assert.Equal("Read", body.GetProperty("title").GetString());
        Assert.Equal("20 pages", body.GetProperty("description").GetString());
        Assert.Equal("2026-03-01", body.GetProperty("startDate").GetString());
        Assert.True(body.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task Create_WithoutAScheduleThehabitIsPlannedEveryDay_WithoutAStartDateItStartsToday()
    {
        var user = await NewUser();
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await user.Client.PostAsync("/api/habits", new { title = "Read" });
        var body = await response.ReadJsonAsync();

        Assert.Equal([0, 1, 2, 3, 4, 5, 6], body.GetProperty("schedule").EnumerateArray().Select(day => day.GetInt32()));
        var start = DateOnly.Parse(body.GetProperty("startDate").GetString()!);
        Assert.InRange(start.DayNumber, utcToday.DayNumber - 1, utcToday.DayNumber + 1); // a midnight may pass during the test
    }

    [Fact]
    public async Task Create_TheScheduleIsReturnedSortedAndWithoutDuplicates()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/habits", new { title = "Run", schedule = new[] { 5, 1, 5, 6 } });

        Assert.Equal([1, 5, 6], (await response.ReadJsonAsync()).GetProperty("schedule").EnumerateArray().Select(day => day.GetInt32()));
    }

    [Fact]
    public async Task Create_TitleAndDescriptionAreTrimmed()
    {
        var user = await NewUser();

        var created = await user.Client.PostAsync("/api/habits", new { title = "  Read  ", description = "   " });
        var details = await user.Client.GetAsync(created.Headers.Location!.ToString());

        var body = await details.ReadJsonAsync();
        Assert.Equal("Read", body.GetProperty("title").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("description").ValueKind);
    }

    [Theory]
    [InlineData("", "title")]
    [InlineData("   ", "title")]
    public async Task Create_ABlankTitle_IsA400OnTitle(string title, string key)
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/habits", new { title });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(key, await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Create_TheLimits_150ForTheTitle_500ForTheDescription()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.Created, (await user.Client.PostAsync("/api/habits", new { title = new string('a', 150), description = new string('d', 500) })).StatusCode);

        var tooLongTitle = await user.Client.PostAsync("/api/habits", new { title = new string('a', 151) });
        var tooLongDescription = await user.Client.PostAsync("/api/habits", new { title = "Read", description = new string('d', 501) });

        Assert.Contains("title", await tooLongTitle.ErrorKeysAsync());
        Assert.Contains("description", await tooLongDescription.ErrorKeysAsync());
    }

    [Fact]
    public async Task Create_ABadSchedule_IsA400_AtTheRightPosition()
    {
        var user = await NewUser();

        var empty = await user.Client.PostAsync("/api/habits", new { title = "Read", schedule = Array.Empty<int>() });
        var outOfRange = await user.Client.PostAsync("/api/habits", new { title = "Read", schedule = new[] { 1, 7 } });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("schedule", await empty.ErrorKeysAsync());
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        Assert.Contains("schedule[1]", await outOfRange.ErrorKeysAsync());
    }

    [Theory]
    [InlineData("2000-01-01", HttpStatusCode.Created)]
    [InlineData("2100-12-31", HttpStatusCode.Created)]
    [InlineData("1999-12-31", HttpStatusCode.BadRequest)]
    [InlineData("2101-01-01", HttpStatusCode.BadRequest)]
    [InlineData("0001-01-01", HttpStatusCode.BadRequest)]
    [InlineData("9999-12-31", HttpStatusCode.BadRequest)]
    public async Task Create_TheStartDateMustBeBetween2000And2100(string startDate, HttpStatusCode expected)
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/habits", new { title = "Read", startDate });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
            Assert.Contains("startDate", await response.ErrorKeysAsync());
    }

    [Theory]
    [InlineData("\"2026-13-45\"")]
    [InlineData("\"10/08/2026\"")]
    [InlineData("20261008")]
    [InlineData("\"2026-10-08T10:00:00Z\"")]
    public async Task Create_ABrokenStartDate_IsA400_NotA500(string startDateJson)
    {
        var user = await NewUser();

        var response = await user.Client.SendRawAsync(HttpMethod.Post, "/api/habits", $$"""{ "title": "Read", "startDate": {{startDateJson}} }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await (await user.Client.GetAsync("/api/habits")).ReadAsync<List<object>>()); // nothing was stored
    }

    [Fact]
    public async Task Create_NoBodyOrBrokenJson_IsA400()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.SendRawAsync(HttpMethod.Post, "/api/habits", "{ nope")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.SendRawAsync(HttpMethod.Post, "/api/habits", "{}")).StatusCode);
    }

    // ---- read ----

    [Fact]
    public async Task List_ReturnsTheUsersHabitsWithTheirPlan()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", [1, 3], description: "20 pages");

        var body = await (await user.Client.GetAsync("/api/habits")).ReadJsonAsync();

        var habit = Assert.Single(body.EnumerateArray());
        Assert.Equal(id, habit.GetProperty("id").GetGuid());
        Assert.Equal("Read", habit.GetProperty("title").GetString());
        Assert.Equal("20 pages", habit.GetProperty("description").GetString());
        Assert.Equal([1, 3], habit.GetProperty("schedule").EnumerateArray().Select(day => day.GetInt32()));
    }

    [Fact]
    public async Task List_ANewUserHasNoHabits()
    {
        var user = await NewUser();

        Assert.Empty((await (await user.Client.GetAsync("/api/habits")).ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Details_AnUnknownHabit_IsA404Problem()
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync($"/api/habits/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, (await response.ReadJsonAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Details_ANonGuidId_IsA400_NotA500()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/habits/not-a-guid")).StatusCode);
    }

    // ---- update ----

    [Fact]
    public async Task Update_ReplacesTheHabit_ButNotItsStartDate()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", [1], "2026-03-01", "old");

        var update = await user.Client.PutAsync($"/api/habits/{id}", new { title = " Run ", description = "new", schedule = new[] { 6, 0 } });

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var body = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal("Run", body.GetProperty("title").GetString());
        Assert.Equal("new", body.GetProperty("description").GetString());
        Assert.Equal([0, 6], body.GetProperty("schedule").EnumerateArray().Select(day => day.GetInt32()));
        Assert.Equal("2026-03-01", body.GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task Update_TheScheduleIsRequired_AnUpdateReplacesTheWholeHabit()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", [1]);

        var response = await user.Client.PutAsync($"/api/habits/{id}", new { title = "Run" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("schedule", await response.ErrorKeysAsync());
        Assert.Equal("Read", (await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Update_ABadTitle_IsA400_AndChangesNothing()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", [1]);

        var response = await user.Client.PutAsync($"/api/habits/{id}", new { title = " ", schedule = new[] { 2 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title", await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Update_AnUnknownHabit_IsA404()
    {
        var user = await NewUser();

        var response = await user.Client.PutAsync($"/api/habits/{Guid.NewGuid()}", new { title = "Run", schedule = new[] { 2 } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesTheHabitAndItsRecords()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync();
        await user.MarkAsync(id, "2026-10-07", "2026-10-08");

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/habits/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/habits/{id}/records")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/habits/{id}")).StatusCode);
    }

    // ---- data isolation ----

    [Fact]
    public async Task AnotherUser_CannotSeeChangeOrDeleteAHabit()
    {
        var owner = await NewUser();
        var intruder = await NewUser();
        var id = await owner.CreateHabitAsync("Private", [1]);
        await owner.MarkAsync(id, "2026-10-08");

        // Everything answers exactly like "no such habit".
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.GetAsync($"/api/habits/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.PutAsync($"/api/habits/{id}", new { title = "Hacked", schedule = new[] { 1 } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.DeleteAsync($"/api/habits/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.GetAsync($"/api/habits/{id}/records")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.PutAsync($"/api/habits/{id}/records/2026-10-09")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.DeleteAsync($"/api/habits/{id}/records/2026-10-08")).StatusCode);

        Assert.Empty((await (await intruder.Client.GetAsync("/api/habits")).ReadJsonAsync()).EnumerateArray());
        Assert.Empty((await intruder.OverviewAsync("2026-10-01", "2026-10-31")).EnumerateArray());

        // The owner's habit is exactly as it was.
        var body = await (await owner.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal("Private", body.GetProperty("title").GetString());
        Assert.Equal(["2026-10-08"], await owner.RecordDatesAsync(id));
    }

    [Fact]
    public async Task TwoUsersMayHaveHabitsWithTheSameTitle()
    {
        var first = await NewUser();
        var second = await NewUser();

        await first.CreateHabitAsync("Read");
        await second.CreateHabitAsync("Read");

        Assert.Single((await (await first.Client.GetAsync("/api/habits")).ReadJsonAsync()).EnumerateArray());
        Assert.Single((await (await second.Client.GetAsync("/api/habits")).ReadJsonAsync()).EnumerateArray());
    }

    // ---- authentication ----

    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "GET", "/api/habits" },
        { "POST", "/api/habits" },
        { "GET", "/api/habits/overview?from=2026-10-01&to=2026-10-31" },
        { "GET", "/api/habits/00000000-0000-0000-0000-000000000001" },
        { "PUT", "/api/habits/00000000-0000-0000-0000-000000000001" },
        { "DELETE", "/api/habits/00000000-0000-0000-0000-000000000001" },
        { "GET", "/api/habits/00000000-0000-0000-0000-000000000001/records" },
        { "PUT", "/api/habits/00000000-0000-0000-0000-000000000001/records/2026-10-08" },
        { "DELETE", "/api/habits/00000000-0000-0000-0000-000000000001/records/2026-10-08" },
        { "GET", "/api/daily-diaries" },
        { "GET", "/api/daily-diaries/2026-10-08" },
        { "PUT", "/api/daily-diaries/2026-10-08" },
        { "DELETE", "/api/daily-diaries/2026-10-08" },
        { "GET", "/api/resolutions/2026" },
        { "POST", "/api/resolutions/2026/items" },
        { "PUT", "/api/resolutions/2026/items/00000000-0000-0000-0000-000000000001" },
        { "DELETE", "/api/resolutions/2026/items/00000000-0000-0000-0000-000000000001" }
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task EveryDataEndpoint_NeedsAnAccessToken(string method, string url)
    {
        var response = await TestUser.Anonymous(_api).SendRawAsync(new HttpMethod(method), url, method is "POST" or "PUT" ? "{}" : null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
