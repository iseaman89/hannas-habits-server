using MediatR;

namespace HannasHabits.Application.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResult>
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _tokens;

    public RegisterCommandHandler(IIdentityService identity, IJwtTokenService tokens)
    {
        _identity = identity;
        _tokens = tokens;
    }

    public async Task<AuthResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = await _identity.RegisterAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
        var tokens = await _tokens.CreateTokenPairAsync(user, cancellationToken);

        return new AuthResult(user, tokens);
    }
}
