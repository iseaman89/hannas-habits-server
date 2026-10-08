using Google.Apis.Auth;
using HannasHabits.Application.Auth;
using HannasHabits.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HannasHabits.Infrastructure.Identity;

public class GoogleTokenVerifier : IGoogleTokenVerifier
{
    public const string Provider = "Google";

    private readonly GoogleOptions _options;
    private readonly ILogger<GoogleTokenVerifier> _logger;

    public GoogleTokenVerifier(IOptions<GoogleOptions> options, ILogger<GoogleTokenVerifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExternalIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Checks signature (Google's public keys, cached by the library), expiry, issuer and - the important
            // one - the audience: a token Google issued for some other app must not log in here.
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId]
            });
        }
        catch (Exception exception) when (exception is not (HttpRequestException or OperationCanceledException))
        {
            // InvalidJwtException for a bad signature, audience or expiry - but for input that is not even a JWT the
            // library leaks its parser's exceptions (Newtonsoft's JsonReaderException, FormatException, ...). The input
            // is whatever a client sends, so everything except "could not reach Google" means "not a valid token".
            _logger.LogInformation("Rejected a Google ID token: {ExceptionType}", exception.GetType().Name);
            throw new AuthenticationFailedException("The Google token is not valid.");
        }

        // The email is what links a Google identity to an account, so it has to be one Google vouches for.
        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
            throw new AuthenticationFailedException("The Google account has no verified email address.");

        return new ExternalIdentity(Provider, payload.Subject, payload.Email, payload.Name);
    }
}
