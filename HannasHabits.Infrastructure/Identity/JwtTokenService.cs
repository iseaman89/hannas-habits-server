using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Exceptions;
using HannasHabits.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HannasHabits.Infrastructure.Identity;

// Talks to the DbContext directly and saves with the raw SaveChangesAsync (not IUnitOfWork): it needs EF's own
// DbUpdateConcurrencyException to notice that another request used the same refresh token first.
public class JwtTokenService : IJwtTokenService
{
    // One message for every unusable refresh token (unknown, expired, revoked, replayed): no hints for an attacker.
    private const string InvalidRefreshToken = "The refresh token is invalid or has expired.";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly JwtOptions _jwt;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider,
        ILogger<JwtTokenService> logger)
    {
        _db = db;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _logger = logger;
        _jwt = options.Value;
    }

    public async Task<TokenPair> CreateTokenPairAsync(IdentityUserDto user, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var (refreshToken, refreshEntity) = NewRefreshToken(user.Id, now);

        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return ToTokenPair(user, now, refreshToken, refreshEntity);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var hash = Hash(refreshToken);

        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
                     ?? throw new AuthenticationFailedException(InvalidRefreshToken);

        // A token is used exactly once. Seeing a used one again means somebody replays a copy - the thief or the
        // real client, we cannot tell which - so no session of this user can be trusted any more.
        if (stored.IsRotated)
            throw await RejectReplayAsync(stored.UserId, now, cancellationToken);

        if (!stored.IsActive(now))
            throw new AuthenticationFailedException(InvalidRefreshToken);

        var appUser = await _userManager.FindByIdAsync(stored.UserId.ToString())
                      ?? throw new AuthenticationFailedException(InvalidRefreshToken);
        var user = appUser.ToDto();

        var (newToken, replacement) = NewRefreshToken(stored.UserId, now);
        stored.Rotate(replacement, now);
        _db.RefreshTokens.Add(replacement);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated the same token between our read and our write: the same replay as above.
            _db.ChangeTracker.Clear();
            throw await RejectReplayAsync(stored.UserId, now, cancellationToken);
        }

        return new AuthResult(user, ToTokenPair(user, now, newToken, replacement));
    }

    public async Task RevokeAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var hash = Hash(refreshToken);

        // One conditional UPDATE: atomic, and idempotent because already revoked rows do not match.
        await _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTime?)now), cancellationToken);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await RevokeAllCoreAsync(userId, Now(), cancellationToken);
    }

    private async Task<AuthenticationFailedException> RejectReplayAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        await RevokeAllCoreAsync(userId, now, cancellationToken);
        _logger.LogWarning("A used refresh token was presented again; all sessions of user {UserId} were revoked", userId);

        return new AuthenticationFailedException(InvalidRefreshToken);
    }

    private Task<int> RevokeAllCoreAsync(Guid userId, DateTime now, CancellationToken cancellationToken) => _db.RefreshTokens
        .Where(t => t.UserId == userId && t.RevokedAt == null)
        .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, (DateTime?)now), cancellationToken);

    private TokenPair ToTokenPair(IdentityUserDto user, DateTime now, string refreshToken, RefreshToken refreshEntity)
    {
        var accessTokenExpiresAt = now.AddMinutes(_jwt.AccessMinutes);
        return new TokenPair(CreateAccessToken(user, accessTokenExpiresAt), refreshToken, accessTokenExpiresAt, refreshEntity.ExpiresAt);
    }

    private string CreateAccessToken(IdentityUserDto user, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // The client gets the random token, the database only its hash.
    private (string Token, RefreshToken Entity) NewRefreshToken(Guid userId, DateTime now)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = RefreshToken.Create(userId, Hash(token), now, TimeSpan.FromDays(_jwt.RefreshDays));

        return (token, entity);
    }

    // A plain SHA-256 is enough here: unlike a password, the input is 512 random bits, so there is nothing to guess
    // and nothing for a salt or a slow hash to protect. (That would be different for a user-chosen secret.)
    private static string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
