using HannasHabits.Application.Auth.Commands.Register;

namespace HannasHabits.WebApi.Models;

public record RegisterRequest(string Email, string Password, string? FirstName = null, string? LastName = null)
{
    public RegisterCommand ToCommand() => new(Email, Password, FirstName, LastName);
}
