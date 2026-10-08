using HannasHabits.Application.Auth;
using HannasHabits.Application.Auth.Commands.GoogleLogin;
using HannasHabits.Application.Auth.Commands.Login;
using HannasHabits.Application.Auth.Commands.Refresh;
using HannasHabits.Application.Auth.Commands.Register;
using HannasHabits.Application.Auth.Commands.Revoke;
using HannasHabits.Application.Auth.Commands.RevokeAll;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Application.Tests.Fakes;

namespace HannasHabits.Application.Tests.Auth;

// The handlers only orchestrate: who proves what first, and that no token is issued unless that step succeeded.
public class AuthHandlerTests
{
    private readonly List<string> _calls = [];
    private readonly FakeIdentityService _identity;
    private readonly FakeJwtTokenService _tokens;
    private readonly FakeGoogleTokenVerifier _google = new();
    private readonly FakeCurrentUser _user = new();

    public AuthHandlerTests()
    {
        _identity = new FakeIdentityService(_calls);
        _tokens = new FakeJwtTokenService(_calls);
    }

    // ---- register ----

    [Fact]
    public async Task Register_CreatesTheAccount_ThenIssuesTokensForIt()
    {
        var result = await new RegisterCommandHandler(_identity, _tokens)
            .Handle(new RegisterCommand("ada@example.com", "Passw0rd!", "Ada"), CancellationToken.None);

        Assert.Equal(["identity.register", "tokens.create"], _calls);
        Assert.Equal(("ada@example.com", "Passw0rd!", "Ada"),
            (_identity.RegisteredEmail, _identity.RegisteredPassword, _identity.RegisteredDisplayName));
        Assert.Same(FakeIdentityService.DefaultUser, _tokens.IssuedFor);
        Assert.Equal(new AuthResult(FakeIdentityService.DefaultUser, FakeJwtTokenService.DefaultTokens), result);
    }

    [Fact]
    public async Task Register_WithoutAName_PassesNullOn()
    {
        await new RegisterCommandHandler(_identity, _tokens)
            .Handle(new RegisterCommand("ada@example.com", "Passw0rd!"), CancellationToken.None);

        Assert.Null(_identity.RegisteredDisplayName);
    }

    [Fact]
    public async Task Register_WhenTheEmailIsTaken_NoTokenIsIssued()
    {
        _identity.FailWith = new ConflictException("The email address is already in use.");

        await Assert.ThrowsAsync<ConflictException>(() => new RegisterCommandHandler(_identity, _tokens)
            .Handle(new RegisterCommand("ada@example.com", "Passw0rd!"), CancellationToken.None));

        Assert.Equal(["identity.register"], _calls);
    }

    // ---- login ----

    [Fact]
    public async Task Login_ChecksTheCredentials_ThenIssuesTokens()
    {
        var result = await new LoginCommandHandler(_identity, _tokens)
            .Handle(new LoginCommand("ada@example.com", "Passw0rd!"), CancellationToken.None);

        Assert.Equal(["identity.authenticate", "tokens.create"], _calls);
        Assert.Equal(FakeJwtTokenService.DefaultTokens, result.Tokens);
        Assert.Equal(FakeIdentityService.DefaultUser, result.User);
    }

    [Theory]
    [MemberData(nameof(LoginFailures))]
    public async Task Login_WhenTheCredentialsAreRejected_NoTokenIsIssued(Exception failure)
    {
        _identity.FailWith = failure;

        var thrown = await Assert.ThrowsAsync(failure.GetType(), () => new LoginCommandHandler(_identity, _tokens)
            .Handle(new LoginCommand("ada@example.com", "wrong"), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["identity.authenticate"], _calls);
    }

    public static TheoryData<Exception> LoginFailures => new()
    {
        new AuthenticationFailedException("The email or password is incorrect."),
        new AccountLockedOutException()
    };

    // ---- google ----

    [Fact]
    public async Task GoogleLogin_VerifiesTheToken_ThenSignsInTheVerifiedIdentity_ThenIssuesTokens()
    {
        var result = await new GoogleLoginCommandHandler(_google, _identity, _tokens)
            .Handle(new GoogleLoginCommand("google-id-token"), CancellationToken.None);

        Assert.Equal("google-id-token", _google.VerifiedToken);
        Assert.Same(_google.Identity, _identity.SignedInExternal); // exactly what Google vouched for, nothing from the client
        Assert.Equal(["identity.external", "tokens.create"], _calls);
        Assert.Equal(FakeJwtTokenService.DefaultTokens, result.Tokens);
    }

    [Fact]
    public async Task GoogleLogin_WithAnInvalidToken_NothingElseHappens()
    {
        _google.FailWith = new AuthenticationFailedException("The Google token is not valid.");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => new GoogleLoginCommandHandler(_google, _identity, _tokens)
            .Handle(new GoogleLoginCommand("forged"), CancellationToken.None));

        Assert.Empty(_calls);
    }

    [Fact]
    public async Task GoogleLogin_WhenAPasswordAccountOwnsTheEmail_NoTokenIsIssued()
    {
        _identity.FailWith = new ConflictException("An account with this email address already exists.");

        await Assert.ThrowsAsync<ConflictException>(() => new GoogleLoginCommandHandler(_google, _identity, _tokens)
            .Handle(new GoogleLoginCommand("token"), CancellationToken.None));

        Assert.Equal(["identity.external"], _calls);
    }

    // ---- refresh ----

    [Fact]
    public async Task Refresh_ExchangesTheTokenAndReturnsWhatTheTokenServiceAnswers()
    {
        var answer = new AuthResult(FakeIdentityService.DefaultUser, FakeJwtTokenService.DefaultTokens with { RefreshToken = "rotated" });
        _tokens.RefreshResult = answer;

        var result = await new RefreshCommandHandler(_tokens).Handle(new RefreshCommand("old-token"), CancellationToken.None);

        Assert.Equal("old-token", _tokens.RefreshedWith);
        Assert.Same(answer, result);
    }

    // ---- logout ----

    [Fact]
    public async Task Revoke_RevokesTheTokenForTheCurrentUserOnly()
    {
        await new RevokeCommandHandler(_tokens, _user).Handle(new RevokeCommand("some-token"), CancellationToken.None);

        Assert.Equal((_user.UserId, "some-token"), _tokens.Revoked);
    }

    [Fact]
    public async Task RevokeAll_RevokesEverySessionOfTheCurrentUser()
    {
        await new RevokeAllCommandHandler(_tokens, _user).Handle(new RevokeAllCommand(), CancellationToken.None);

        Assert.Equal(_user.UserId, _tokens.RevokedAllFor);
    }
}
