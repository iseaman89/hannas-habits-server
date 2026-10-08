using HannasHabits.Application.Resolutions.Commands.AddResolution;

namespace HannasHabits.WebApi.Models;

/// <param name="HabitId">Optional: one of the user's habits that tracks this resolution.</param>
public record AddResolutionRequest(string Title, Guid? HabitId)
{
    public AddResolutionCommand ToCommand(int year) => new(year, Title, HabitId);
}
