namespace HannasHabits.Infrastructure.Identity;

/// <summary>
/// A refresh token as stored in the database. Only the SHA-256 hash of the token is kept, so a leaked database
/// (or backup) does not hand out working sessions. It lives in Infrastructure, not in the Domain: it is a security
/// artefact of the Identity setup, not part of the habit model.
/// </summary>
public class RefreshToken
{
    // EF Core only; instances are created through Create, so TokenHash is never null.
    private RefreshToken()
    {
        TokenHash = null!;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    /// <summary>When the token stopped being usable: logged out, or used up by a refresh.</summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>Set when a refresh used this token up. A used token that shows up again is a replay.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime now, TimeSpan lifetime) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = now + lifetime
    };

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>True if a refresh already exchanged this token for a new one.</summary>
    public bool IsRotated => ReplacedByTokenId is not null;

    /// <summary>Uses this token up and records its successor.</summary>
    public void Rotate(RefreshToken replacement, DateTime now)
    {
        RevokedAt = now;
        ReplacedByTokenId = replacement.Id;
    }
}
