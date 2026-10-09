using MediatR;

namespace HannasHabits.Application.Auth.Commands.GoogleLogin;

/// <param name="IdToken">The Google ID token (JWT) the frontend received from Google sign-in.</param>
public record GoogleLoginCommand(string IdToken) : IRequest<AuthResult>;
