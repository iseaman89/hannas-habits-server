using HannasHabits.Application.Common.Interfaces;
using MediatR;

namespace HannasHabits.Application.Auth.Commands.Revoke;

public class RevokeCommandHandler : IRequestHandler<RevokeCommand, Unit>
{
    private readonly IJwtTokenService _tokens;
    private readonly ICurrentUser _currentUser;

    public RevokeCommandHandler(IJwtTokenService tokens, ICurrentUser currentUser)
    {
        _tokens = tokens;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RevokeCommand request, CancellationToken cancellationToken)
    {
        await _tokens.RevokeAsync(_currentUser.UserId, request.RefreshToken, cancellationToken);
        return Unit.Value;
    }
}
