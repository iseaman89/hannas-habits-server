using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class SubHabitsApiTests
{
    // October 2026: the 5th is a Monday, the 7th a Wednesday, the 9th a Friday.
    private const string Monday = "2026-10-05";
    private const string Wednesday = "2026-10-07";
    private const string Friday = "2026-10-09";
    private const string NextMonday = "2026-10-12";

    private const int Sunday = 0, MondayDay = 1, TuesdayDay = 2, WednesdayDay = 3, FridayDay = 5;

    private readonly ApiFactory _api;

    public SubHabitsApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    /// <summary>Sport, due on Mondays, Wednesdays and Fridays since September.</summary>
    private static Task<Guid> NewSport(TestUser user)
        => user.CreateHabitAsync("Sport", weekdays: [MondayDay, WednesdayDay, FridayDay], startDate: "2026-09-01");

    private static string SubHabits(Guid habitId) => $"/api/habits/{habitId}/sub-habits";

    private static string Tick(Guid habitId, Guid subHabitId, string date) => $"{SubHabits(habitId)}/{subHabitId}/records/{date}";

    private static JsonElement ItemOf(JsonElement overview, Guid habitId)
        => overview.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == habitId);

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static int[] Numbers(JsonElement array) => array.EnumerateArray().Select(e => e.GetInt32()).ToArray();

    private static Guid[] Ids(JsonElement array) => array.EnumerateArray().Select(e => e.GetGuid()).ToArray();

    // ---- add ----

    [Fact]
    public async Task Add_Answers201WithThePart_AndTheHabitAsLocation()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var response = await user.Client.PostAsync(SubHabits(habit), new { title = "  Legs ", weekdays = new[] { FridayDay, MondayDay, MondayDay } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/api/habits/{habit}", response.Headers.Location!.ToString());
        var body = await response.ReadJsonAsync();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("Legs", body.GetProperty("title").GetString());
        Assert.Equal([MondayDay, FridayDay], Numbers(body.GetProperty("weekdays"))); // sorted, duplicates collapsed
    }

    [Fact]
    public async Task Add_ThePartShowsUpInTheHabitsDetails_InTheOrderTheyWereAdded()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        var arms = await user.AddSubHabitAsync(habit, "Arms", WednesdayDay);

        var details = await (await user.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();

        var parts = details.GetProperty("subHabits").EnumerateArray().ToArray();
        Assert.Equal([legs, arms], parts.Select(p => p.GetProperty("id").GetGuid()));
        Assert.Equal(["Legs", "Arms"], parts.Select(p => p.GetProperty("title").GetString()));
        Assert.Equal([MondayDay], Numbers(parts[0].GetProperty("weekdays")));
    }

    [Theory]
    [InlineData("""{ "title": "", "weekdays": [1] }""", "title")]
    [InlineData("""{ "title": "   ", "weekdays": [1] }""", "title")]
    [InlineData("""{ "weekdays": [1] }""", "title")]
    [InlineData("""{ "title": "Legs", "weekdays": [] }""", "weekdays")]
    [InlineData("""{ "title": "Legs" }""", "weekdays")]
    [InlineData("""{ "title": "Legs", "weekdays": [1, 7] }""", "weekdays[1]")]
    public async Task Add_AnInvalidBody_IsA400OnTheField_AndNothingIsStored(string json, string key)
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var response = await user.Client.SendRawAsync(HttpMethod.Post, SubHabits(habit), json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(key, await response.ErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
        var details = await (await user.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();
        Assert.Equal(0, details.GetProperty("subHabits").GetArrayLength());
    }

    [Fact]
    public async Task Add_ADayTheHabitIsNotPlannedOn_IsA400_WithTheReason()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var response = await user.Client.PostAsync(SubHabits(habit), new { title = "Legs", weekdays = new[] { TuesdayDay } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("planned on", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Add_AHabitWithoutFixedWeekdays_AcceptsAnyDay()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Run", recurrence: new { kind = "timesPerWeek", times = 3 });

        var response = await user.Client.PostAsync(SubHabits(habit), new { title = "Long run", weekdays = new[] { Sunday } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Add_ATitleTheHabitAlreadyHas_IgnoringCase_IsA400()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await user.Client.PostAsync(SubHabits(habit), new { title = "LEGS", weekdays = new[] { WednesdayDay } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already has a sub-habit", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Add_TwoHabitsMayHaveTheSameTitle()
    {
        var user = await NewUser();
        var sport = await NewSport(user);
        var gym = await user.CreateHabitAsync("Gym");

        await user.AddSubHabitAsync(sport, "Legs", MondayDay);
        await user.AddSubHabitAsync(gym, "Legs", MondayDay);
    }

    [Fact]
    public async Task Add_AnEleventhPart_IsA400_AndTheTenStay()
    {
        var user = await NewUser();
        var habit = await user.CreateHabitAsync("Sport"); // every day

        for (var i = 0; i < 10; i++)
            await user.AddSubHabitAsync(habit, $"Part {i}", MondayDay);

        var response = await user.Client.PostAsync(SubHabits(habit), new { title = "One more", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("10", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
        var details = await (await user.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();
        Assert.Equal(10, details.GetProperty("subHabits").GetArrayLength());
    }

    [Fact]
    public async Task Add_ToAnUnknownHabit_Is404()
    {
        var user = await NewUser();

        var response = await user.Client.PostAsync(SubHabits(Guid.NewGuid()), new { title = "Legs", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Add_ToAnotherUsersHabit_Is404_AndTheirHabitStaysAsItWas()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var habit = await NewSport(owner);

        var response = await stranger.Client.PostAsync(SubHabits(habit), new { title = "Legs", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var details = await (await owner.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();
        Assert.Equal(0, details.GetProperty("subHabits").GetArrayLength());
    }

    // ---- update ----

    [Fact]
    public async Task Update_RenamesAndMovesThePart_ItsTicksStay()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.TickSubHabitAsync(habit, Monday, legs);

        var response = await user.Client.PutAsync($"{SubHabits(habit)}/{legs}", new { title = "Legs & glutes", weekdays = new[] { MondayDay, FridayDay } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(legs, body.GetProperty("id").GetGuid());
        Assert.Equal("Legs & glutes", body.GetProperty("title").GetString());
        Assert.Equal([MondayDay, FridayDay], Numbers(body.GetProperty("weekdays")));

        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);
        Assert.Equal([Monday], Strings(item.GetProperty("subHabits")[0].GetProperty("completedDates")));
    }

    [Fact]
    public async Task Update_ATitleOfAnotherPart_IsA400_AndNothingChanges()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.AddSubHabitAsync(habit, "Arms", WednesdayDay);

        var response = await user.Client.PutAsync($"{SubHabits(habit)}/{legs}", new { title = "arms", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = await (await user.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();
        Assert.Equal("Legs", details.GetProperty("subHabits")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Update_AnUnknownPart_Is404()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var response = await user.Client.PutAsync($"{SubHabits(habit)}/{Guid.NewGuid()}", new { title = "Legs", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_APartOfAnotherUser_Is404_AndStaysAsItWas()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var habit = await NewSport(owner);
        var legs = await owner.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await stranger.Client.PutAsync($"{SubHabits(habit)}/{legs}", new { title = "Mine", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var details = await (await owner.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();
        Assert.Equal("Legs", details.GetProperty("subHabits")[0].GetProperty("title").GetString());
    }

    // ---- remove ----

    [Fact]
    public async Task Remove_TakesThePartWithItsTicks_AndLeavesTheHabitsDays()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.TickSubHabitAsync(habit, Monday, legs); // completes Monday

        var response = await user.Client.DeleteAsync($"{SubHabits(habit)}/{legs}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);
        Assert.Equal(0, item.GetProperty("subHabits").GetArrayLength());
        Assert.Equal([Monday], Strings(item.GetProperty("completedDates")));
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.DeleteAsync($"{SubHabits(habit)}/{legs}")).StatusCode);
    }

    [Fact]
    public async Task Remove_APartOfAnotherUser_Is404_AndStays()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var habit = await NewSport(owner);
        var legs = await owner.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await stranger.Client.DeleteAsync($"{SubHabits(habit)}/{legs}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var details = await (await owner.Client.GetAsync($"/api/habits/{habit}")).ReadJsonAsync();
        Assert.Equal(1, details.GetProperty("subHabits").GetArrayLength());
    }

    [Fact]
    public async Task DeletingTheHabit_TakesItsPartsAndTheirTicksWithIt()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.TickSubHabitAsync(habit, Monday, legs);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PutAsync(Tick(habit, legs, NextMonday))).StatusCode);
    }

    // ---- tick ----

    [Fact]
    public async Task Tick_TheOnlyDuePart_CompletesTheHabitsDay()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await user.Client.PutAsync(Tick(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var day = await response.ReadJsonAsync();
        Assert.Equal(Monday, day.GetProperty("date").GetString());
        Assert.True(day.GetProperty("habitCompleted").GetBoolean());
        Assert.Equal([legs], Ids(day.GetProperty("completedSubHabitIds")));
        Assert.Equal([Monday], await user.RecordDatesAsync(habit)); // the habit's own record exists
    }

    [Fact]
    public async Task Tick_OneOfTwoDueParts_LeavesTheDayOpen_TheSecondCompletesIt()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        var core = await user.AddSubHabitAsync(habit, "Core", MondayDay);

        var first = await user.TickSubHabitAsync(habit, Monday, legs);
        Assert.False(first.GetProperty("habitCompleted").GetBoolean());
        Assert.Empty(await user.RecordDatesAsync(habit));

        var second = await user.TickSubHabitAsync(habit, Monday, core);
        Assert.True(second.GetProperty("habitCompleted").GetBoolean());
        Assert.Equal([legs, core], Ids(second.GetProperty("completedSubHabitIds")));
        Assert.Equal([Monday], await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Tick_IsIdempotent()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        await user.TickSubHabitAsync(habit, Monday, legs);
        var again = await user.TickSubHabitAsync(habit, Monday, legs);

        Assert.True(again.GetProperty("habitCompleted").GetBoolean());
        Assert.Equal([legs], Ids(again.GetProperty("completedSubHabitIds")));
        Assert.Single(await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Tick_ADayThePartIsNotDueOn_IsOnlyAMark_TheHabitStaysOpen()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        var day = await user.TickSubHabitAsync(habit, Wednesday, legs);

        Assert.False(day.GetProperty("habitCompleted").GetBoolean());
        Assert.Equal([legs], Ids(day.GetProperty("completedSubHabitIds")));
        Assert.Empty(await user.RecordDatesAsync(habit));
    }

    [Theory]
    [InlineData("1999-12-31")]
    [InlineData("2101-01-01")]
    public async Task Tick_ADayOutsideThePlausibleRange_IsA400(string date)
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await user.Client.PutAsync(Tick(habit, legs, date));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("date", await response.ErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tick_AnUnknownPart_Is404()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.PutAsync(Tick(habit, Guid.NewGuid(), Monday))).StatusCode);
    }

    [Fact]
    public async Task Tick_APartOfAnotherUser_Is404_AndNothingIsTicked()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var habit = await NewSport(owner);
        var legs = await owner.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await stranger.Client.PutAsync(Tick(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await owner.RecordDatesAsync(habit));
        var item = ItemOf(await owner.OverviewAsync("2026-10-01", "2026-10-31"), habit);
        Assert.Empty(Strings(item.GetProperty("subHabits")[0].GetProperty("completedDates")));
    }

    // ---- untick ----

    [Fact]
    public async Task Untick_TakesThePartBack_AndTheHabitsDayWithIt()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.TickSubHabitAsync(habit, Monday, legs);

        var response = await user.Client.DeleteAsync(Tick(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var day = await response.ReadJsonAsync();
        Assert.False(day.GetProperty("habitCompleted").GetBoolean());
        Assert.Empty(Ids(day.GetProperty("completedSubHabitIds")));
        Assert.Empty(await user.RecordDatesAsync(habit));
    }

    [Fact]
    public async Task Untick_IsIdempotent_APartThatIsNotDoneIsNoError()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        var response = await user.Client.DeleteAsync(Tick(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadJsonAsync()).GetProperty("habitCompleted").GetBoolean());
    }

    [Fact]
    public async Task Untick_APartOfAnotherUser_Is404_AndKeepsTheTick()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var habit = await NewSport(owner);
        var legs = await owner.AddSubHabitAsync(habit, "Legs", MondayDay);
        await owner.TickSubHabitAsync(habit, Monday, legs);

        var response = await stranger.Client.DeleteAsync(Tick(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([Monday], await owner.RecordDatesAsync(habit));
    }

    // ---- the habit's own tick ----

    [Fact]
    public async Task TickingTheHabit_TicksTheDuePartsOfThatDay()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        var core = await user.AddSubHabitAsync(habit, "Core", MondayDay, FridayDay);
        var arms = await user.AddSubHabitAsync(habit, "Arms", WednesdayDay);

        await user.MarkAsync(habit, Monday);

        var parts = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit).GetProperty("subHabits");
        Assert.Equal([Monday], Strings(parts[0].GetProperty("completedDates"))); // Legs
        Assert.Equal([Monday], Strings(parts[1].GetProperty("completedDates"))); // Core
        Assert.Empty(Strings(parts[2].GetProperty("completedDates")));           // Arms is not due on a Monday
        Assert.Equal(legs, parts[0].GetProperty("id").GetGuid());
        Assert.Equal(core, parts[1].GetProperty("id").GetGuid());
        Assert.Equal(arms, parts[2].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task UntickingTheHabit_ClearsThePartsOfThatDay_AndOnlyThatDay()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.MarkAsync(habit, Monday, NextMonday);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}/records/{Monday}")).StatusCode);

        var parts = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit).GetProperty("subHabits");
        Assert.Equal([NextMonday], Strings(parts[0].GetProperty("completedDates")));
    }

    // ---- overview ----

    [Fact]
    public async Task Overview_CarriesEachPartWithItsDueAndItsDoneDays()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        var core = await user.AddSubHabitAsync(habit, "Core", MondayDay, FridayDay);
        await user.TickSubHabitAsync(habit, Monday, legs, core);
        await user.TickSubHabitAsync(habit, Friday, core);

        var item = ItemOf(await user.OverviewAsync("2026-10-05", "2026-10-11"), habit);

        var parts = item.GetProperty("subHabits").EnumerateArray().ToArray();
        Assert.Equal("Legs", parts[0].GetProperty("title").GetString());
        Assert.Equal([MondayDay], Numbers(parts[0].GetProperty("weekdays")));
        Assert.Equal([Monday], Strings(parts[0].GetProperty("scheduledDates")));
        Assert.Equal([Monday], Strings(parts[0].GetProperty("completedDates")));
        Assert.Equal([Monday, Friday], Strings(parts[1].GetProperty("scheduledDates")));
        Assert.Equal([Monday, Friday], Strings(parts[1].GetProperty("completedDates")));
        Assert.Equal([Monday, Friday], Strings(item.GetProperty("completedDates"))); // both days are the habit's own now
    }

    [Fact]
    public async Task Overview_TheRangeNarrowsThePartsDays_LikeItDoesTheHabits()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        var legs = await user.AddSubHabitAsync(habit, "Legs", MondayDay);
        await user.TickSubHabitAsync(habit, Monday, legs);
        await user.TickSubHabitAsync(habit, NextMonday, legs);

        var item = ItemOf(await user.OverviewAsync("2026-10-06", "2026-10-31"), habit);

        var part = item.GetProperty("subHabits")[0];
        Assert.Equal([NextMonday, "2026-10-19", "2026-10-26"], Strings(part.GetProperty("scheduledDates")));
        Assert.Equal([NextMonday], Strings(part.GetProperty("completedDates")));
    }

    [Fact]
    public async Task Overview_AHabitWithoutParts_HasAnEmptyList()
    {
        var user = await NewUser();
        var habit = await NewSport(user);

        var item = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit);

        Assert.Equal(0, item.GetProperty("subHabits").GetArrayLength());
    }

    [Fact]
    public async Task Overview_NeverShowsThePartsOfAnotherUser()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var habit = await NewSport(owner);
        await owner.AddSubHabitAsync(habit, "Legs", MondayDay);
        var strangersHabit = await NewSport(stranger);

        var overview = await stranger.OverviewAsync("2026-10-01", "2026-10-31");

        Assert.Single(overview.EnumerateArray());
        Assert.Equal(0, ItemOf(overview, strangersHabit).GetProperty("subHabits").GetArrayLength());
    }

    // ---- rhythm ----

    [Fact]
    public async Task APartFollowsTheHabitsRhythm_WhenTheHabitIsEdited()
    {
        var user = await NewUser();
        var habit = await NewSport(user);
        await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        // From now on Sport is due on Mondays every second week, counted from 5 October.
        var update = await user.Client.PutAsync($"/api/habits/{habit}", new
        {
            title = "Sport",
            startDate = "2026-09-01",
            recurrence = new { kind = "weekly", weekdays = new[] { MondayDay }, interval = 2, anchor = Monday }
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var part = ItemOf(await user.OverviewAsync("2026-10-01", "2026-10-31"), habit).GetProperty("subHabits")[0];
        Assert.Equal([Monday, "2026-10-19"], Strings(part.GetProperty("scheduledDates")));
    }
}
