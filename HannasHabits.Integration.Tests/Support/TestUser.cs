using System.Net;
using System.Text.Json;

namespace HannasHabits.Integration.Tests.Support;

/// <summary>
/// A freshly registered user of the application under test, with a client that sends their access token.
/// Every test creates its own users, so tests never see each other's data - the same isolation the application gives
/// its real users.
/// </summary>
public sealed class TestUser
{
    public const string Password = "Passw0rd!-test";

    private TestUser(ApiClient client, string email, Guid id, string accessToken, string refreshToken)
    {
        Client = client;
        Email = email;
        Id = id;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    public ApiClient Client { get; }
    public string Email { get; }
    public Guid Id { get; }
    public string AccessToken { get; }
    public string RefreshToken { get; }

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    public static async Task<TestUser> RegisterAsync(ApiFactory api, string? displayName = null, string? email = null)
    {
        email ??= NewEmail();
        var anonymous = new ApiClient(api.CreateClient());

        var response = await anonymous.PostAsync("/api/auth/register", new { email, password = Password, displayName });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.ReadJsonAsync();
        var accessToken = body.GetProperty("tokens").GetProperty("accessToken").GetString()!;
        var refreshToken = body.GetProperty("tokens").GetProperty("refreshToken").GetString()!;
        var id = body.GetProperty("user").GetProperty("id").GetGuid();

        return new TestUser(new ApiClient(api.CreateClient()).WithBearer(accessToken), email, id, accessToken, refreshToken);
    }

    /// <summary>A client without any token.</summary>
    public static ApiClient Anonymous(ApiFactory api) => new(api.CreateClient());
}
