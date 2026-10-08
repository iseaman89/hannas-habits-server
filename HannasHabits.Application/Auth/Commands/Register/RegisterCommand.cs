using MediatR;

namespace HannasHabits.Application.Auth.Commands.Register;

/// <param name="DisplayName">Optional "Your name" of the register form; without it the UI shows the email's local part.</param>
public record RegisterCommand(string Email, string Password, string? DisplayName = null) : IRequest<AuthResult>;
