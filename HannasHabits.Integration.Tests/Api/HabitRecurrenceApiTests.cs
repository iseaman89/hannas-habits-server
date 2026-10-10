using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

/// <summary>
/// The recurrence rules over HTTP: the five kinds of rule go in, come back unchanged from every read, drive the
/// overview's <c>scheduledDates</c> and the streak, and a wrong fact is a field error at the right position.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class HabitRecurrenceApiTests
{
    private readonly ApiFactory _api;

    public HabitRecurrenceApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    // The rule as the API answers with it, flattened so that two of them compare with ==.
    public sealed record Rule(
        string Kind, string Weekdays, int Interval, string? Anchor, string? Occurrence, int? Weekday, int? DayOfMonth);

    private static Rule RuleOf(JsonElement recurrence)
    {
        static int? Number(JsonElement element) => element.ValueKind == JsonValueKind.Number ? element.GetInt32() : null;

        return new Rule(
            recurrence.GetProperty("kind").GetString()!,
            string.Join(",", recurrence.GetProperty("weekdays").EnumerateArray().Select(day => day.GetInt32())),
            recurrence.GetProperty("interval").GetInt32(),
            recurrence.GetProperty("anchor").GetString(),
            recurrence.GetProperty("occurrence").GetString(),
            Number(recurrence.GetProperty("weekday")),
            Number(recurrence.GetProperty("dayOfMonth")));
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string[] Oct(params int[] days) => ApiExtensions.Days(2026, 10, days);

    public static TheoryData<string, object, Rule> EveryKind => new()
    {
        {
            "weekly, every week",
            new { kind = "weekly", weekdays = new[] { 3, 1 } },
            new Rule("weekly", "1,3", 1, null, null, null, null)
        },
        {
            "weekly, every 2nd week, with its anchor",
            new { kind = "weekly", weekdays = new[] { 1, 4 }, interval = 2, anchor = "2026-10-07" },
            new Rule("weekly", "1,4", 2, "2026-10-07", null, null, null)
        },
        {
            "weekly, every 2nd week, without an anchor: counted from the start date",
            new { kind = "weekly", weekdays = new[] { 1 }, interval = 2 },
            new Rule("weekly", "1", 2, "2026-03-01", null, null, null)
        },
        {
            "every 3 days",
            new { kind = "everyNDays", interval = 3, anchor = "2026-10-10" },
            new Rule("everyNDays", "", 3, "2026-10-10", null, null, null)
        },
        {
            "every 3 days, without an anchor: counted from the start date",
            new { kind = "everyNDays", interval = 3 },
            new Rule("everyNDays", "", 3, "2026-03-01", null, null, null)
        },
        {
            "the last Saturday of the month",
            new { kind = "monthlyOnWeekday", occurrence = "last", weekday = 6 },
            new Rule("monthlyOnWeekday", "", 1, null, "last", 6, null)
        },
        {
            "the second Monday of the month",
            new { kind = "monthlyOnWeekday", occurrence = "second", weekday = 1 },
            new Rule("monthlyOnWeekday", "", 1, null, "second", 1, null)
        },
        {
            "the 15th of the month",
            new { kind = "monthlyOnDate", dayOfMonth = 15 },
            new Rule("monthlyOnDate", "", 1, null, null, null, 15)
        },
        {
            "the 31st of the month",
            new { kind = "monthlyOnDate", dayOfMonth = 31 },
            new Rule("monthlyOnDate", "", 1, null, null, null, 31)
        }
    };

    // ---- create, and every way to read it back ----

    [Theory]
    [MemberData(nameof(EveryKind))]
    public async Task Create_TheRuleComesBackUnchanged_FromEveryRead(
        string description, object recurrence, Rule expected)
    {
        var user = await NewUser();

        var created = await user.Client.PostAsync(
            "/api/habits", new { title = description, startDate = "2026-03-01", recurrence });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(expected, RuleOf(body.GetProperty("recurrence")));

        var details = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal(expected, RuleOf(details.GetProperty("recurrence")));

        var list = await (await user.Client.GetAsync("/api/habits")).ReadJsonAsync();
        var listed = Assert.Single(list.EnumerateArray());
        Assert.Equal(expected, RuleOf(listed.GetProperty("recurrence")));

        var overview = await user.OverviewAsync("2026-03-01", "2026-03-31", asOf: "2026-03-31");
        Assert.Equal(expected, RuleOf(Assert.Single(overview.EnumerateArray()).GetProperty("recurrence")));
    }

    [Fact]
    public async Task TheKindAndTheOccurrence_AreWords_TheWeekdaysAreNumbers()
    {
        var user = await NewUser();

        var created = await user.Client.PostAsync(
            "/api/habits", new { title = "Walk", recurrence = new { kind = "monthlyOnWeekday", occurrence = "fourth", weekday = 0 } });
        var raw = await created.Content.ReadAsStringAsync();

        Assert.Contains("\"kind\":\"monthlyOnWeekday\"", raw);
        Assert.Contains("\"occurrence\":\"fourth\"", raw);
        Assert.Contains("\"weekday\":0", raw);
    }

    [Fact]
    public async Task WithoutARecurrenceTheHabitIsDueEveryDay_AsWeekly_EveryWeek()
    {
        var user = await NewUser();

        var created = await (await user.Client.PostAsync("/api/habits", new { title = "Read" })).ReadJsonAsync();

        Assert.Equal(
            new Rule("weekly", "0,1,2,3,4,5,6", 1, null, null, null, null),
            RuleOf(created.GetProperty("recurrence")));
    }

    // The weekday list `schedule` of B2..B13 is gone (B13b): no answer carries it, and a client that still sends it
    // does not get a rule out of it - on create the habit is due every day, an update is refused like one without a rule.

    [Fact]
    public async Task TheDroppedSchedule_IsNotInAnyAnswer()
    {
        var user = await NewUser();
        var created = await (await user.Client.PostAsync("/api/habits", new { title = "Read", startDate = "2026-10-01" })).ReadJsonAsync();
        var id = created.GetProperty("id").GetGuid();

        var details = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        var list = await (await user.Client.GetAsync("/api/habits")).ReadJsonAsync();
        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-31");

        Assert.False(created.TryGetProperty("schedule", out _));
        Assert.False(details.TryGetProperty("schedule", out _));
        Assert.False(Assert.Single(list.EnumerateArray()).TryGetProperty("schedule", out _));
        Assert.False(Assert.Single(overview.EnumerateArray()).TryGetProperty("schedule", out _));
    }

    [Fact]
    public async Task ASchedule_SentByAnOldClient_IsNotARule_OnCreate()
    {
        var user = await NewUser();

        var created = await user.Client.PostAsync("/api/habits", new { title = "Read", schedule = new[] { 5, 1 } });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(
            new Rule("weekly", "0,1,2,3,4,5,6", 1, null, null, null, null),
            RuleOf((await created.ReadJsonAsync()).GetProperty("recurrence")));
    }

    [Fact]
    public async Task ASchedule_SentByAnOldClient_IsNotARule_OnUpdate()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", recurrence: new { kind = "monthlyOnDate", dayOfMonth = 15 });

        var response = await user.Client.PutAsync($"/api/habits/{id}", new { title = "Changed", schedule = new[] { 5, 1 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("recurrence", await response.ErrorKeysAsync());
        var details = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal("Read", details.GetProperty("title").GetString());
        Assert.Equal("monthlyOnDate", details.GetProperty("recurrence").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task AFactThatDoesNotBelongToTheKind_IsIgnored()
    {
        var user = await NewUser();

        var created = await user.Client.PostAsync("/api/habits", new
        {
            title = "Pay rent",
            recurrence = new { kind = "monthlyOnDate", dayOfMonth = 1, weekdays = new[] { 1, 2 }, interval = 7, anchor = "2026-10-10", occurrence = "last", weekday = 3 }
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(
            new Rule("monthlyOnDate", "", 1, null, null, null, 1),
            RuleOf((await created.ReadJsonAsync()).GetProperty("recurrence")));
    }

    // ---- update ----

    [Fact]
    public async Task Update_TheRuleIsReplaced_AndNothingOfTheOldKindIsLeft()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", startDate: "2026-10-01",
            recurrence: new { kind = "everyNDays", interval = 3, anchor = "2026-10-10" });

        var update = await user.Client.PutAsync($"/api/habits/{id}", new
        {
            title = "Read", recurrence = new { kind = "monthlyOnWeekday", occurrence = "first", weekday = 1 }
        });

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var details = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal(new Rule("monthlyOnWeekday", "", 1, null, "first", 1, null), RuleOf(details.GetProperty("recurrence")));
    }

    [Fact]
    public async Task Update_MovingTheStartDate_DoesNotShiftTheRhythm_WhenTheAnchorIsSentBack()
    {
        var user = await NewUser();
        var recurrence = new { kind = "weekly", weekdays = new[] { 1, 4 }, interval = 2, anchor = "2026-10-07" };
        var id = await user.CreateHabitAsync("Gym", startDate: "2026-10-01", recurrence: recurrence);
        var before = Strings((await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-31")).EnumerateArray().Single().GetProperty("scheduledDates"));

        var update = await user.Client.PutAsync($"/api/habits/{id}", new { title = "Gym", startDate = "2026-09-01", recurrence });

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var after = (await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-31")).EnumerateArray().Single();
        Assert.Equal("2026-10-07", after.GetProperty("recurrence").GetProperty("anchor").GetString());
        Assert.Equal(before, Strings(after.GetProperty("scheduledDates")));
        Assert.Equal(Oct(5, 8, 19, 22), before);
    }

    [Theory]
    [InlineData("2026-09-01", "2026-09-01")]   // a new start date: the rule counts from it
    [InlineData(null, "2026-10-01")]           // none sent: the start date the habit has
    public async Task Update_WithoutAnAnchor_TheRuleCountsFromTheStartDateOfTheSavedHabit(string? sentStartDate, string expectedAnchor)
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Water plants", startDate: "2026-10-01");

        var update = await user.Client.PutAsync($"/api/habits/{id}", new
        {
            title = "Water plants", startDate = sentStartDate, recurrence = new { kind = "everyNDays", interval = 4 }
        });

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var details = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal(expectedAnchor, details.GetProperty("recurrence").GetProperty("anchor").GetString());
    }

    [Fact]
    public async Task Update_WithoutARecurrenceIsA400_OnTheRecurrence()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read");

        var response = await user.Client.PutAsync($"/api/habits/{id}", new { title = "Read" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("recurrence", await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Update_ARejectedRule_ChangesNothing()
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read", recurrence: new { kind = "monthlyOnDate", dayOfMonth = 15 });

        var response = await user.Client.PutAsync($"/api/habits/{id}", new
        {
            title = "Changed", recurrence = new { kind = "everyNDays", interval = 1 }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await (await user.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal("Read", details.GetProperty("title").GetString());
        Assert.Equal("monthlyOnDate", details.GetProperty("recurrence").GetProperty("kind").GetString());
    }

    // ---- what is refused, and where the error is ----

    public static TheoryData<string, object, string> Refused => new()
    {
        { "no kind", new { weekdays = new[] { 1 } }, "recurrence.kind" },
        { "an unknown kind number", new { kind = 9, weekdays = new[] { 1 } }, "recurrence.kind" },

        { "weekly without days", new { kind = "weekly" }, "recurrence.weekdays" },
        { "weekly with no days", new { kind = "weekly", weekdays = Array.Empty<int>() }, "recurrence.weekdays" },
        { "weekly with day 7", new { kind = "weekly", weekdays = new[] { 1, 7 } }, "recurrence.weekdays[1]" },
        { "weekly every 0 weeks", new { kind = "weekly", weekdays = new[] { 1 }, interval = 0 }, "recurrence.interval" },
        { "weekly every 53 weeks", new { kind = "weekly", weekdays = new[] { 1 }, interval = 53, anchor = "2026-10-05" }, "recurrence.interval" },

        { "every N days without N", new { kind = "everyNDays" }, "recurrence.interval" },
        { "every 1 day (that is daily)", new { kind = "everyNDays", interval = 1 }, "recurrence.interval" },
        { "every 366 days", new { kind = "everyNDays", interval = 366 }, "recurrence.interval" },
        { "an anchor before 2000", new { kind = "everyNDays", interval = 3, anchor = "1999-12-31" }, "recurrence.anchor" },
        { "an anchor after 2100", new { kind = "weekly", weekdays = new[] { 1 }, interval = 2, anchor = "2101-01-01" }, "recurrence.anchor" },

        { "monthly on a weekday without the occurrence", new { kind = "monthlyOnWeekday", weekday = 1 }, "recurrence.occurrence" },
        { "monthly on a weekday: occurrence 0", new { kind = "monthlyOnWeekday", occurrence = 0, weekday = 1 }, "recurrence.occurrence" },
        { "monthly on a weekday: a sixth", new { kind = "monthlyOnWeekday", occurrence = 6, weekday = 1 }, "recurrence.occurrence" },
        { "monthly on a weekday without the weekday", new { kind = "monthlyOnWeekday", occurrence = "last" }, "recurrence.weekday" },
        { "monthly on a weekday: weekday 7", new { kind = "monthlyOnWeekday", occurrence = "last", weekday = 7 }, "recurrence.weekday" },

        { "monthly on a date without the date", new { kind = "monthlyOnDate" }, "recurrence.dayOfMonth" },
        { "the 0th", new { kind = "monthlyOnDate", dayOfMonth = 0 }, "recurrence.dayOfMonth" },
        { "the 32nd", new { kind = "monthlyOnDate", dayOfMonth = 32 }, "recurrence.dayOfMonth" }
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task Create_ABadRule_IsA400_AtTheRightPosition(string description, object recurrence, string errorKey)
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/habits", new { title = description, recurrence });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errorKey, await response.ErrorKeysAsync());
        Assert.Empty((await (await user.Client.GetAsync("/api/habits")).ReadJsonAsync()).EnumerateArray()); // nothing was stored
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task Update_ABadRule_IsA400_AtTheRightPosition(string description, object recurrence, string errorKey)
    {
        var user = await NewUser();
        var id = await user.CreateHabitAsync("Read");

        var response = await user.Client.PutAsync($"/api/habits/{id}", new { title = description, recurrence });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(errorKey, await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task AKindThatIsNoWord_IsA400_NotA500()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync("/api/habits", new { title = "Read", recurrence = new { kind = "yearly" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- the overview: due days and the streak follow the rule ----

    [Fact]
    public async Task Overview_ScheduledDates_AreTheDaysTheRuleIsDueOn_WithinTheRange()
    {
        var user = await NewUser();
        await user.CreateHabitAsync("Every week", startDate: "2026-10-01", recurrence: new { kind = "weekly", weekdays = new[] { 1, 4 } });
        await user.CreateHabitAsync("Every 2nd week", startDate: "2026-10-01", recurrence: new { kind = "weekly", weekdays = new[] { 1, 4 }, interval = 2, anchor = "2026-10-07" });
        await user.CreateHabitAsync("Every 3 days", startDate: "2026-10-01", recurrence: new { kind = "everyNDays", interval = 3, anchor = "2026-10-10" });
        await user.CreateHabitAsync("Last Saturday", startDate: "2026-10-01", recurrence: new { kind = "monthlyOnWeekday", occurrence = "last", weekday = 6 });
        await user.CreateHabitAsync("The 15th", startDate: "2026-10-01", recurrence: new { kind = "monthlyOnDate", dayOfMonth = 15 });

        var overview = await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-31");

        var dates = overview.EnumerateArray().ToDictionary(
            item => item.GetProperty("title").GetString()!, item => Strings(item.GetProperty("scheduledDates")));
        Assert.Equal(Oct(1, 5, 8, 12, 15, 19, 22, 26, 29), dates["Every week"]);
        Assert.Equal(Oct(5, 8, 19, 22), dates["Every 2nd week"]);
        Assert.Equal(Oct(1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31), dates["Every 3 days"]);
        Assert.Equal(Oct(31), dates["Last Saturday"]);
        Assert.Equal(Oct(15), dates["The 15th"]);
    }

    [Fact]
    public async Task Overview_ScheduledDates_NeverReachBeforeTheStartDate_AndStayInsideTheRange()
    {
        var user = await NewUser();
        await user.CreateHabitAsync("Mondays", startDate: "2026-10-20", recurrence: new { kind = "weekly", weekdays = new[] { 1 } });

        var inMonth = (await user.OverviewAsync("2026-10-01", "2026-10-31", "2026-10-31")).EnumerateArray().Single();
        var beforeStart = (await user.OverviewAsync("2026-10-01", "2026-10-19", "2026-10-31")).EnumerateArray().Single();
        var oneDay = (await user.OverviewAsync("2026-10-26", "2026-10-26", "2026-10-31")).EnumerateArray().Single();

        Assert.Equal(Oct(26), Strings(inMonth.GetProperty("scheduledDates")));
        Assert.Empty(beforeStart.GetProperty("scheduledDates").EnumerateArray());
        Assert.Equal(Oct(26), Strings(oneDay.GetProperty("scheduledDates")));
    }

    [Fact]
    public async Task Overview_TheStreakFollowsTheRule()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Water plants", startDate: "2026-10-01",
            recurrence: new { kind = "everyNDays", interval = 3, anchor = "2026-10-10" });
        await user.MarkAsync(habit, Oct(1, 4, 7, 10, 13)); // exactly the due days; 14 is not due

        var item = (await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-14")).EnumerateArray().Single();

        Assert.Equal(5, item.GetProperty("currentStreak").GetInt32());

        await user.Client.DeleteAsync($"/api/habits/{habit}/records/2026-10-07");
        var broken = (await user.OverviewAsync("2026-10-01", "2026-10-31", asOf: "2026-10-14")).EnumerateArray().Single();
        Assert.Equal(2, broken.GetProperty("currentStreak").GetInt32()); // 13 and 10, then the missed 7th
    }

    // ---- isolation ----

    [Fact]
    public async Task AnotherUser_CannotChangeTheRuleOfSomeoneElsesHabit()
    {
        var owner = await NewUser();
        var intruder = await NewUser();
        var id = await owner.CreateHabitAsync("Read", recurrence: new { kind = "monthlyOnDate", dayOfMonth = 15 });

        var response = await intruder.Client.PutAsync($"/api/habits/{id}", new
        {
            title = "Hacked", recurrence = new { kind = "everyNDays", interval = 2 }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var details = await (await owner.Client.GetAsync($"/api/habits/{id}")).ReadJsonAsync();
        Assert.Equal("monthlyOnDate", details.GetProperty("recurrence").GetProperty("kind").GetString());
    }
}
