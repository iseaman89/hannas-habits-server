using HannasHabits.Application.Common.Interfaces;
using MediatR;

namespace HannasHabits.Application.Auth.Commands.RevokeAll;

public class RevokeAllCommandHandler : IRequestHandler<RevokeAllCommand, Unit>
{
    private readonly IJwtTokenService _tokens;
    private readonly ICurrentUser _currentUser;

    public RevokeAllCommandHandler(IJwtTokenService tokens, ICurrentUser currentUser)
    {
        _tokens = tokens;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RevokeAllCommand request, CancellationToken cancellationToken)
    {
        await _tokens.RevokeAllAsync(_currentUser.UserId, cancellationToken);
        return Unit.Value;
    }
}
