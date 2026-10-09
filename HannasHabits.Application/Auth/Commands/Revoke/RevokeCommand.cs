using MediatR;

namespace HannasHabits.Application.Auth.Commands.Revoke;

/// <summary>Logout of one session: the refresh token must belong to the current user.</summary>
public record RevokeCommand(string RefreshToken) : IRequest<Unit>;
