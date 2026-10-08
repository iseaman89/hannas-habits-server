using HannasHabits.Application.Auth.Commands.GoogleLogin;

namespace HannasHabits.WebApi.Models;

/// <param name="IdToken">The Google ID token (JWT) the frontend received from Google sign-in.</param>
public record GoogleLoginRequest(string IdToken)
{
    public GoogleLoginCommand ToCommand() => new(IdToken);
}
