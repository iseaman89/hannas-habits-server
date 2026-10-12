using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class TagsApiTests
{
    private readonly ApiFactory _api;

    public TagsApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    private static string[] Names(JsonElement tags) => tags.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToArray();

    private static JsonElement TaggedDay(JsonElement habitOverview, string date)
        => habitOverview.GetProperty("taggedDays").EnumerateArray().Single(d => d.GetProperty("date").GetString() == date);

    private static JsonElement ItemOf(JsonElement overview, Guid habitId)
        => overview.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == habitId);

    private static Task<HttpResponseMessage> Attach(TestUser user, Guid habitId, string date, Guid tagId)
        => user.Client.PutAsync($"/api/habits/{habitId}/days/{date}/tags/{tagId}");

    private static Task<HttpResponseMessage> Detach(TestUser user, Guid habitId, string date, Guid tagId)
        => user.Client.DeleteAsync($"/api/habits/{habitId}/days/{date}/tags/{tagId}");

    // ---- create ----

    [Fact]
    public async Task Create_Answers201WithTheTag_AndTheListAsLocation()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/tags", new { name = "  krank " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/tags", response.Headers.Location!.ToString());
        var body = await response.ReadJsonAsync();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("krank", body.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("krank")]
    [InlineData("KRANK")]
    [InlineData("  Krank  ")]
    public async Task Create_ANameThatExists_IgnoringCase_Answers200WithTheExistingTag(string again)
    {
        var user = await NewUser();
        var id = await user.CreateTagAsync("krank");

        var response = await user.Client.PostAsync("/api/tags", new { name = again });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("krank", body.GetProperty("name").GetString()); // the spelling it was stored with
        Assert.Single((await user.TagsAsync()).EnumerateArray());
    }

    [Theory]
    [InlineData("""{ "name": "" }""")]
    [InlineData("""{ "name": "   " }""")]
    [InlineData("""{}""")]
    [InlineData("""{ "name": null }""")]
    public async Task Create_ABlankOrMissingName_IsA400OnName_AndNothingIsStored(string json)
    {
        var user = await NewUser();

        var response = await user.Client.SendRawAsync(HttpMethod.Post, "/api/tags", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", await response.ErrorKeysAsync());
        Assert.Empty((await user.TagsAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Create_TheName_MayHave30Characters_NotMore()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.Created, (await user.Client.PostAsync("/api/tags", new { name = new string('a', 30) })).StatusCode);

        var tooLong = await user.Client.PostAsync("/api/tags", new { name = new string('b', 31) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("name", await tooLong.ErrorKeysAsync());
    }

    [Fact]
    public async Task Create_ATagBeyondTheLimitOf200_IsA400_AndTheExistingOnesStillAnswer200()
    {
        var user = await NewUser();
        for (var i = 0; i < 200; i++)
            await user.CreateTagAsync($"tag {i}");

        var response = await user.Client.PostAsync("/api/tags", new { name = "one too many" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("200", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
        Assert.Equal(200, (await user.TagsAsync()).GetArrayLength());

        // The limit is about new tags: asking for one of the 200 is still fine.
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PostAsync("/api/tags", new { name = "TAG 7" })).StatusCode);
    }

    [Fact]
    public async Task Create_TwoPeopleMayHaveTheSameName_EachHasTheirOwnTag()
    {
        var first = await NewUser();
        var second = await NewUser();

        var one = await first.CreateTagAsync("krank");
        var two = await second.CreateTagAsync("krank"); // 201 for both: not "taken"

        Assert.NotEqual(one, two);
    }

    // ---- list ----

    [Fact]
    public async Task List_ANewUserHasNoTags_A200WithAnEmptyList()
    {
        var user = await NewUser();

        Assert.Empty((await user.TagsAsync()).EnumerateArray());
    }

    [Fact]
    public async Task List_ShowsHowOftenAndWhenLastEachTagWasUsed()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var other = await user.CreateHabitAsync("Run");
        var sick = await user.CreateTagAsync("krank");
        var holiday = await user.CreateTagAsync("Urlaub");
        await user.CreateTagAsync("unused");
        await user.TagAsync(habit, "2026-10-01", sick);
        await user.TagAsync(habit, "2026-10-05", sick);
        await user.TagAsync(other, "2026-10-03", sick, holiday);

        var tags = (await user.TagsAsync()).EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!);

        Assert.Equal(3, tags["krank"].GetProperty("useCount").GetInt32());
        Assert.Equal("2026-10-05", tags["krank"].GetProperty("lastUsedOn").GetString());
        Assert.Equal(1, tags["Urlaub"].GetProperty("useCount").GetInt32());
        Assert.Equal("2026-10-03", tags["Urlaub"].GetProperty("lastUsedOn").GetString());
        Assert.Equal(0, tags["unused"].GetProperty("useCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, tags["unused"].GetProperty("lastUsedOn").ValueKind);
        Assert.All(tags.Values, tag => Assert.Equal(0, tag.GetProperty("habitUseCount").GetInt32())); // no habit asked for
    }

    [Fact]
    public async Task List_WithoutAHabit_IsOrderedByUseThenName_IgnoringCase()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var beta = await user.CreateTagAsync("beta");
        await user.CreateTagAsync("Zeta");
        await user.CreateTagAsync("alpha");
        var busy = await user.CreateTagAsync("busy");
        await user.TagAsync(habit, "2026-10-01", busy, beta);
        await user.TagAsync(habit, "2026-10-02", busy);

        Assert.Equal(["busy", "beta", "alpha", "Zeta"], Names(await user.TagsAsync()));
    }

    [Fact]
    public async Task List_ForAHabit_PutsTheTagsUsedInThatHabitFirst_ThenTheOnesUsedOverall()
    {
        var user = await NewUser();
        var run = await user.CreateHabitAsync("Run");
        var read = await user.CreateHabitAsync("Read");
        var x = await user.CreateTagAsync("x-in-run");
        var y = await user.CreateTagAsync("y-mostly-in-read");
        await user.CreateTagAsync("z-unused");
        await user.TagAsync(run, "2026-10-01", x);
        await user.TagAsync(run, "2026-10-02", x);
        await user.TagAsync(run, "2026-10-03", x);
        await user.TagAsync(run, "2026-10-01", y);
        for (var day = 1; day <= 5; day++)
            await user.TagAsync(read, $"2026-10-0{day}", y);

        var forRun = (await user.TagsAsync(run)).EnumerateArray().ToArray();
        var forRead = (await user.TagsAsync(read)).EnumerateArray().ToArray();
        var overall = await user.TagsAsync();

        Assert.Equal(["x-in-run", "y-mostly-in-read", "z-unused"], forRun.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal([3, 1, 0], forRun.Select(t => t.GetProperty("habitUseCount").GetInt32()));
        Assert.Equal([3, 6, 0], forRun.Select(t => t.GetProperty("useCount").GetInt32())); // overall use is kept
        Assert.Equal(["y-mostly-in-read", "x-in-run", "z-unused"], forRead.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(["y-mostly-in-read", "x-in-run", "z-unused"], Names(overall)); // 6 vs 3 overall
    }

    [Fact]
    public async Task List_ForAnUnknownOrAnotherUsersHabit_Is404()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var foreign = await stranger.CreateHabitAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/tags?habitId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/tags?habitId={foreign}")).StatusCode);
    }

    [Fact]
    public async Task List_AMalformedHabitId_Is400()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/tags?habitId=not-a-guid")).StatusCode);
    }

    [Fact]
    public async Task List_ShowsOnlyTheUsersOwnTags()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        await user.CreateTagAsync("mine");
        await stranger.CreateTagAsync("theirs");

        Assert.Equal(["mine"], Names(await user.TagsAsync()));
        Assert.Equal(["theirs"], Names(await stranger.TagsAsync()));
    }

    // ---- attach ----

    [Fact]
    public async Task Attach_Answers200WithTheDaysTags_ByName()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var sick = await user.CreateTagAsync("krank");
        var holiday = await user.CreateTagAsync("Urlaub");
        await Attach(user, habit, "2026-10-08", holiday);

        var response = await Attach(user, habit, "2026-10-08", sick);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var day = await response.ReadJsonAsync();
        Assert.Equal("2026-10-08", day.GetProperty("date").GetString());
        Assert.Equal(["krank", "Urlaub"], Names(day.GetProperty("tags")));
        Assert.Equal(sick, day.GetProperty("tags")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Attach_IsIdempotent()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync();

        await Attach(user, habit, "2026-10-08", tag);
        var again = await Attach(user, habit, "2026-10-08", tag);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Single((await again.ReadJsonAsync()).GetProperty("tags").EnumerateArray());
        Assert.Equal(1, (await user.TagsAsync())[0].GetProperty("useCount").GetInt32());
    }

    [Fact]
    public async Task Attach_TheSameTagWorksInEveryHabit()
    {
        var user = await NewUser();
        var run = await user.CreateHabitAsync("Run");
        var read = await user.CreateHabitAsync("Read");
        var sick = await user.CreateTagAsync("krank");

        await user.TagAsync(run, "2026-10-08", sick);
        await user.TagAsync(read, "2026-10-08", sick);

        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31");
        Assert.Equal(["krank"], Names(TaggedDay(ItemOf(overview, run), "2026-10-08").GetProperty("tags")));
        Assert.Equal(["krank"], Names(TaggedDay(ItemOf(overview, read), "2026-10-08").GetProperty("tags")));
    }

    [Fact]
    public async Task Attach_ADayCanBeTagged_WhetherItWasCompletedOrNot()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync(startDate: "2026-10-01");
        var sick = await user.CreateTagAsync("krank");
        await user.MarkAsync(habit, "2026-10-02");

        await user.TagAsync(habit, "2026-10-02", sick); // done
        await user.TagAsync(habit, "2026-10-03", sick); // missed - the point of "krank"

        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);
        Assert.Equal(["2026-10-02"], item.GetProperty("completedDates").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(["2026-10-02", "2026-10-03"], item.GetProperty("taggedDays").EnumerateArray().Select(d => d.GetProperty("date").GetString()));
    }

    [Fact]
    public async Task Attach_TheEleventhTagOnADay_IsA400_AndTheDayStaysAsItWas()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        for (var i = 1; i <= 10; i++)
            await user.TagAsync(habit, "2026-10-08", await user.CreateTagAsync($"tag {i}"));
        var eleventh = await user.CreateTagAsync("one too many");

        var response = await Attach(user, habit, "2026-10-08", eleventh);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("10", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
        var day = TaggedDay(ItemOf(await user.OverviewAsync("2026-10-08", "2026-10-08"), habit), "2026-10-08");
        Assert.Equal(10, day.GetProperty("tags").GetArrayLength());

        // Another day of the same habit has its own ten.
        await user.TagAsync(habit, "2026-10-09", eleventh);
    }

    [Fact]
    public async Task Attach_AnUnknownHabitOrTag_Is404()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Attach(user, Guid.NewGuid(), "2026-10-08", tag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Attach(user, habit, "2026-10-08", Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Attach_AnotherUsersHabitOrTag_Is404_AndNothingIsTagged()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var mine = await user.CreateHabitAsync();
        var myTag = await user.CreateTagAsync("mine");
        var theirs = await stranger.CreateHabitAsync();
        var theirTag = await stranger.CreateTagAsync("theirs");

        Assert.Equal(HttpStatusCode.NotFound, (await Attach(user, theirs, "2026-10-08", myTag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Attach(user, mine, "2026-10-08", theirTag)).StatusCode);

        Assert.Empty(ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), mine).GetProperty("taggedDays").EnumerateArray());
        Assert.Empty(ItemOf(await stranger.OverviewAsync("2026-10-01", "2026-10-31"), theirs).GetProperty("taggedDays").EnumerateArray());
    }

    [Theory]
    [InlineData("not-a-date", HttpStatusCode.BadRequest)]
    [InlineData("1999-12-31", HttpStatusCode.BadRequest)]
    [InlineData("2101-01-01", HttpStatusCode.BadRequest)]
    [InlineData("2000-01-01", HttpStatusCode.OK)]
    [InlineData("2100-12-31", HttpStatusCode.OK)]
    public async Task Attach_TheDay_MustBeADateInThePlausibleRange(string date, HttpStatusCode expected)
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync();

        Assert.Equal(expected, (await Attach(user, habit, date, tag)).StatusCode);
    }

    [Fact]
    public async Task Attach_NeedsASignedInUser()
    {
        var anonymous = TestUser.Anonymous(_api);

        var response = await anonymous.PutAsync($"/api/habits/{Guid.NewGuid()}/days/2026-10-08/tags/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/tags")).StatusCode);
    }

    // ---- detach ----

    [Fact]
    public async Task Detach_Answers200WithTheTagsLeft()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var sick = await user.CreateTagAsync("krank");
        var holiday = await user.CreateTagAsync("Urlaub");
        await user.TagAsync(habit, "2026-10-08", sick, holiday);

        var response = await Detach(user, habit, "2026-10-08", sick);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Urlaub"], Names((await response.ReadJsonAsync()).GetProperty("tags")));
    }

    [Fact]
    public async Task Detach_TheLastTag_LeavesAnUntaggedDay_AndTheTagItselfStays()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync("krank");
        await user.TagAsync(habit, "2026-10-08", tag);

        var response = await Detach(user, habit, "2026-10-08", tag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).GetProperty("tags").EnumerateArray());
        Assert.Empty(ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit).GetProperty("taggedDays").EnumerateArray());
        Assert.Equal(0, (await user.TagsAsync())[0].GetProperty("useCount").GetInt32());

        // ...and the day can be tagged again: the emptied entry did not block anything.
        await user.TagAsync(habit, "2026-10-08", tag);
    }

    [Fact]
    public async Task Detach_ATagTheDayDoesNotHave_IsNoError()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync();

        var response = await Detach(user, habit, "2026-10-08", tag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).GetProperty("tags").EnumerateArray());
    }

    [Fact]
    public async Task Detach_AnUnknownHabitOrTag_Is404()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Detach(user, Guid.NewGuid(), "2026-10-08", tag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Detach(user, habit, "2026-10-08", Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Detach_AnotherUsersDay_Is404_AndTheirTagStaysOnIt()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var theirs = await stranger.CreateHabitAsync();
        var theirTag = await stranger.CreateTagAsync();
        var myTag = await user.CreateTagAsync();
        await stranger.TagAsync(theirs, "2026-10-08", theirTag);

        Assert.Equal(HttpStatusCode.NotFound, (await Detach(user, theirs, "2026-10-08", theirTag)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Detach(user, theirs, "2026-10-08", myTag)).StatusCode);

        var day = TaggedDay(ItemOf(await stranger.OverviewAsync("2026-10-08", "2026-10-08"), theirs), "2026-10-08");
        Assert.Single(day.GetProperty("tags").EnumerateArray());
    }

    // ---- overview ----

    [Fact]
    public async Task Overview_CarriesTheTaggedDaysOfTheRange_OldestFirst_WithTheTagsByName()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync(startDate: "2026-09-01");
        var sick = await user.CreateTagAsync("krank");
        var holiday = await user.CreateTagAsync("Urlaub");
        await user.TagAsync(habit, "2026-10-07", holiday, sick);
        await user.TagAsync(habit, "2026-10-02", sick);
        await user.TagAsync(habit, "2026-09-30", sick); // before the range
        await user.TagAsync(habit, "2026-11-01", sick); // after it

        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);

        var days = item.GetProperty("taggedDays").EnumerateArray().ToArray();
        Assert.Equal(["2026-10-02", "2026-10-07"], days.Select(d => d.GetProperty("date").GetString()));
        Assert.Equal(["krank"], Names(days[0].GetProperty("tags")));
        Assert.Equal(["krank", "Urlaub"], Names(days[1].GetProperty("tags")));
    }

    [Fact]
    public async Task Overview_AHabitWithoutTags_HasAnEmptyList_NotAMissingField()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();

        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);

        Assert.Equal(JsonValueKind.Array, item.GetProperty("taggedDays").ValueKind);
        Assert.Empty(item.GetProperty("taggedDays").EnumerateArray());
    }

    [Fact]
    public async Task Overview_TagsStayWhenTheDayIsMarkedOrUnmarked()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync(startDate: "2026-10-01");
        var sick = await user.CreateTagAsync("krank");
        await user.TagAsync(habit, "2026-10-05", sick);

        await user.MarkAsync(habit, "2026-10-05");
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-05")).StatusCode);

        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);
        Assert.Equal(["2026-10-05"], item.GetProperty("taggedDays").EnumerateArray().Select(d => d.GetProperty("date").GetString()));
    }

    [Fact]
    public async Task Overview_NeverShowsAnotherUsersTags()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var mine = await user.CreateHabitAsync();
        var theirs = await stranger.CreateHabitAsync();
        await stranger.TagAsync(theirs, "2026-10-08", await stranger.CreateTagAsync("secret"));

        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31");

        Assert.Empty(ItemOf(overview, mine).GetProperty("taggedDays").EnumerateArray());
        Assert.DoesNotContain("secret", overview.GetRawText());
    }

    // ---- rename ----

    [Fact]
    public async Task Rename_Answers200WithTheNewName_NotMerged_AndTheDaysShowIt()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var tag = await user.CreateTagAsync("krank");
        await user.TagAsync(habit, "2026-10-08", tag);

        var response = await user.Client.PutAsync($"/api/tags/{tag}", new { name = "  Erkältet " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(tag, body.GetProperty("id").GetGuid());
        Assert.Equal("Erkältet", body.GetProperty("name").GetString());
        Assert.False(body.GetProperty("merged").GetBoolean());
        var day = TaggedDay(ItemOf(await user.OverviewAsync("2026-10-08", "2026-10-08"), habit), "2026-10-08");
        Assert.Equal(["Erkältet"], Names(day.GetProperty("tags")));
    }

    [Fact]
    public async Task Rename_ChangingOnlyTheCase_IsARename_NotAMergeWithItself()
    {
        var user = await NewUser();
        var tag = await user.CreateTagAsync("krank");

        var response = await user.Client.PutAsync($"/api/tags/{tag}", new { name = "KRANK" });

        var body = await response.ReadJsonAsync();
        Assert.Equal(tag, body.GetProperty("id").GetGuid());
        Assert.Equal("KRANK", body.GetProperty("name").GetString());
        Assert.False(body.GetProperty("merged").GetBoolean());
        Assert.Equal(["KRANK"], Names(await user.TagsAsync()));
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherTag_MergesThem()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var flu = await user.CreateTagAsync("Grippe");
        var sick = await user.CreateTagAsync("krank");
        await user.TagAsync(habit, "2026-10-01", flu);
        await user.TagAsync(habit, "2026-10-02", flu, sick); // has both: keeps one
        await user.TagAsync(habit, "2026-10-03", sick);

        var response = await user.Client.PutAsync($"/api/tags/{flu}", new { name = "KRANK" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(sick, body.GetProperty("id").GetGuid());     // the survivor
        Assert.Equal("krank", body.GetProperty("name").GetString()); // with its own spelling
        Assert.True(body.GetProperty("merged").GetBoolean());

        var tags = (await user.TagsAsync()).EnumerateArray().Single();
        Assert.Equal("krank", tags.GetProperty("name").GetString());
        Assert.Equal(3, tags.GetProperty("useCount").GetInt32());

        var days = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit).GetProperty("taggedDays").EnumerateArray().ToArray();
        Assert.Equal(["2026-10-01", "2026-10-02", "2026-10-03"], days.Select(d => d.GetProperty("date").GetString()));
        Assert.All(days, day => Assert.Equal(["krank"], Names(day.GetProperty("tags"))));

        // The renamed tag is gone.
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PutAsync($"/api/tags/{flu}", new { name = "x" })).StatusCode);
    }

    [Fact]
    public async Task Rename_AMergeAcrossHabits_MovesEveryDay_AndKeepsTheOtherTagsOfThoseDays()
    {
        var user = await NewUser();
        var run = await user.CreateHabitAsync("Run");
        var read = await user.CreateHabitAsync("Read");
        var flu = await user.CreateTagAsync("Grippe");
        var sick = await user.CreateTagAsync("krank");
        var holiday = await user.CreateTagAsync("Urlaub");
        await user.TagAsync(run, "2026-10-01", flu, holiday);
        await user.TagAsync(read, "2026-10-01", flu);

        await user.Client.PutAsync($"/api/tags/{flu}", new { name = "krank" });

        var overview = await user.OverviewAsync("2026-10-01", "2026-10-01");
        Assert.Equal(["krank", "Urlaub"], Names(TaggedDay(ItemOf(overview, run), "2026-10-01").GetProperty("tags")));
        Assert.Equal(["krank"], Names(TaggedDay(ItemOf(overview, read), "2026-10-01").GetProperty("tags")));
        Assert.Equal(sick, TaggedDay(ItemOf(overview, run), "2026-10-01").GetProperty("tags")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherUsersTag_IsJustARename()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var tag = await user.CreateTagAsync("krank");
        await stranger.CreateTagAsync("Urlaub");

        var response = await user.Client.PutAsync($"/api/tags/{tag}", new { name = "urlaub" });

        Assert.False((await response.ReadJsonAsync()).GetProperty("merged").GetBoolean());
        Assert.Equal(["Urlaub"], Names(await stranger.TagsAsync()));
    }

    [Theory]
    [InlineData("""{ "name": "" }""")]
    [InlineData("""{ "name": "   " }""")]
    [InlineData("""{}""")]
    public async Task Rename_InvalidInput_IsA400OnName_AndTheTagKeepsItsName(string json)
    {
        var user = await NewUser();
        var tag = await user.CreateTagAsync("krank");

        var response = await user.Client.SendRawAsync(HttpMethod.Put, $"/api/tags/{tag}", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", await response.ErrorKeysAsync());
        Assert.Equal(["krank"], Names(await user.TagsAsync()));
    }

    [Fact]
    public async Task Rename_AnUnknownOrAnotherUsersTag_Is404_AndTheOtherOneKeepsItsName()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var theirs = await stranger.CreateTagAsync("krank");

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PutAsync($"/api/tags/{Guid.NewGuid()}", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PutAsync($"/api/tags/{theirs}", new { name = "mine now" })).StatusCode);

        Assert.Equal(["krank"], Names(await stranger.TagsAsync()));
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesTheTagFromEveryDay_AndSaysOnHowManyItWas()
    {
        var user = await NewUser();
        var run = await user.CreateHabitAsync("Run");
        var read = await user.CreateHabitAsync("Read");
        var sick = await user.CreateTagAsync("krank");
        var holiday = await user.CreateTagAsync("Urlaub");
        await user.TagAsync(run, "2026-10-01", sick, holiday);
        await user.TagAsync(run, "2026-10-02", sick);
        await user.TagAsync(read, "2026-10-01", sick);

        var response = await user.Client.DeleteAsync($"/api/tags/{sick}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, (await response.ReadJsonAsync()).GetProperty("removedFromDays").GetInt32());

        Assert.Equal(["Urlaub"], Names(await user.TagsAsync()));
        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31");
        var runDays = ItemOf(overview, run).GetProperty("taggedDays").EnumerateArray().ToArray();
        Assert.Equal("2026-10-01", Assert.Single(runDays).GetProperty("date").GetString()); // 10-02 held only this tag
        Assert.Equal(["Urlaub"], Names(runDays[0].GetProperty("tags")));
        Assert.Empty(ItemOf(overview, read).GetProperty("taggedDays").EnumerateArray());
    }

    [Fact]
    public async Task Delete_ATagThatIsNotUsed_Answers200WithZero()
    {
        var user = await NewUser();
        var tag = await user.CreateTagAsync();

        var response = await user.Client.DeleteAsync($"/api/tags/{tag}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.ReadJsonAsync()).GetProperty("removedFromDays").GetInt32());
        Assert.Empty((await user.TagsAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Delete_ADeletedTagCanBeCreatedAgain_AsANewOne()
    {
        var user = await NewUser();
        var first = await user.CreateTagAsync("krank");
        await user.Client.DeleteAsync($"/api/tags/{first}");

        var second = await user.CreateTagAsync("krank");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Delete_AnUnknownOrAnotherUsersTag_Is404_AndTheOtherOneStaysOnItsDays()
    {
        var user = await NewUser();
        var stranger = await NewUser();
        var habit = await stranger.CreateHabitAsync();
        var theirs = await stranger.CreateTagAsync("krank");
        await stranger.TagAsync(habit, "2026-10-08", theirs);

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/tags/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/tags/{theirs}")).StatusCode);

        var day = TaggedDay(ItemOf(await stranger.OverviewAsync("2026-10-08", "2026-10-08"), habit), "2026-10-08");
        Assert.Equal(["krank"], Names(day.GetProperty("tags")));
    }

    [Fact]
    public async Task DeletingAHabit_TakesItsTaggedDaysAlong_TheTagsThemselvesStay()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync();
        var other = await user.CreateHabitAsync("Read");
        var tag = await user.CreateTagAsync("krank");
        await user.TagAsync(habit, "2026-10-08", tag);
        await user.TagAsync(other, "2026-10-08", tag);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}")).StatusCode);

        var tags = (await user.TagsAsync()).EnumerateArray().Single();
        Assert.Equal("krank", tags.GetProperty("name").GetString());
        Assert.Equal(1, tags.GetProperty("useCount").GetInt32());
    }
}
