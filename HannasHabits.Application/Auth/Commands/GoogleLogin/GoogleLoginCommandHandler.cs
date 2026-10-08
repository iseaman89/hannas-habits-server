using MediatR;

namespace HannasHabits.Application.Auth.Commands.GoogleLogin;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, AuthResult>
{
    private readonly IGoogleTokenVerifier _google;
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _tokens;

    public GoogleLoginCommandHandler(IGoogleTokenVerifier google, IIdentityService identity, IJwtTokenService tokens)
    {
        _google = google;
        _identity = identity;
        _tokens = tokens;
    }

    public async Task<AuthResult> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var external = await _google.VerifyAsync(request.IdToken, cancellationToken);
        var user = await _identity.SignInWithExternalAsync(external, cancellationToken);
        var tokens = await _tokens.CreateTokenPairAsync(user, cancellationToken);

        return new AuthResult(user, tokens);
    }
}
