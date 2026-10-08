using System.Net;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>The calls the tests use to set the stage; each of them also checks that the call itself worked.</summary>
public static class ApiExtensions
{
    public static async Task<Guid> CreateHabitAsync(
        this TestUser user, string title = "Read", int[]? schedule = null, string? startDate = null, string? description = null)
    {
        var response = await user.Client.PostAsync("/api/habits", new { title, description, schedule, startDate });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    public static async Task MarkAsync(this TestUser user, Guid habitId, params string[] dates)
    {
        foreach (var date in dates)
            Assert.Equal(HttpStatusCode.OK, (await user.Client.PutAsync($"/api/habits/{habitId}/records/{date}")).StatusCode);
    }

    public static string[] Days(int year, int month, params int[] days)
        => days.Select(day => new DateOnly(year, month, day).ToString("yyyy-MM-dd")).ToArray();

    public static async Task<string[]> RecordDatesAsync(this TestUser user, Guid habitId, string query = "")
    {
        var response = await user.Client.GetAsync($"/api/habits/{habitId}/records{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.ReadJsonAsync()).EnumerateArray().Select(record => record.GetProperty("date").GetString()!).ToArray();
    }

    public static async Task<System.Text.Json.JsonElement> OverviewAsync(this TestUser user, string from, string to, string? asOf = null)
    {
        var response = await user.Client.GetAsync($"/api/habits/overview?from={from}&to={to}" + (asOf is null ? "" : $"&asOf={asOf}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.ReadJsonAsync();
    }
}
