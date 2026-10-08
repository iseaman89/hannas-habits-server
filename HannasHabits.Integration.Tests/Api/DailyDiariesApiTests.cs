using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class DailyDiariesApiTests
{
    private const string Day = "2026-10-08";

    private readonly ApiFactory _api;

    public DailyDiariesApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    private static Task<HttpResponseMessage> Save(TestUser user, object? document, string date = Day)
        => user.Client.PutAsync($"/api/daily-diaries/{date}", document);

    private static Task<HttpResponseMessage> Get(TestUser user, string date = Day) => user.Client.GetAsync($"/api/daily-diaries/{date}");

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    // ---- read / write ----

    [Fact]
    public async Task ADayWithoutEntry_IsA404Problem_TheFrontendShowsAnEmptyForm()
    {
        var user = await NewUser();

        var response = await Get(user);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Save_StoresTheWholeDocument_AnswersWithoutBody_AndRoundTrips()
    {
        var user = await NewUser();

        var save = await Save(user, new
        {
            mood = 4, body = 70, mind = 40, highlight = "A good day",
            grateful = new[] { "coffee", "sun" }, learned = new[] { "some Rust" },
            tasks = new[] { new { title = "Call mum", done = true }, new { title = "Run", done = false } }
        });

        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        Assert.Equal(0, save.Content.Headers.ContentLength ?? 0);

        var body = await (await Get(user)).ReadJsonAsync();
        Assert.Equal(Day, body.GetProperty("date").GetString());
        Assert.Equal(4, body.GetProperty("mood").GetInt32());
        Assert.Equal(70, body.GetProperty("body").GetInt32());
        Assert.Equal(40, body.GetProperty("mind").GetInt32());
        Assert.Equal("A good day", body.GetProperty("highlight").GetString());
        Assert.Equal(["coffee", "sun"], Strings(body.GetProperty("grateful")));
        Assert.Equal(["some Rust"], Strings(body.GetProperty("learned")));
        var tasks = body.GetProperty("tasks").EnumerateArray().ToArray();
        Assert.Equal(["Call mum", "Run"], tasks.Select(t => t.GetProperty("title").GetString()));
        Assert.Equal([true, false], tasks.Select(t => t.GetProperty("done").GetBoolean()));
        Assert.False(body.TryGetProperty("id", out _)); // the date is the key, there is no id
    }

    [Fact]
    public async Task Save_TrimsTexts_KeepsTheUsersOrder_AndDuplicates()
    {
        var user = await NewUser();

        await Save(user, new { highlight = "  padded  ", grateful = new[] { "  b ", "a", "b" }, tasks = new[] { new { title = "  z ", done = false }, new { title = "a", done = true } } });

        var body = await (await Get(user)).ReadJsonAsync();
        Assert.Equal("padded", body.GetProperty("highlight").GetString());
        Assert.Equal(["b", "a", "b"], Strings(body.GetProperty("grateful")));
        Assert.Equal(["z", "a"], body.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Save_TextSurvivesUnchanged_UmlautsEmojiQuotesAndMarkup()
    {
        var user = await NewUser();
        var tricky = new[] { "Grüße 🎉", "say \"hi\"", "back\\slash", "</script><b>x</b>", "line1\nline2" };

        await Save(user, new { highlight = "Ärger → Freude", grateful = tricky, tasks = tricky.Select(title => new { title, done = false }).ToArray() });

        var body = await (await Get(user)).ReadJsonAsync();
        Assert.Equal("Ärger → Freude", body.GetProperty("highlight").GetString());
        Assert.Equal(tricky, Strings(body.GetProperty("grateful")));
        Assert.Equal(tricky, body.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Save_ReplacesTheWholeDocument_WhatIsLeftOutIsCleared()
    {
        var user = await NewUser();
        await Save(user, new { mood = 5, body = 90, mind = 80, highlight = "old", grateful = new[] { "a" }, learned = new[] { "b" }, tasks = new[] { new { title = "t", done = true } } });

        await Save(user, new { highlight = "new" });

        var body = await (await Get(user)).ReadJsonAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("mood").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("body").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("mind").ValueKind);
        Assert.Equal("new", body.GetProperty("highlight").GetString());
        Assert.Empty(body.GetProperty("grateful").EnumerateArray());
        Assert.Empty(body.GetProperty("learned").EnumerateArray());
        Assert.Empty(body.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public async Task Save_IsIdempotent()
    {
        var user = await NewUser();
        var document = new { mood = 3, highlight = "same" };

        await Save(user, document);
        await Save(user, document);

        Assert.Equal("same", (await (await Get(user)).ReadJsonAsync()).GetProperty("highlight").GetString());
        Assert.Single((await (await user.Client.GetAsync("/api/daily-diaries")).ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Save_PascalCasePropertyNamesBindToo()
    {
        var user = await NewUser();

        var response = await user.Client.SendRawAsync(HttpMethod.Put, $"/api/daily-diaries/{Day}", """{ "Mood": 2, "Highlight": "x", "Tasks": [ { "Title": "t", "Done": true } ] }""");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var body = await (await Get(user)).ReadJsonAsync();
        Assert.Equal(2, body.GetProperty("mood").GetInt32());
        Assert.True(body.GetProperty("tasks")[0].GetProperty("done").GetBoolean());
    }

    [Fact]
    public async Task Save_AnEntryMayBeJustAMood_OrJustABodyOfZero()
    {
        var user = await NewUser();

        await Save(user, new { mood = 1 }, "2026-10-01");
        await Save(user, new { body = 0 }, "2026-10-02");

        Assert.Equal(1, (await (await Get(user, "2026-10-01")).ReadJsonAsync()).GetProperty("mood").GetInt32());
        Assert.Equal(0, (await (await Get(user, "2026-10-02")).ReadJsonAsync()).GetProperty("body").GetInt32());
    }

    // ---- an empty document is "no entry" ----

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "highlight": "   ", "grateful": [], "learned": [], "tasks": [] }""")]
    [InlineData("""{ "mood": null, "body": null, "mind": null, "highlight": null, "grateful": null, "learned": null, "tasks": null }""")]
    public async Task Save_AnEmptyDocument_RemovesTheEntry(string emptyDocument)
    {
        var user = await NewUser();
        await Save(user, new { mood = 3, highlight = "something" });

        var response = await user.Client.SendRawAsync(HttpMethod.Put, $"/api/daily-diaries/{Day}", emptyDocument);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(user)).StatusCode);
        Assert.Empty((await (await user.Client.GetAsync("/api/daily-diaries")).ReadJsonAsync()).EnumerateArray()); // not counted in the calendar
    }

    [Fact]
    public async Task Save_AnEmptyDocumentForADayWithoutEntry_IsFine_AndStoresNothing()
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.NoContent, (await Save(user, new { })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Get(user)).StatusCode);
    }

    // ---- validation ----

    [Fact]
    public async Task Save_TheErrorSaysWhichEntryIsWrong()
    {
        var user = await NewUser();

        var response = await Save(user, new
        {
            grateful = new[] { "fine", "  " },
            learned = new[] { "", "fine" },
            tasks = new[] { new { title = "ok", done = false }, new { title = " ", done = true } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var keys = await response.ErrorKeysAsync();
        Assert.Contains("grateful[1]", keys);
        Assert.Contains("learned[0]", keys);
        Assert.Contains("tasks[1].title", keys); // every segment of a nested path is camelCased
        Assert.Equal(HttpStatusCode.NotFound, (await Get(user)).StatusCode); // nothing was stored
    }

    [Theory]
    [InlineData("""{ "mood": 0 }""", "mood")]
    [InlineData("""{ "mood": 6 }""", "mood")]
    [InlineData("""{ "body": -1 }""", "body")]
    [InlineData("""{ "body": 101 }""", "body")]
    [InlineData("""{ "mind": 101 }""", "mind")]
    public async Task Save_OutOfRangeNumbers_AreA400OnThatField(string json, string key)
    {
        var user = await NewUser();

        var response = await user.Client.SendRawAsync(HttpMethod.Put, $"/api/daily-diaries/{Day}", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(key, await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Save_TheLimits_AreAcceptedExactly_AndRefusedOneBeyond()
    {
        var user = await NewUser();
        string[] Items(int count, int length) => Enumerable.Range(0, count).Select(_ => new string('x', length)).ToArray();
        object[] Tasks(int count, int length) => Enumerable.Range(0, count).Select(_ => (object)new { title = new string('t', length), done = true }).ToArray();

        var atLimit = await Save(user, new { mood = 5, body = 100, mind = 0, highlight = new string('h', 5000), grateful = Items(30, 200), learned = Items(30, 200), tasks = Tasks(50, 200) });
        Assert.Equal(HttpStatusCode.NoContent, atLimit.StatusCode);

        var beyond = new[]
        {
            (new { highlight = new string('h', 5001) } as object, "highlight"),
            (new { grateful = Items(31, 1) }, "grateful"),
            (new { learned = Items(31, 1) }, "learned"),
            (new { grateful = Items(1, 201) }, "grateful[0]"),
            (new { tasks = Tasks(51, 1) }, "tasks"),
            (new { tasks = Tasks(1, 201) }, "tasks[0].title")
        };

        foreach (var (document, key) in beyond)
        {
            var response = await Save(user, document, "2026-10-09");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(key, await response.ErrorKeysAsync());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await Get(user, "2026-10-09")).StatusCode);
    }

    [Theory]
    [InlineData("""{ "mood": "great" }""")]
    [InlineData("""{ "body": "high" }""")]
    [InlineData("""{ "grateful": "not a list" }""")]
    [InlineData("""{ "grateful": [null] }""")]
    [InlineData("""{ "tasks": [null] }""")]
    [InlineData("""{ "tasks": [ { "title": 5, "done": true } ] }""")]
    [InlineData("{ broken")]
    public async Task Save_WrongTypesAndBrokenJson_AreA400_NotA500(string json)
    {
        var user = await NewUser();

        var response = await user.Client.SendRawAsync(HttpMethod.Put, $"/api/daily-diaries/{Day}", json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(user)).StatusCode);
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("tomorrow")]
    public async Task ABrokenDateInTheRoute_IsA400(string date)
    {
        var user = await NewUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await Save(user, new { mood = 3 }, date)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Get(user, date)).StatusCode);
    }

    // ---- delete ----

    [Fact]
    public async Task Delete_RemovesTheEntry_AndAnUnknownDayIsA404()
    {
        var user = await NewUser();
        await Save(user, new { mood = 3 });

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/daily-diaries/{Day}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(user)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"/api/daily-diaries/{Day}")).StatusCode);
    }

    // ---- calendar ----

    [Fact]
    public async Task Calendar_ListsTheDaysWithTheirMood_OldestFirst()
    {
        var user = await NewUser();
        await Save(user, new { mood = 5 }, "2026-10-09");
        await Save(user, new { highlight = "no mood" }, "2026-10-03");
        await Save(user, new { mood = 2 }, "2026-10-21");

        var body = await (await user.Client.GetAsync("/api/daily-diaries")).ReadJsonAsync();

        var days = body.EnumerateArray().ToArray();
        Assert.Equal(["2026-10-03", "2026-10-09", "2026-10-21"], days.Select(d => d.GetProperty("date").GetString()));
        Assert.Equal(JsonValueKind.Null, days[0].GetProperty("mood").ValueKind);
        Assert.Equal([5, 2], days.Skip(1).Select(d => d.GetProperty("mood").GetInt32()));
        Assert.Equal(2, days[0].EnumerateObject().Count()); // slim: only date and mood
    }

    [Fact]
    public async Task Calendar_TheRangeIsInclusive_AndEachEndIsOptional()
    {
        var user = await NewUser();
        foreach (var day in ApiExtensions.Days(2026, 10, 1, 8, 15, 22))
            await Save(user, new { mood = 3 }, day);

        async Task<string[]> Dates(string query) =>
            (await (await user.Client.GetAsync("/api/daily-diaries" + query)).ReadJsonAsync()).EnumerateArray().Select(d => d.GetProperty("date").GetString()!).ToArray();

        Assert.Equal(["2026-10-08", "2026-10-15"], await Dates("?from=2026-10-08&to=2026-10-15"));
        Assert.Equal(["2026-10-15", "2026-10-22"], await Dates("?from=2026-10-09"));
        Assert.Equal(["2026-10-01", "2026-10-08"], await Dates("?to=2026-10-14"));
        Assert.Equal(["2026-10-15"], await Dates("?from=2026-10-15&to=2026-10-15"));
        Assert.Empty(await Dates("?from=2027-01-01"));
    }

    [Fact]
    public async Task Calendar_ABackwardsRange_IsA400OnTo()
    {
        var user = await NewUser();

        var response = await user.Client.GetAsync("/api/daily-diaries?from=2026-10-08&to=2026-10-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("to", await response.ErrorKeysAsync());
    }

    // ---- data isolation ----

    [Fact]
    public async Task EachUserHasTheirOwnDiary_EvenForTheSameDay()
    {
        var alice = await NewUser();
        var bob = await NewUser();
        await Save(alice, new { mood = 5, highlight = "Alice's secret" });

        Assert.Equal(HttpStatusCode.NotFound, (await Get(bob)).StatusCode);
        Assert.Empty((await (await bob.Client.GetAsync("/api/daily-diaries")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.DeleteAsync($"/api/daily-diaries/{Day}")).StatusCode);

        // Bob writes the same day: Alice's entry must not change.
        await Save(bob, new { mood = 1, highlight = "Bob's" });
        Assert.Equal("Alice's secret", (await (await Get(alice)).ReadJsonAsync()).GetProperty("highlight").GetString());
        Assert.Equal("Bob's", (await (await Get(bob)).ReadJsonAsync()).GetProperty("highlight").GetString());

        // Bob's empty save removes Bob's entry only.
        await Save(bob, new { });
        Assert.Equal(HttpStatusCode.NotFound, (await Get(bob)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(alice)).StatusCode);
    }

    // ---- concurrency ----

    [Fact]
    public async Task TwelveParallelFirstSavesOfADay_AllSucceed_AndLeaveOneWholeDocument()
    {
        var user = await NewUser();

        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Save(user, new
        {
            mood = (i % 5) + 1, highlight = $"writer {i}", grateful = new[] { $"g{i}" }, tasks = new[] { new { title = $"t{i}", done = i % 2 == 0 } }
        })));

        Assert.True(responses.All(r => r.StatusCode == HttpStatusCode.NoContent), string.Join(Environment.NewLine, _api.Logs.Errors.Where(e => e.Contains("GlobalExceptionHandler")).Take(2)));

        // Last write wins - but as a whole: the fields all come from the same writer, never mixed.
        var body = await (await Get(user)).ReadJsonAsync();
        var writer = int.Parse(body.GetProperty("highlight").GetString()!.Split(' ')[1]);
        Assert.Equal((writer % 5) + 1, body.GetProperty("mood").GetInt32());
        Assert.Equal([$"g{writer}"], Strings(body.GetProperty("grateful")));
        Assert.Equal($"t{writer}", body.GetProperty("tasks")[0].GetProperty("title").GetString());
        Assert.Single((await (await user.Client.GetAsync("/api/daily-diaries")).ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task EightParallelDeletes_OneSucceeds_TheOthersAreToldNoOrRetry()
    {
        var user = await NewUser();
        await Save(user, new { mood = 3 });

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => user.Client.DeleteAsync($"/api/daily-diaries/{Day}")));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.NoContent),
            r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict }));
        Assert.Equal(HttpStatusCode.NotFound, (await Get(user)).StatusCode);
    }
}
