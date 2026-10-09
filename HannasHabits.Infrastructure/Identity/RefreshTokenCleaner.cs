using HannasHabits.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HannasHabits.Infrastructure.Identity;

/// <summary>
/// Deletes refresh token rows that have no job left. Every login and every refresh adds a row and nothing else ever
/// removes one, so without this the table only grows.
/// <para>
/// The rule is about expiry alone: a row goes <see cref="RefreshTokenCleanupOptions.RetentionDays"/> after
/// <c>ExpiresAt</c>, whether it was used up, logged out or simply never touched again. Replay detection is why a
/// <i>rotated</i> row must not go earlier - a used token that shows up again revokes all sessions of its user, and that
/// needs the row. But a copy of a token is only worth stealing while the token has not expired; afterwards the row
/// protects nothing, so there is no reason to keep it longer than the grace period.
/// </para>
/// </summary>
public class RefreshTokenCleaner
{
    private readonly ApplicationDbContext _db;
    private readonly RefreshTokenCleanupOptions _options;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenCleaner(ApplicationDbContext db, IOptions<RefreshTokenCleanupOptions> options, TimeProvider timeProvider)
    {
        _db = db;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    /// <summary>Returns how many rows were deleted. One statement, so it is safe to run on several instances at once.</summary>
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - TimeSpan.FromDays(_options.RetentionDays);

        return _db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
