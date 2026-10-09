using MediatR;

namespace HannasHabits.Application.Auth.Commands.Refresh;

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, AuthResult>
{
    private readonly IJwtTokenService _tokens;

    public RefreshCommandHandler(IJwtTokenService tokens)
    {
        _tokens = tokens;
    }

    public Task<AuthResult> Handle(RefreshCommand request, CancellationToken cancellationToken) =>
        _tokens.RefreshAsync(request.RefreshToken, cancellationToken);
}
