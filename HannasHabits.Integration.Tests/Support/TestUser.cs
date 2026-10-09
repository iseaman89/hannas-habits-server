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
    // Made up anew for every test run: no password is written down in the code, and it still meets the password rules
    // (upper and lower case, a digit and a symbol are in the fixed start, the rest is random).
    public static readonly string Password = "Tt1!" + Guid.NewGuid().ToString("N");

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

    public static async Task<TestUser> RegisterAsync(ApiFactory api, string? firstName = null, string? email = null, string? lastName = null)
    {
        email ??= NewEmail();
        var anonymous = new ApiClient(api.CreateClient());

        var response = await anonymous.PostAsync("/api/auth/register", new { email, password = Password, firstName, lastName });
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
