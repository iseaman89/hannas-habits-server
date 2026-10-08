using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class ResolutionsApiTests
{
    private readonly ApiFactory _api;

    public ResolutionsApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    private static async Task<JsonElement> ListAsync(TestUser user, int year = 2026)
    {
        var response = await user.Client.GetAsync($"/api/resolutions/{year}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private static async Task<Guid> AddAsync(TestUser user, string title = "Read 12 books", int year = 2026, Guid? habitId = null)
    {
        var response = await user.Client.PostAsync($"/api/resolutions/{year}/items", new { title, habitId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Put(TestUser user, Guid id, object body, int year = 2026)
        => user.Client.PutAsync($"/api/resolutions/{year}/items/{id}", body);

    // ---- read ----

    [Fact]
    public async Task AYearWithoutResolutions_IsA200WithAnEmptyList()
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync("/api/resolutions/2031");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Theory]
    [InlineData("2000", HttpStatusCode.OK)]
    [InlineData("2100", HttpStatusCode.OK)]
    [InlineData("1999", HttpStatusCode.BadRequest)]
    [InlineData("2101", HttpStatusCode.BadRequest)]
    [InlineData("0", HttpStatusCode.BadRequest)]
    [InlineData("-5", HttpStatusCode.BadRequest)]
    [InlineData("abc", HttpStatusCode.BadRequest)]
    public async Task TheYear_MustBeBetween2000And2100(string year, HttpStatusCode expected)
    {
        var user = await NewUser();

        Assert.Equal(expected, (await user.Client.GetAsync($"/api/resolutions/{year}")).StatusCode);
    }

    // ---- add ----

    [Fact]
    public async Task Add_Answers201WithTheItem_NotKeptYet_AndTheYearsListAsLocation()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "  Read 12 books " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/resolutions/2026", response.Headers.Location!.ToString());
        var body = await response.ReadJsonAsync();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("Read 12 books", body.GetProperty("title").GetString());
        Assert.False(body.GetProperty("kept").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("habitId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("habitTitle").ValueKind);
    }

    [Fact]
    public async Task Add_TheListShowsTheItemsInCreationOrder_AndKeepsThatOrderAfterUpdates()
    {
        var user = await NewUser();
        var first = await AddAsync(user, "First");
        var second = await AddAsync(user, "Second");
        var third = await AddAsync(user, "Third");

        await Put(user, first, new { title = "First (edited)", kept = true });

        var items = (await ListAsync(user)).EnumerateArray().ToArray();
        Assert.Equal([first, second, third], items.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal(["First (edited)", "Second", "Third"], items.Select(i => i.GetProperty("title").GetString()));
        Assert.Equal([true, false, false], items.Select(i => i.GetProperty("kept").GetBoolean()));
    }

    [Fact]
    public async Task Add_AResolutionBelongsToItsYear()
    {
        var user = await NewUser();
        await AddAsync(user, "For 2026", 2026);
        await AddAsync(user, "For 2027", 2027);

        Assert.Equal(["For 2026"], (await ListAsync(user, 2026)).EnumerateArray().Select(i => i.GetProperty("title").GetString()));
        Assert.Equal(["For 2027"], (await ListAsync(user, 2027)).EnumerateArray().Select(i => i.GetProperty("title").GetString()));
    }

    [Theory]
    [InlineData("""{ "title": "" }""", "title")]
    [InlineData("""{ "title": "   " }""", "title")]
    [InlineData("""{}""", "title")]
    [InlineData("""{ "title": "ok", "habitId": "00000000-0000-0000-0000-000000000000" }""", "habitId")]
    public async Task Add_InvalidInput_IsA400OnThatField_AndNothingIsStored(string json, string key)
    {
        var user = await NewUser();

        var response = await user.Client.SendRawAsync(HttpMethod.Post, "/api/resolutions/2026/items", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(key, await response.ErrorKeysAsync());
        Assert.Empty((await ListAsync(user)).EnumerateArray());
    }

    [Fact]
    public async Task Add_TheTitleMayHave200Characters_NotMore()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.Created, (await user.Client.PostAsync("/api/resolutions/2026/items", new { title = new string('a', 200) })).StatusCode);

        var tooLong = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = new string('a', 201) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("title", await tooLong.ErrorKeysAsync());
    }

    [Fact]
    public async Task Add_ABadYearInTheRoute_IsA400()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/resolutions/1999/items", new { title = "Read" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("year", await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Add_AYearHoldsAtMostFiftyResolutions()
    {
        var user = await NewUser();
        for (var i = 1; i <= 50; i++)
            await AddAsync(user, $"Resolution {i}");

        var fiftyFirst = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "One too many" });

        Assert.Equal(HttpStatusCode.BadRequest, fiftyFirst.StatusCode);
        Assert.Contains("at most 50", (await fiftyFirst.ReadJsonAsync()).GetProperty("detail").GetString());
        Assert.Equal(50, (await ListAsync(user)).GetArrayLength());

        // Other years and other users are unaffected; deleting one makes room again.
        await AddAsync(user, "Next year", 2027);
        var stranger = await NewUser();
        await AddAsync(stranger, "Mine");
        var oneToDelete = (await ListAsync(user)).EnumerateArray().First().GetProperty("id").GetGuid();
        await user.Client.DeleteAsync($"/api/resolutions/2026/items/{oneToDelete}");
        Assert.Equal(HttpStatusCode.Created, (await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "Fits again" })).StatusCode);

        var last = (await ListAsync(user)).EnumerateArray().Last();
        Assert.Equal("Fits again", last.GetProperty("title").GetString()); // a new item sorts last
    }

    [Fact]
    public async Task Add_TenParallelRequests_AllSucceed()
    {
        var user = await NewUser();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => user.Client.PostAsync("/api/resolutions/2026/items", new { title = $"Item {i}" })));

        Assert.True(responses.All(r => r.StatusCode == HttpStatusCode.Created), string.Join(Environment.NewLine, _api.Logs.Errors.Where(e => e.Contains("GlobalExceptionHandler")).Take(2)));
        Assert.Equal(10, (await ListAsync(user)).GetArrayLength());
    }

    // ---- the habit link ----

    [Fact]
    public async Task ALinkedHabit_ShowsUpWithItsTitle_AndFollowsARename()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read every day", [1]);

        var add = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "Read 12 books", habitId = habit });
        var added = await add.ReadJsonAsync();
        Assert.Equal(habit, added.GetProperty("habitId").GetGuid());
        Assert.Equal("Read every day", added.GetProperty("habitTitle").GetString());

        await user.Client.PutAsync($"/api/habits/{habit}", new { title = "Read daily", schedule = new[] { 1 } });

        var item = Assert.Single((await ListAsync(user)).EnumerateArray());
        Assert.Equal(habit, item.GetProperty("habitId").GetGuid());
        Assert.Equal("Read daily", item.GetProperty("habitTitle").GetString());
    }

    [Fact]
    public async Task DeletingTheHabit_KeepsTheResolution_AndOnlyLosesTheLink()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read every day");
        await AddAsync(user, "Read 12 books", habitId: habit);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}")).StatusCode);

        var item = Assert.Single((await ListAsync(user)).EnumerateArray());
        Assert.Equal("Read 12 books", item.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("habitId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("habitTitle").ValueKind);
    }

    [Fact]
    public async Task ALinkToAnUnknownHabit_IsA400OnHabitId()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "Read", habitId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("habitId", await response.ErrorKeysAsync());
        Assert.Empty((await ListAsync(user)).EnumerateArray());
    }

    [Fact]
    public async Task ALinkToSomebodyElsesHabit_IsRefusedExactlyLikeAnUnknownOne()
    {
        var owner = await NewUser();
        var user = await NewUser();
        var foreignHabit = await owner.CreateHabitAsync("Not yours");
        var mine = await AddAsync(user, "Mine");

        var add = await user.Client.PostAsync("/api/resolutions/2026/items", new { title = "Sneaky", habitId = foreignHabit });
        var update = await Put(user, mine, new { title = "Mine", kept = false, habitId = foreignHabit });

        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode);
        Assert.Contains("habitId", await add.ErrorKeysAsync());
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Contains("habitId", await update.ErrorKeysAsync());

        var item = Assert.Single((await ListAsync(user)).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("habitId").ValueKind);
    }

    // ---- update ----

    [Fact]
    public async Task Update_TogglesKept_AndReplacesTheTitle()
    {
        var user = await NewUser();
        var id = await AddAsync(user, "Read");

        Assert.Equal(HttpStatusCode.NoContent, (await Put(user, id, new { title = "  Read more ", kept = true })).StatusCode);
        var kept = Assert.Single((await ListAsync(user)).EnumerateArray());
        Assert.Equal("Read more", kept.GetProperty("title").GetString());
        Assert.True(kept.GetProperty("kept").GetBoolean());

        await Put(user, id, new { title = "Read more", kept = false });
        Assert.False(Assert.Single((await ListAsync(user)).EnumerateArray()).GetProperty("kept").GetBoolean());
    }

    [Fact]
    public async Task Update_LeavingTheHabitOut_RemovesTheLink()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Read every day");
        var id = await AddAsync(user, "Read", habitId: habit);

        await Put(user, id, new { title = "Read", kept = false });

        Assert.Equal(JsonValueKind.Null, Assert.Single((await ListAsync(user)).EnumerateArray()).GetProperty("habitId").ValueKind);
    }

    [Theory]
    [InlineData("""{ "title": "Read" }""")]
    [InlineData("""{ "title": "Read", "kept": null }""")]
    [InlineData("""{ "title": "Read", "kept": "yes" }""")]
    public async Task Update_KeptIsRequired_ForgettingItMustNotQuietlyReopenAKeptResolution(string json)
    {
        var user = await NewUser();
        var id = await AddAsync(user, "Read");
        await Put(user, id, new { title = "Read", kept = true });

        var response = await user.Client.SendRawAsync(HttpMethod.Put, $"/api/resolutions/2026/items/{id}", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(Assert.Single((await ListAsync(user)).EnumerateArray()).GetProperty("kept").GetBoolean());
    }

    [Fact]
    public async Task Update_ABlankTitle_IsA400OnTitle()
    {
        var user = await NewUser();
        var id = await AddAsync(user, "Read");

        var response = await Put(user, id, new { title = " ", kept = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("title", await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Update_UnknownItemOrWrongYear_IsA404()
    {
        var user = await NewUser();
        var id = await AddAsync(user, "Read", 2026);

        Assert.Equal(HttpStatusCode.NotFound, (await Put(user, Guid.NewGuid(), new { title = "x", kept = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(user, id, new { title = "x", kept = true }, year: 2027)).StatusCode);
        Assert.Equal("Read", Assert.Single((await ListAsync(user)).EnumerateArray()).GetProperty("title").GetString());
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesTheItem_AndAnUnknownOneIsA404()
    {
        var user = await NewUser();
        var id = await AddAsync(user, "Read");
        var other = await AddAsync(user, "Keep");

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/resolutions/2026/items/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/resolutions/2026/items/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/resolutions/2027/items/{other}")).StatusCode); // wrong year

        Assert.Equal(other, Assert.Single((await ListAsync(user)).EnumerateArray()).GetProperty("id").GetGuid());
    }

    // ---- data isolation ----

    [Fact]
    public async Task AnotherUser_CannotSeeChangeOrDeleteResolutions()
    {
        var owner = await NewUser();
        var intruder = await NewUser();
        var id = await AddAsync(owner, "Private");

        Assert.Empty((await ListAsync(intruder)).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await Put(intruder, id, new { title = "Hacked", kept = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.Client.DeleteAsync($"/api/resolutions/2026/items/{id}")).StatusCode);

        var item = Assert.Single((await ListAsync(owner)).EnumerateArray());
        Assert.Equal("Private", item.GetProperty("title").GetString());
        Assert.False(item.GetProperty("kept").GetBoolean());
    }
}
