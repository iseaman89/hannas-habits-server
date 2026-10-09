namespace HannasHabits.Application.Auth;

public interface IGoogleTokenVerifier
{
    /// <summary>
    /// Verifies a Google ID token (signature, expiry, issuer, audience = our client id) and returns the identity
    /// it vouches for. Throws <see cref="Common.Exceptions.AuthenticationFailedException"/> if the token is not valid
    /// or Google has not verified the email address.
    /// </summary>
    Task<ExternalIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken = default);
}
