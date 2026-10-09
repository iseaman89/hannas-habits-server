using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HannasHabits.Integration.Tests.Support;
using Microsoft.IdentityModel.Tokens;

namespace HannasHabits.Integration.Tests.Api;

[Collection(IntegrationCollection.Name)]
public class AuthApiTests
{
    private readonly ApiFactory _api;

    public AuthApiTests(TestEnvironment environment)
    {
        _api = environment.Api;
    }

    private ApiClient Anonymous() => TestUser.Anonymous(_api);

    private Task<HttpResponseMessage> Register(object body) => Anonymous().PostAsync("/api/auth/register", body);

    private Task<HttpResponseMessage> Login(string email, string password)
        => Anonymous().PostAsync("/api/auth/login", new { email, password });

    private Task<HttpResponseMessage> Refresh(string refreshToken)
        => Anonymous().PostAsync("/api/auth/refresh", new { refreshToken });

    // ---- register ----

    [Fact]
    public async Task Register_AnswersWithTheUserAndAFreshPairOfTokens()
    {
        var email = TestUser.NewEmail();

        var response = await Register(new { email, password = TestUser.Password, firstName = "  Ada ", lastName = " Lovelace " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();

        var user = body.GetProperty("user");
        Assert.NotEqual(Guid.Empty, user.GetProperty("id").GetGuid());
        Assert.Equal(email, user.GetProperty("userName").GetString());
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Ada", user.GetProperty("firstName").GetString());
        Assert.Equal("Lovelace", user.GetProperty("lastName").GetString());

        var tokens = body.GetProperty("tokens");
        Assert.Equal(3, tokens.GetProperty("accessToken").GetString()!.Split('.').Length); // a JWT
        Assert.Equal(88, tokens.GetProperty("refreshToken").GetString()!.Length);          // 64 random bytes, base64
        Assert.InRange(tokens.GetProperty("accessTokenExpiresAt").GetDateTime(), DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        Assert.InRange(tokens.GetProperty("refreshTokenExpiresAt").GetDateTime(), DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_WithoutAName_TheFirstNameIsTheLocalPartOfTheEmail(string? firstName)
    {
        var local = $"grace-{Guid.NewGuid():N}";

        var response = await Register(new { email = local + "@example.com", password = TestUser.Password, firstName });

        var user = (await response.ReadJsonAsync()).GetProperty("user");
        Assert.Equal(local, user.GetProperty("firstName").GetString());
        Assert.Equal(JsonValueKind.Null, user.GetProperty("lastName").ValueKind); // none given: null, not an empty string
    }

    [Fact]
    public async Task Register_EachNameMayHave100Characters_NotMore_AndARejectedNameCreatesNoAccount()
    {
        var ok = await Register(new { email = TestUser.NewEmail(), password = TestUser.Password, firstName = new string('n', 100), lastName = new string('l', 100) });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var email = TestUser.NewEmail();
        var firstTooLong = await Register(new { email, password = TestUser.Password, firstName = new string('n', 101) });
        var lastTooLong = await Register(new { email, password = TestUser.Password, firstName = "Ada", lastName = new string('l', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, firstTooLong.StatusCode);
        Assert.Contains("firstName", await firstTooLong.ErrorKeysAsync());
        Assert.Equal(HttpStatusCode.BadRequest, lastTooLong.StatusCode);
        Assert.Contains("lastName", await lastTooLong.ErrorKeysAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, TestUser.Password)).StatusCode);
    }

    [Fact]
    public async Task Register_NamesWithUmlautsAndEmojiSurvive()
    {
        var response = await Register(new { email = TestUser.NewEmail(), password = TestUser.Password, firstName = "Jörg 🎉 Müller" });

        Assert.Equal("Jörg 🎉 Müller", (await response.ReadJsonAsync()).GetProperty("user").GetProperty("firstName").GetString());
    }

    [Fact]
    public async Task Register_AWeakPassword_IsA400OnPassword_AndNoAccountIsCreated()
    {
        var email = TestUser.NewEmail();

        var response = await Register(new { email, password = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("password", await response.ErrorKeysAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, "abc")).StatusCode);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Register_ABadEmail_IsA400OnEmail(string email)
    {
        var response = await Register(new { email, password = TestUser.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("email", await response.ErrorKeysAsync());
    }

    [Fact]
    public async Task Register_MissingProperties_ComeBackWithCamelCaseKeys_LikeEveryOtherValidationError()
    {
        var response = await Anonymous().SendRawAsync(HttpMethod.Post, "/api/auth/register", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var keys = await response.ErrorKeysAsync();
        Assert.Contains("email", keys);
        Assert.Contains("password", keys);
        Assert.DoesNotContain(keys, key => key != key.ToLowerInvariant() && char.IsUpper(key[0]));
    }

    [Fact]
    public async Task Register_PascalCasePropertyNamesBindToo()
    {
        var response = await Anonymous().SendRawAsync(HttpMethod.Post, "/api/auth/register",
            $$"""{ "Email": "{{TestUser.NewEmail()}}", "Password": "{{TestUser.Password}}" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ABrokenBody_IsA400_NotA500()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Anonymous().SendRawAsync(HttpMethod.Post, "/api/auth/register", "{ not json")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Register(new { email = 123, password = TestUser.Password })).StatusCode);
    }

    [Theory]
    [InlineData("same@example.com", "SAME@EXAMPLE.COM")]
    [InlineData("same@example.com", "same@example.com")]
    public async Task Register_AnEmailThatIsTaken_IsA409_EvenInOtherCase(string first, string second)
    {
        var unique = Guid.NewGuid().ToString("N");
        first = unique + first;
        second = unique + second;
        Assert.Equal(HttpStatusCode.OK, (await Register(new { email = first, password = TestUser.Password })).StatusCode);

        var response = await Register(new { email = second, password = TestUser.Password });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Errors_AreProblemDetails()
    {
        var response = await Login("nobody@example.com", "wrong");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.ReadJsonAsync();
        Assert.Equal(401, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
    }

    // ---- login ----

    [Fact]
    public async Task Login_AnswersWithTheSameShapeAsRegister()
    {
        var user = await TestUser.RegisterAsync(_api, "Ada");

        var response = await Login(user.Email, TestUser.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(user.Id, body.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("Ada", body.GetProperty("user").GetProperty("firstName").GetString());
        Assert.NotEqual(user.RefreshToken, body.GetProperty("tokens").GetProperty("refreshToken").GetString()); // a new session
    }

    [Fact]
    public async Task Login_TheEmailIsNotCaseSensitive()
    {
        var user = await TestUser.RegisterAsync(_api);

        Assert.Equal(HttpStatusCode.OK, (await Login(user.Email.ToUpperInvariant(), TestUser.Password)).StatusCode);
    }

    [Fact]
    public async Task Login_AWrongPasswordAndAnUnknownEmail_AreIndistinguishable()
    {
        var user = await TestUser.RegisterAsync(_api);

        var wrongPassword = await Login(user.Email, "Wr0ng-password!");
        var unknownEmail = await Login(TestUser.NewEmail(), "Wr0ng-password!");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await wrongPassword.ReadJsonAsync()).GetProperty("detail").GetString(),
            (await unknownEmail.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_TheFifthWrongPasswordLocksTheAccount_EvenTheRightPasswordIsRefusedAfterwards()
    {
        var user = await TestUser.RegisterAsync(_api);

        for (var attempt = 1; attempt <= 4; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(user.Email, "Wr0ng-password!")).StatusCode);

        // The fifth failure is the one that trips the lock - it is already answered with 429.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(user.Email, "Wr0ng-password!")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(user.Email, TestUser.Password)).StatusCode);
    }

    [Fact]
    public async Task Login_FourWrongPasswords_DoNotLockTheAccount()
    {
        var user = await TestUser.RegisterAsync(_api);

        for (var attempt = 1; attempt <= 4; attempt++)
            await Login(user.Email, "Wr0ng-password!");

        Assert.Equal(HttpStatusCode.OK, (await Login(user.Email, TestUser.Password)).StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_AreA400()
    {
        var response = await Anonymous().SendRawAsync(HttpMethod.Post, "/api/auth/login", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["email", "password"], (await response.ErrorKeysAsync()).Order());
    }

    // ---- refresh ----

    [Fact]
    public async Task Refresh_ReturnsTheUserAndANewPair_AndTheNewAccessTokenWorks()
    {
        var user = await TestUser.RegisterAsync(_api, "Ada");

        var response = await Refresh(user.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(user.Id, body.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal("Ada", body.GetProperty("user").GetProperty("firstName").GetString());

        var tokens = body.GetProperty("tokens");
        Assert.NotEqual(user.RefreshToken, tokens.GetProperty("refreshToken").GetString());

        var client = new ApiClient(_api.CreateClient()).WithBearer(tokens.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/habits")).StatusCode);
    }

    [Fact]
    public async Task Refresh_ATokenWorksExactlyOnce_AndItsReplayRevokesEverySessionOfTheUser()
    {
        var user = await TestUser.RegisterAsync(_api);
        var rotated = await (await Refresh(user.RefreshToken)).ReadJsonAsync();
        var successor = rotated.GetProperty("tokens").GetProperty("refreshToken").GetString()!;
        var otherSession = await (await Login(user.Email, TestUser.Password)).ReadJsonAsync();
        var otherToken = otherSession.GetProperty("tokens").GetProperty("refreshToken").GetString()!;

        // Somebody presents the used token again: a copy has been stolen - or the real client lost a response.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(user.RefreshToken)).StatusCode);

        // Nobody can be trusted now: not the successor, not the other device.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(successor)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(otherToken)).StatusCode);
    }

    [Fact]
    public async Task Refresh_TenParallelRefreshesOfOneToken_GiveExactlyOneSuccess()
    {
        var user = await TestUser.RegisterAsync(_api);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Refresh(user.RefreshToken)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Refresh_AnUnknownToken_IsA401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(Convert.ToBase64String(new byte[64]))).StatusCode);
    }

    [Fact]
    public async Task Refresh_AMissingOrHugeToken_IsA400()
    {
        var empty = await Refresh("");
        var huge = await Refresh(new string('a', 257));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("refreshToken", await empty.ErrorKeysAsync());
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
    }

    // ---- revoke / logout ----

    [Fact]
    public async Task Revoke_NeedsAnAccessToken()
    {
        var user = await TestUser.RegisterAsync(_api);

        var response = await Anonymous().PostAsync("/api/auth/revoke", new { refreshToken = user.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_EndsThatSession_Idempotently_WithoutEndingTheOthers()
    {
        var user = await TestUser.RegisterAsync(_api);
        var other = await (await Login(user.Email, TestUser.Password)).ReadJsonAsync();
        var otherToken = other.GetProperty("tokens").GetProperty("refreshToken").GetString()!;

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsync("/api/auth/revoke", new { refreshToken = user.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsync("/api/auth/revoke", new { refreshToken = user.RefreshToken })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(user.RefreshToken)).StatusCode);

        // A plain logout is not a theft alarm: the other device keeps its session.
        Assert.Equal(HttpStatusCode.OK, (await Refresh(otherToken)).StatusCode);
    }

    [Fact]
    public async Task Revoke_ForgetsAboutUnknownTokens_AndTokensOfOtherUsers()
    {
        var mallory = await TestUser.RegisterAsync(_api);
        var victim = await TestUser.RegisterAsync(_api);

        Assert.Equal(HttpStatusCode.NoContent, (await mallory.Client.PostAsync("/api/auth/revoke", new { refreshToken = victim.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await mallory.Client.PostAsync("/api/auth/revoke", new { refreshToken = "does-not-exist" })).StatusCode);

        // The victim's session is untouched.
        Assert.Equal(HttpStatusCode.OK, (await Refresh(victim.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task RevokeAll_EndsEverySessionOfTheUser_AndOnlyOfTheUser()
    {
        var user = await TestUser.RegisterAsync(_api);
        var bystander = await TestUser.RegisterAsync(_api);
        var second = await (await Login(user.Email, TestUser.Password)).ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await user.Client.PostAsync("/api/auth/revoke-all")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(user.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(second.GetProperty("tokens").GetProperty("refreshToken").GetString()!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Refresh(bystander.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task RevokeAll_NeedsAnAccessToken()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous().PostAsync("/api/auth/revoke-all")).StatusCode);
    }

    // ---- access tokens ----

    private async Task<HttpStatusCode> CallWithToken(string? token)
    {
        var client = Anonymous();
        if (token is not null)
            client.WithBearer(token);

        return (await client.GetAsync("/api/habits")).StatusCode;
    }

    private static string AccessToken(
        string key = ApiFactory.JwtKey, string issuer = ApiFactory.JwtIssuer, string audience = ApiFactory.JwtAudience,
        DateTime? expires = null, string? userId = null)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        var expiry = expires ?? now.AddMinutes(10);

        var token = new JwtSecurityToken(
            issuer, audience,
            [new Claim(ClaimTypes.NameIdentifier, userId ?? Guid.NewGuid().ToString())],
            notBefore: expiry < now ? expiry.AddMinutes(-10) : now.AddSeconds(-1), expires: expiry, signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task AValidToken_IsAccepted_Garbage_IsNot()
    {
        Assert.Equal(HttpStatusCode.OK, await CallWithToken(AccessToken()));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(null));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken("garbage"));
    }

    [Fact]
    public async Task ATokenIsRejected_ForAWrongKey_Issuer_Audience_OrWhenExpired()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(AccessToken(key: "another-signing-key-0123456789-abcdefghijklmnop")));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(AccessToken(issuer: "somebody-else")));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(AccessToken(audience: "somebody-else")));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(AccessToken(expires: DateTime.UtcNow.AddHours(-1))));
    }

    [Fact]
    public async Task ATamperedToken_IsRejected()
    {
        var victim = Guid.NewGuid().ToString();
        var parts = AccessToken(userId: Guid.NewGuid().ToString()).Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        var forgedPayload = System.Text.RegularExpressions.Regex.Replace(
            payload, "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", victim); // somebody else's id
        Assert.NotEqual(payload, forgedPayload);

        // Header and signature are the original ones: the signature no longer matches the changed payload.
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(forgedPayload)}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(forged));
    }

    [Fact]
    public async Task ATokenWithoutAUserId_IsRejected()
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            ApiFactory.JwtIssuer, ApiFactory.JwtAudience, [new Claim("name", "no id")], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));

        // Signed correctly, but there is nobody the data could belong to.
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithToken(token));
    }

    // ---- google ----

    private Task<HttpResponseMessage> Google(string idToken) => Anonymous().PostAsync("/api/auth/google", new { idToken });

    [Fact]
    public async Task Google_TheFirstSignInCreatesTheAccount_TheNextOneFindsIt()
    {
        var subject = Guid.NewGuid().ToString("N");
        var email = TestUser.NewEmail();
        var token = FakeGoogleTokenVerifier.TokenFor(subject, email, "Ada", "Lovelace");

        var first = await Google(token);
        var second = await Google(token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var user = (await first.ReadJsonAsync()).GetProperty("user");
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal("Ada", user.GetProperty("firstName").GetString());
        Assert.Equal("Lovelace", user.GetProperty("lastName").GetString());
        Assert.Equal(user.GetProperty("id").GetGuid(), (await second.ReadJsonAsync()).GetProperty("user").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Google_TheTokensOfAGoogleSignInWork()
    {
        var response = await Google(FakeGoogleTokenVerifier.TokenFor(Guid.NewGuid().ToString("N"), TestUser.NewEmail()));
        var tokens = (await response.ReadJsonAsync()).GetProperty("tokens");

        var client = new ApiClient(_api.CreateClient()).WithBearer(tokens.GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/habits")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Refresh(tokens.GetProperty("refreshToken").GetString()!)).StatusCode);
    }

    [Fact]
    public async Task Google_APasswordAccountWithTheSameEmail_IsNotTakenOver()
    {
        var user = await TestUser.RegisterAsync(_api);

        var response = await Google(FakeGoogleTokenVerifier.TokenFor(Guid.NewGuid().ToString("N"), user.Email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        // ...and the password account still works as before.
        Assert.Equal(HttpStatusCode.OK, (await Login(user.Email, TestUser.Password)).StatusCode);
    }

    [Fact]
    public async Task Google_AnInvalidToken_IsA401_AMissingOneA400()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Google("forged")).StatusCode);

        var missing = await Google("");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("idToken", await missing.ErrorKeysAsync());
    }

    [Fact]
    public async Task Google_ParallelFirstSignIns_CreateOneAccount()
    {
        var token = FakeGoogleTokenVerifier.TokenFor(Guid.NewGuid().ToString("N"), TestUser.NewEmail(), "Ada");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Google(token)));

        Assert.True(responses.All(r => r.StatusCode == HttpStatusCode.OK),
            string.Join(Environment.NewLine, _api.Logs.Errors.Where(e => e.Contains("GlobalExceptionHandler")).Take(2)));
        var ids = await Task.WhenAll(responses.Select(async r => (await r.ReadJsonAsync()).GetProperty("user").GetProperty("id").GetGuid()));
        Assert.Single(ids.Distinct());
    }
}
