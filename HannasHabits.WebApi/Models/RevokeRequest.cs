using HannasHabits.Application.Auth.Commands.Revoke;

namespace HannasHabits.WebApi.Models;

public record RevokeRequest(string RefreshToken)
{
    public RevokeCommand ToCommand() => new(RefreshToken);
}
