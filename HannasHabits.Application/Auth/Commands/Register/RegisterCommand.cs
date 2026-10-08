using MediatR;

namespace HannasHabits.Application.Auth.Commands.Register;

public record RegisterCommand(string Email, string Password) : IRequest<AuthResult>;
