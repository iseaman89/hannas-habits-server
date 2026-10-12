using System.Net;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class SubHabitNotesApiTests
{
    // October 2026: the 5th is a Monday, the 7th a Wednesday, the 9th a Friday.
    private const string Monday = "2026-10-05";
    private const string Tuesday = "2026-10-06";
    private const string Wednesday = "2026-10-07";
    private const string NextMonday = "2026-10-12";
    private const string LastMonthsMonday = "2026-09-28";

    private const int MondayDay = 1, WednesdayDay = 3, FridayDay = 5;

    private readonly ApiFactory _api;

    public SubHabitNotesApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private Task<TestUser> NewUser() => TestUser.RegisterAsync(_api);

    /// <summary>Sport, due on Mondays, Wednesdays and Fridays since September, with "Legs" on Mondays.</summary>
    private static async Task<(Guid Habit, Guid Legs)> SportWithLegs(TestUser user)
    {
        var habit = await user.CreateHabitAsync("Sport", weekdays: [MondayDay, WednesdayDay, FridayDay], startDate: "2026-09-01");
        return (habit, await user.AddSubHabitAsync(habit, "Legs", MondayDay));
    }

    private static string NoteUrl(Guid habit, Guid part, string date) => $"/api/habits/{habit}/sub-habits/{part}/notes/{date}";

    private static async Task<JsonElement[]> NotesOfAsync(TestUser user, Guid habit, Guid part, string from = "2026-10-01", string to = "2026-10-31")
    {
        var overview = await user.OverviewAsync(from, to);
        var item = overview.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == habit);
        var subHabit = item.GetProperty("subHabits").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == part);

        return subHabit.GetProperty("notes").EnumerateArray().ToArray();
    }

    private static string[] Texts(IEnumerable<JsonElement> notes) => notes.Select(n => n.GetProperty("text").GetString()!).ToArray();

    private static string[] Dates(IEnumerable<JsonElement> notes) => notes.Select(n => n.GetProperty("date").GetString()!).ToArray();

    // ---- write ----

    [Fact]
    public async Task Set_Answers200WithTheNote_AndTrimsTheEnds()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        var response = await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "  3 x 10\nleft knee odd \n" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(Monday, body.GetProperty("date").GetString());
        Assert.Equal("3 x 10\nleft knee odd", body.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Set_TheNoteShowsUpOnItsPartInTheOverview_OldestFirst_OnlyWithinTheRange()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        var arms = await user.AddSubHabitAsync(habit, "Arms", WednesdayDay);
        await user.Client.PutAsync(NoteUrl(habit, legs, NextMonday), new { text = "second" });
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "first" });
        await user.Client.PutAsync(NoteUrl(habit, legs, LastMonthsMonday), new { text = "out of range" });
        await user.Client.PutAsync(NoteUrl(habit, arms, Wednesday), new { text = "arms only" });

        var legsNotes = await NotesOfAsync(user, habit, legs);
        var armsNotes = await NotesOfAsync(user, habit, arms);

        Assert.Equal([Monday, NextMonday], Dates(legsNotes));
        Assert.Equal(["first", "second"], Texts(legsNotes));
        Assert.Equal(["arms only"], Texts(armsNotes));
    }

    [Fact]
    public async Task Set_ASecondWrite_ReplacesTheText_AndStaysOneNote()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "first" });

        var response = await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "second" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["second"], Texts(await NotesOfAsync(user, habit, legs)));
    }

    [Fact]
    public async Task Set_IsIndependentOfTheTick_AMissedOrNotDueDayCanCarryANote()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        // Monday is due and not done; Tuesday is not a day of the part at all.
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "skipped: ill" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsync(NoteUrl(habit, legs, Tuesday), new { text = "stretched" })).StatusCode);

        Assert.Equal(["skipped: ill", "stretched"], Texts(await NotesOfAsync(user, habit, legs)));
    }

    [Fact]
    public async Task TheTick_AndTakingItBack_DoNotTouchTheNote()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "kept" });

        await user.TickSubHabitAsync(habit, Monday, legs);
        Assert.Equal(["kept"], Texts(await NotesOfAsync(user, habit, legs)));

        Assert.Equal(HttpStatusCode.OK,
            (await user.Client.DeleteAsync($"/api/habits/{habit}/sub-habits/{legs}/records/{Monday}")).StatusCode);
        Assert.Equal(["kept"], Texts(await NotesOfAsync(user, habit, legs)));

        // The habit's own day, ticked and taken back, clears the part's tick - not the words.
        await user.Client.PutAsync($"/api/habits/{habit}/records/{Monday}");
        await user.Client.DeleteAsync($"/api/habits/{habit}/records/{Monday}");
        Assert.Equal(["kept"], Texts(await NotesOfAsync(user, habit, legs)));
    }

    [Theory]
    [InlineData("""{ "text": "" }""")]
    [InlineData("""{ "text": "   " }""")]
    [InlineData("""{ "text": null }""")]
    [InlineData("""{ }""")]
    public async Task Set_ABlankOrMissingText_IsA400OnText_AndNothingIsStored(string json)
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        var response = await user.Client.SendRawAsync(HttpMethod.Put, NoteUrl(habit, legs, Monday), json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("text", await response.ErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
        Assert.Empty(await NotesOfAsync(user, habit, legs));
    }

    [Fact]
    public async Task Set_ATextOfTheLimit_IsFine_OneLongerIsA400()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        var atTheLimit = await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = new string('a', 500) });
        var tooLong = await user.Client.PutAsync(NoteUrl(habit, legs, NextMonday), new { text = new string('a', 501) });

        Assert.Equal(HttpStatusCode.OK, atTheLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("text", await tooLong.ErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal([Monday], Dates(await NotesOfAsync(user, habit, legs)));
    }

    [Theory]
    [InlineData("1999-12-31")]
    [InlineData("2101-01-01")]
    public async Task Set_ADayOutsideThePlausibleRange_IsA400OnDate(string date)
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        var response = await user.Client.PutAsync(NoteUrl(habit, legs, date), new { text = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("date", await response.ErrorKeysAsync(), StringComparer.OrdinalIgnoreCase);
    }

    // ---- isolation ----

    [Fact]
    public async Task Set_APartOfAnotherUser_Is404_AndWritesNothing()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var (habit, legs) = await SportWithLegs(owner);

        var response = await stranger.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "not mine" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await NotesOfAsync(owner, habit, legs));
    }

    [Fact]
    public async Task Set_APartThatIsNotOfTheHabit_Is404()
    {
        var user = await NewUser();
        var (habit, _) = await SportWithLegs(user);
        var (_, otherLegs) = await SportWithLegs(user);

        Assert.Equal(HttpStatusCode.NotFound,
            (await user.Client.PutAsync(NoteUrl(habit, otherLegs, Monday), new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await user.Client.PutAsync(NoteUrl(Guid.NewGuid(), Guid.NewGuid(), Monday), new { text = "x" })).StatusCode);
    }

    [Fact]
    public async Task TheOverview_NeverCarriesTheNotesOfAnotherUser()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var (habit, legs) = await SportWithLegs(owner);
        await owner.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "private" });
        var (strangerHabit, strangerLegs) = await SportWithLegs(stranger);

        var strangersNotes = await NotesOfAsync(stranger, strangerHabit, strangerLegs);
        var strangersOverview = (await stranger.OverviewAsync("2026-10-01", "2026-10-31")).ToString();

        Assert.Empty(strangersNotes);
        Assert.DoesNotContain("private", strangersOverview);
    }

    // ---- clear ----

    [Fact]
    public async Task Clear_Answers204_AndTheNoteIsGone_TheOthersStay()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "monday" });
        await user.Client.PutAsync(NoteUrl(habit, legs, NextMonday), new { text = "next monday" });

        var response = await user.Client.DeleteAsync(NoteUrl(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["next monday"], Texts(await NotesOfAsync(user, habit, legs)));
    }

    [Fact]
    public async Task Clear_IsIdempotent_ADayWithoutANoteIsNo404()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync(NoteUrl(habit, legs, Monday))).StatusCode);

        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "x" });
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync(NoteUrl(habit, legs, Monday))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync(NoteUrl(habit, legs, Monday))).StatusCode);
    }

    [Fact]
    public async Task Clear_APartOfAnotherUser_Is404_AndTheNoteStays()
    {
        var owner = await NewUser();
        var stranger = await NewUser();
        var (habit, legs) = await SportWithLegs(owner);
        await owner.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "mine" });

        var response = await stranger.Client.DeleteAsync(NoteUrl(habit, legs, Monday));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["mine"], Texts(await NotesOfAsync(owner, habit, legs)));
    }

    // ---- life cycle ----

    [Fact]
    public async Task RemovingThePart_TakesItsNotesWithIt_AndANewPartOfTheSameNameStartsEmpty()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "gone with the part" });

        Assert.Equal(HttpStatusCode.NoContent,
            (await user.Client.DeleteAsync($"/api/habits/{habit}/sub-habits/{legs}")).StatusCode);
        var again = await user.AddSubHabitAsync(habit, "Legs", MondayDay);

        Assert.Empty(await NotesOfAsync(user, habit, again));
        Assert.Equal(HttpStatusCode.NotFound,
            (await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "x" })).StatusCode);
    }

    [Fact]
    public async Task RenamingThePart_KeepsItsNotes()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "stays" });

        var response = await user.Client.PutAsync(
            $"/api/habits/{habit}/sub-habits/{legs}", new { title = "Legs & glutes", weekdays = new[] { MondayDay } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["stays"], Texts(await NotesOfAsync(user, habit, legs)));
    }

    [Fact]
    public async Task DeletingTheHabit_TakesTheNotesOfItsPartsWithIt()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);
        await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "x" });

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.DeleteAsync($"/api/habits/{habit}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound,
            (await user.Client.PutAsync(NoteUrl(habit, legs, Monday), new { text = "x" })).StatusCode);
    }

    [Fact]
    public async Task AHabitWithoutNotes_HasAnEmptyListOnEveryPart()
    {
        var user = await NewUser();
        var (habit, legs) = await SportWithLegs(user);

        var item = (await user.OverviewAsync("2026-10-01", "2026-10-31")).EnumerateArray().Single();

        Assert.Equal(habit, item.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Array, item.GetProperty("subHabits")[0].GetProperty("notes").ValueKind);
        Assert.Empty(await NotesOfAsync(user, habit, legs));
    }
}
