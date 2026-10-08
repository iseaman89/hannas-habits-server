using HannasHabits.Application.Auth.Commands.Refresh;

namespace HannasHabits.WebApi.Models;

public record RefreshRequest(string RefreshToken)
{
    public RefreshCommand ToCommand() => new(RefreshToken);
}
