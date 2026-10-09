using MediatR;

namespace HannasHabits.Application.Auth.Commands.RevokeAll;

/// <summary>Logout everywhere: revokes every refresh token of the current user.</summary>
public record RevokeAllCommand : IRequest<Unit>;
