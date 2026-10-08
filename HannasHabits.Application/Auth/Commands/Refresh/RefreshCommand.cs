using MediatR;

namespace HannasHabits.Application.Auth.Commands.Refresh;

public record RefreshCommand(string RefreshToken) : IRequest<AuthResult>;
