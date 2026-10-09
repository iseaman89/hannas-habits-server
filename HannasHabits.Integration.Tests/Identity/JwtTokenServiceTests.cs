using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Infrastructure.Identity;
using HannasHabits.Infrastructure.Persistence;
using HannasHabits.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace HannasHabits.Integration.Tests.Identity;

// Access tokens and the life of refresh tokens, with a clock the test moves by hand. Every call runs in a scope of its
// own, like one HTTP request each.
[Collection(IntegrationCollection.Name)]
public class JwtTokenServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly TestEnvironment _environment;
    private readonly FakeTimeProvider _clock = new(Start);
    private ServiceProvider _provider = null!;
    private ApplicationDbContext _db = null!;

    public JwtTokenServiceTests(TestEnvironment environment)
    {
        _environment = environment;
    }

    public Task InitializeAsync()
    {
        _provider = TestServices.Build(_environment.SharedConnectionString, _clock);
        _db = _environment.CreateContext();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _provider.DisposeAsync();
    }

    private async Task<IdentityUserDto> NewUserAsync(string? name = "Ada")
    {
        var id = await Sql.InsertUserAsync(_db);
        var email = (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).Email!;
        return new IdentityUserDto(id, email, email, name ?? "ada", null);
    }

    private async Task<T> InScope<T>(Func<IJwtTokenService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IJwtTokenService>());
    }

    private Task<TokenPair> Issue(IdentityUserDto user) => InScope(tokens => tokens.CreateTokenPairAsync(user));

    private Task<AuthResult> Refresh(string token) => InScope(tokens => tokens.RefreshAsync(token));

    private Task Revoke(Guid userId, string token) => InScope(async tokens => { await tokens.RevokeAsync(userId, token); return 0; });

    private Task RevokeAll(Guid userId) => InScope(async tokens => { await tokens.RevokeAllAsync(userId); return 0; });

    private static string HashOf(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<RefreshToken> Stored(string token)
        => await _db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == HashOf(token));

    // ---- the access token ----

    [Fact]
    public async Task TheAccessToken_CarriesTheUser_TheIssuerAndAudience_AndExpiresAfterTheConfiguredTime()
    {
        var user = await NewUserAsync();

        var pair = await Issue(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(pair.AccessToken);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.UserName, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Contains(jwt.Claims, c => c.Type is ClaimTypes.NameIdentifier or "nameid" && c.Value == user.Id.ToString());
        Assert.Equal(ApiFactory.JwtIssuer, jwt.Issuer);
        Assert.Contains(ApiFactory.JwtAudience, jwt.Audiences);
        Assert.Equal(Start.UtcDateTime.AddMinutes(15), pair.AccessTokenExpiresAt);
        Assert.Equal(pair.AccessTokenExpiresAt, jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TheAccessToken_IsSignedWithTheConfiguredKey_AndOnlyThatKeyAcceptsIt()
    {
        var pair = await Issue(await NewUserAsync());
        TokenValidationParameters Parameters(string key) => new()
        {
            ValidIssuer = ApiFactory.JwtIssuer,
            ValidAudience = ApiFactory.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false
        };
        var handler = new JwtSecurityTokenHandler();

        handler.ValidateToken(pair.AccessToken, Parameters(ApiFactory.JwtKey), out _);
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(pair.AccessToken, Parameters("another-signing-key-0123456789-abcdefghijklmnop"), out _));
    }

    // ---- the refresh token as stored ----

    [Fact]
    public async Task TheRefreshToken_IsStoredOnlyAsAHash_WithItsLifetime()
    {
        var user = await NewUserAsync();

        var pair = await Issue(user);

        var stored = await Stored(pair.RefreshToken);
        Assert.NotEqual(pair.RefreshToken, stored.TokenHash);       // a leaked database holds no working session
        Assert.Equal(64, stored.TokenHash.Length);                  // hex of a SHA-256
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(Start.UtcDateTime, stored.CreatedAt);
        Assert.Equal(Start.UtcDateTime.AddDays(30), stored.ExpiresAt);
        Assert.Equal(stored.ExpiresAt, pair.RefreshTokenExpiresAt);
        Assert.Null(stored.RevokedAt);
        Assert.Null(stored.ReplacedByTokenId);
        Assert.Equal(0, await Sql.CountAsync(_db, "RefreshTokens", "TokenHash", pair.RefreshToken)); // the token itself appears nowhere
    }

    [Fact]
    public async Task EverySessionGetsItsOwnRandomToken()
    {
        var user = await NewUserAsync();

        var tokens = new[] { await Issue(user), await Issue(user), await Issue(user) };

        Assert.Equal(3, tokens.Select(t => t.RefreshToken).Distinct().Count());
        Assert.All(tokens, t => Assert.Equal(88, t.RefreshToken.Length));
        Assert.Equal(3, await Sql.CountAsync(_db, "RefreshTokens", "UserId", user.Id));
    }

    // ---- rotation ----

    [Fact]
    public async Task Refresh_UsesTheTokenUp_AndLinksItToItsSuccessor()
    {
        var user = await NewUserAsync();
        var first = await Issue(user);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await Refresh(first.RefreshToken);

        var old = await Stored(first.RefreshToken);
        var successor = await Stored(result.Tokens.RefreshToken);
        Assert.Equal(Start.UtcDateTime.AddMinutes(5), old.RevokedAt);
        Assert.Equal(successor.Id, old.ReplacedByTokenId);      // the chain: this one was exchanged for that one
        Assert.True(old.IsRotated);
        Assert.Null(successor.RevokedAt);
        Assert.Null(successor.ReplacedByTokenId);
        Assert.Equal(Start.UtcDateTime.AddMinutes(5).AddDays(30), successor.ExpiresAt); // a full new lifetime
        Assert.NotEqual(first.RefreshToken, result.Tokens.RefreshToken);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(Start.UtcDateTime.AddMinutes(20), result.Tokens.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Refresh_ACanBeFollowedByB_ThenC_EachOnlyOnce()
    {
        var user = await NewUserAsync();
        var a = await Issue(user);
        var b = (await Refresh(a.RefreshToken)).Tokens;
        var c = (await Refresh(b.RefreshToken)).Tokens;

        var chain = (await Stored(a.RefreshToken), await Stored(b.RefreshToken), await Stored(c.RefreshToken));

        Assert.Equal(chain.Item2.Id, chain.Item1.ReplacedByTokenId);
        Assert.Equal(chain.Item3.Id, chain.Item2.ReplacedByTokenId);
        Assert.Null(chain.Item3.ReplacedByTokenId);
    }

    [Fact]
    public async Task Refresh_AnUnknownToken_IsRefused()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))));
    }

    [Fact]
    public async Task Refresh_AnExpiredToken_IsRefused_WithoutRevokingTheOtherSessions()
    {
        var user = await NewUserAsync();
        var old = await Issue(user);
        _clock.Advance(TimeSpan.FromDays(10));
        var young = await Issue(user);
        _clock.Advance(TimeSpan.FromDays(20) + TimeSpan.FromSeconds(1)); // day 30 + 1s: the old one is past its lifetime, the young one is not

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(old.RefreshToken));

        Assert.Null((await Stored(young.RefreshToken)).RevokedAt); // an expired token is not a theft alarm
        await Refresh(young.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ATokenIsStillGoodOnItsLastSecond()
    {
        var user = await NewUserAsync();
        var pair = await Issue(user);
        _clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1));

        await Refresh(pair.RefreshToken);
    }

    // ---- replay = theft alarm ----

    [Fact]
    public async Task Refresh_ReplayingAUsedToken_RevokesEverySessionOfThatUser_AndOnlyThatUser()
    {
        var thief = await NewUserAsync();
        var bystander = await NewUserAsync();
        var a = await Issue(thief);
        var otherDevice = await Issue(thief);
        var bystanderSession = await Issue(bystander);
        var b = (await Refresh(a.RefreshToken)).Tokens; // the real client rotated A to B

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(a.RefreshToken)); // somebody presents A again

        Assert.NotNull((await Stored(b.RefreshToken)).RevokedAt);             // the successor is dead
        Assert.NotNull((await Stored(otherDevice.RefreshToken)).RevokedAt);   // so is the other device
        Assert.Null((await Stored(bystanderSession.RefreshToken)).RevokedAt); // other users are untouched
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(b.RefreshToken));
    }

    [Fact]
    public async Task Refresh_ALoggedOutToken_IsRefused_ButItIsNoTheftAlarm()
    {
        var user = await NewUserAsync();
        var phone = await Issue(user);
        var laptop = await Issue(user);
        await Revoke(user.Id, phone.RefreshToken); // plain logout of the phone

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(phone.RefreshToken));

        Assert.Null((await Stored(laptop.RefreshToken)).RevokedAt);
        await Refresh(laptop.RefreshToken); // the laptop carries on
    }

    [Fact]
    public async Task Refresh_TwoRequestsWithTheSameTokenAtOnce_OnlyOneWins_AndTheLossEndsEverySession()
    {
        var barrier = new Barrier(2);
        var interceptor = new BarrierSaveInterceptor(barrier);
        await using var provider = TestServices.Build(_environment.SharedConnectionString, _clock, interceptor);
        var user = await NewUserAsync();
        TokenPair pair;
        await using (var setup = provider.CreateAsyncScope())
            pair = await setup.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateTokenPairAsync(user);

        interceptor.Armed = true; // from here on, both refreshes wait for each other right before they write

        async Task<(AuthResult? Result, Exception? Failure)> Attempt()
        {
            await using var scope = provider.CreateAsyncScope();
            try
            {
                return (await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().RefreshAsync(pair.RefreshToken), null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        var outcomes = await Task.WhenAll(Task.Run(Attempt), Task.Run(Attempt));
        interceptor.Armed = false;

        var winner = Assert.Single(outcomes, o => o.Result is not null);
        var loser = Assert.Single(outcomes, o => o.Failure is not null);
        Assert.IsType<AuthenticationFailedException>(loser.Failure);

        // Two requests used one token: that is a replay, so the winner's fresh token is revoked as well.
        Assert.NotNull((await Stored(pair.RefreshToken)).RevokedAt);
        Assert.NotNull((await Stored(winner.Result!.Tokens.RefreshToken)).RevokedAt);
        Assert.Equal(2, await Sql.CountAsync(_db, "RefreshTokens", "UserId", user.Id)); // no third token was created
    }

    // ---- logout ----

    [Fact]
    public async Task Revoke_EndsTheSession_AndKeepsTheFirstTimestamp()
    {
        var user = await NewUserAsync();
        var pair = await Issue(user);

        await Revoke(user.Id, pair.RefreshToken);
        _clock.Advance(TimeSpan.FromHours(1));
        await Revoke(user.Id, pair.RefreshToken); // idempotent

        var stored = await Stored(pair.RefreshToken);
        Assert.Equal(Start.UtcDateTime, stored.RevokedAt);
        Assert.Null(stored.ReplacedByTokenId); // revoked, not rotated
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(pair.RefreshToken));
    }

    [Fact]
    public async Task Revoke_CannotEndSomebodyElsesSession_AndIgnoresUnknownTokens()
    {
        var owner = await NewUserAsync();
        var mallory = await NewUserAsync();
        var pair = await Issue(owner);

        await Revoke(mallory.Id, pair.RefreshToken);
        await Revoke(mallory.Id, "no-such-token");

        Assert.Null((await Stored(pair.RefreshToken)).RevokedAt);
    }

    [Fact]
    public async Task Revoke_AnExpiredToken_IsNotAnError()
    {
        var user = await NewUserAsync();
        var pair = await Issue(user);
        _clock.Advance(TimeSpan.FromDays(31));

        await Revoke(user.Id, pair.RefreshToken);
    }

    [Fact]
    public async Task RevokeAll_EndsTheActiveSessionsOfTheUser_KeepsEarlierTimestamps_AndSparesOthers()
    {
        var user = await NewUserAsync();
        var bystander = await NewUserAsync();
        var loggedOutEarlier = await Issue(user);
        var active1 = await Issue(user);
        var active2 = await Issue(user);
        var bystanderSession = await Issue(bystander);
        await Revoke(user.Id, loggedOutEarlier.RefreshToken);
        _clock.Advance(TimeSpan.FromMinutes(10));

        await RevokeAll(user.Id);

        Assert.Equal(Start.UtcDateTime, (await Stored(loggedOutEarlier.RefreshToken)).RevokedAt); // not overwritten
        Assert.Equal(Start.UtcDateTime.AddMinutes(10), (await Stored(active1.RefreshToken)).RevokedAt);
        Assert.Equal(Start.UtcDateTime.AddMinutes(10), (await Stored(active2.RefreshToken)).RevokedAt);
        Assert.Null((await Stored(bystanderSession.RefreshToken)).RevokedAt);
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => Refresh(active1.RefreshToken));
        await Refresh(bystanderSession.RefreshToken);
    }
}
