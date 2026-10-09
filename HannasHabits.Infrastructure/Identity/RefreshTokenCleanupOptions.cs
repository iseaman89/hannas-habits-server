using System.ComponentModel.DataAnnotations;

namespace HannasHabits.Infrastructure.Identity;

/// <summary>
/// Bound from the <c>RefreshTokenCleanup:</c> configuration section. Both values have a sensible default, so the
/// section is optional.
/// </summary>
public class RefreshTokenCleanupOptions
{
    public const string SectionName = "RefreshTokenCleanup";

    /// <summary>How often the cleanup runs (the first run is right at startup).</summary>
    [Range(1, 7 * 24)]
    public int IntervalHours { get; init; } = 6;

    /// <summary>
    /// How long a refresh token row is kept after it expired. Zero would be enough for security (see
    /// <see cref="RefreshTokenCleaner"/>); the grace period keeps a row around long enough to answer "why was my
    /// session rejected?" and absorbs clock differences between several instances.
    /// </summary>
    [Range(0, 365)]
    public int RetentionDays { get; init; } = 7;
}
