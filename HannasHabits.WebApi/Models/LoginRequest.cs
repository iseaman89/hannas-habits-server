using HannasHabits.Application.Auth.Commands.Login;

namespace HannasHabits.WebApi.Models;

public record LoginRequest(string Email, string Password)
{
    public LoginCommand ToCommand() => new(Email, Password);
}
