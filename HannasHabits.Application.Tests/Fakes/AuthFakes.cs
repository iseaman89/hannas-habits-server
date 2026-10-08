using HannasHabits.Application.Auth;

namespace HannasHabits.Application.Tests.Fakes;

/// <summary>
/// Plays the account store. Everything it is asked is recorded in <see cref="Calls"/> so a test can check the order of
/// the steps; <see cref="FailWith"/> makes the next call throw.
/// </summary>
internal sealed class FakeIdentityService : IIdentityService
{
    public static IdentityUserDto DefaultUser { get; } =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "ada@example.com", "ada@example.com", "Ada");

    public FakeIdentityService(List<string>? calls = null)
    {
        Calls = calls ?? [];
    }

    public IdentityUserDto User { get; set; } = DefaultUser;
    public Exception? FailWith { get; set; }
    public List<string> Calls { get; }

    public string? RegisteredEmail { get; private set; }
    public string? RegisteredPassword { get; private set; }
    public string? RegisteredDisplayName { get; private set; }
    public ExternalIdentity? SignedInExternal { get; private set; }

    public Task<IdentityUserDto> RegisterAsync(string email, string password, string? displayName, CancellationToken cancellationToken = default)
    {
        Calls.Add("identity.register");
        RegisteredEmail = email;
        RegisteredPassword = password;
        RegisteredDisplayName = displayName;
        return Result();
    }

    public Task<IdentityUserDto> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        Calls.Add("identity.authenticate");
        RegisteredEmail = email;
        RegisteredPassword = password;
        return Result();
    }

    public Task<IdentityUserDto> SignInWithExternalAsync(ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        Calls.Add("identity.external");
        SignedInExternal = identity;
        return Result();
    }

    private Task<IdentityUserDto> Result()
        => FailWith is null ? Task.FromResult(User) : Task.FromException<IdentityUserDto>(FailWith);
}

internal sealed class FakeJwtTokenService : IJwtTokenService
{
    public static TokenPair DefaultTokens { get; } = new(
        "access-token", "refresh-token", new DateTime(2026, 10, 8, 12, 15, 0, DateTimeKind.Utc), new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc));

    public FakeJwtTokenService(List<string>? calls = null)
    {
        Calls = calls ?? [];
    }

    public List<string> Calls { get; }

    public IdentityUserDto? IssuedFor { get; private set; }
    public string? RefreshedWith { get; private set; }
    public (Guid UserId, string Token)? Revoked { get; private set; }
    public Guid? RevokedAllFor { get; private set; }

    public AuthResult? RefreshResult { get; set; }

    public Task<TokenPair> CreateTokenPairAsync(IdentityUserDto user, CancellationToken cancellationToken = default)
    {
        Calls.Add("tokens.create");
        IssuedFor = user;
        return Task.FromResult(DefaultTokens);
    }

    public Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        Calls.Add("tokens.refresh");
        RefreshedWith = refreshToken;
        return Task.FromResult(RefreshResult ?? new AuthResult(FakeIdentityService.DefaultUser, DefaultTokens));
    }

    public Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        Calls.Add("tokens.revoke");
        Revoked = (userId, refreshToken);
        return Task.CompletedTask;
    }

    public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Calls.Add("tokens.revokeAll");
        RevokedAllFor = userId;
        return Task.CompletedTask;
    }
}

internal sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public ExternalIdentity Identity { get; set; } = new("Google", "google-subject-1", "ada@example.com", "Ada Lovelace");
    public Exception? FailWith { get; set; }
    public string? VerifiedToken { get; private set; }

    public Task<ExternalIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        VerifiedToken = idToken;
        return FailWith is null ? Task.FromResult(Identity) : Task.FromException<ExternalIdentity>(FailWith);
    }
}
