namespace HannasHabits.Application.Auth;

/// <summary>Issues access tokens (JWT) and manages the lifecycle of refresh tokens.</summary>
public interface IJwtTokenService
{
    /// <summary>Creates an access token and a new refresh token for a user who has just proven who they are.</summary>
    Task<TokenPair> CreateTokenPairAsync(IdentityUserDto user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a new pair; the presented token is used up (rotation).
    /// Throws <see cref="Common.Exceptions.AuthenticationFailedException"/> for an unknown, expired or revoked token.
    /// Presenting an already used token again is treated as theft: every session of that user is revoked.
    /// </summary>
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes one refresh token of the user. Idempotent: afterwards the token is unusable, whether it was active,
    /// already revoked, expired, unknown or belongs to somebody else (the last two do nothing).
    /// </summary>
    Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
