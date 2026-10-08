using System.ComponentModel.DataAnnotations;

namespace HannasHabits.Infrastructure.Identity;

/// <summary>
/// Bound from the <c>Jwt:</c> configuration section (user-secrets in Development, env vars in production).
/// Validated on startup, so a missing or too weak key fails fast instead of at the first login.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 needs a key of at least 256 bit = 32 bytes.
    [Required, MinLength(32)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 24 * 60)]
    public int AccessMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshDays { get; init; } = 30;
}
