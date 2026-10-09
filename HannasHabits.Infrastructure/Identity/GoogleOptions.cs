using System.ComponentModel.DataAnnotations;

namespace HannasHabits.Infrastructure.Identity;

/// <summary>
/// Bound from the <c>Google:</c> configuration section (user-secrets in Development, env var
/// <c>Google__ClientId</c> in production). The client id is public (it is embedded in the frontend) but differs
/// per environment, so it is configuration, not code.
/// </summary>
public class GoogleOptions
{
    public const string SectionName = "Google";

    /// <summary>OAuth client id of the frontend; ID tokens issued for any other client are rejected.</summary>
    [Required]
    public string ClientId { get; init; } = string.Empty;
}
