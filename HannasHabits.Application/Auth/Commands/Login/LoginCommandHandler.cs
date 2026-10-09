using MediatR;

namespace HannasHabits.Application.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _tokens;

    public LoginCommandHandler(IIdentityService identity, IJwtTokenService tokens)
    {
        _identity = identity;
        _tokens = tokens;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _identity.AuthenticateAsync(request.Email, request.Password, cancellationToken);
        var tokens = await _tokens.CreateTokenPairAsync(user, cancellationToken);

        return new AuthResult(user, tokens);
    }
}
