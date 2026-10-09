using MediatR;

namespace HannasHabits.Application.Auth.Commands.Register;

/// <param name="FirstName">Optional "First name" of the register form; without it the UI shows the email's local part.</param>
/// <param name="LastName">Optional "Last name".</param>
public record RegisterCommand(string Email, string Password, string? FirstName = null, string? LastName = null) : IRequest<AuthResult>;
